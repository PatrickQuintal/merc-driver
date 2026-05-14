#include <windows.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <strsafe.h>

#include <string>
#include <vector>

namespace
{
constexpr wchar_t AppName[] = L"Merc Keyboard Mapper";
constexpr wchar_t MapperExe[] = L"MercKeyboardMapperEngine.exe";
constexpr wchar_t StartupValueName[] = L"MercMapperGui";
constexpr wchar_t RunKeyPath[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
constexpr UINT_PTR TrayIconId = 1;
constexpr UINT WmTray = WM_APP + 1;
constexpr UINT WmLog = WM_APP + 2;
constexpr UINT WmMapperStopped = WM_APP + 3;

UINT g_taskbarCreatedMessage = 0;
HWND g_window = nullptr;
HWND g_status = nullptr;
HWND g_log = nullptr;
HWND g_enableQ = nullptr;
HWND g_repeat = nullptr;
HWND g_repeatDelay = nullptr;
HWND g_repeatRate = nullptr;
HWND g_startup = nullptr;
NOTIFYICONDATAW g_tray{};
PROCESS_INFORMATION g_mapper{};
HANDLE g_outputThread = nullptr;
HANDLE g_waitThread = nullptr;
HANDLE g_outputPipe = nullptr;
HANDLE g_stopEvent = nullptr;
bool g_stopping = false;
bool g_startupLaunch = false;
DWORD g_mapperGeneration = 0;

struct WaitContext
{
    HANDLE Process;
    DWORD Generation;
};

bool IsChecked(HWND control)
{
    return SendMessageW(control, BM_GETCHECK, 0, 0) == BST_CHECKED;
}

int ComboValue(HWND combo, int fallback)
{
    const LRESULT index = SendMessageW(combo, CB_GETCURSEL, 0, 0);
    if (index == CB_ERR)
    {
        return fallback;
    }

    wchar_t text[32]{};
    SendMessageW(combo, CB_GETLBTEXT, static_cast<WPARAM>(index), reinterpret_cast<LPARAM>(text));
    const int value = _wtoi(text);
    return value > 0 ? value : fallback;
}

void SetComboValue(HWND combo, int value)
{
    wchar_t text[32]{};
    StringCchPrintfW(text, ARRAYSIZE(text), L"%d", value);
    const LRESULT index = SendMessageW(combo, CB_FINDSTRINGEXACT, static_cast<WPARAM>(-1), reinterpret_cast<LPARAM>(text));
    if (index != CB_ERR)
    {
        SendMessageW(combo, CB_SETCURSEL, static_cast<WPARAM>(index), 0);
    }
}

std::wstring Quote(const std::wstring& value)
{
    return L"\"" + value + L"\"";
}

std::wstring CurrentExePath()
{
    wchar_t path[MAX_PATH]{};
    GetModuleFileNameW(nullptr, path, ARRAYSIZE(path));
    return path;
}

std::wstring CurrentDirectoryPath()
{
    wchar_t path[MAX_PATH]{};
    GetModuleFileNameW(nullptr, path, ARRAYSIZE(path));
    PathRemoveFileSpecW(path);
    return path;
}

std::wstring MapperPath()
{
    wchar_t path[MAX_PATH]{};
    StringCchPrintfW(path, ARRAYSIZE(path), L"%s\\%s", CurrentDirectoryPath().c_str(), MapperExe);
    return path;
}

std::wstring BuildMapperArguments(bool includeStartup)
{
    std::wstring args;
    if (includeStartup)
    {
        args += L" --startup";
    }

    if (!IsChecked(g_enableQ))
    {
        args += L" --no-q";
    }

    if (IsChecked(g_repeat))
    {
        args += L" --repeat --repeat-delay-ms ";
        args += std::to_wstring(ComboValue(g_repeatDelay, 350));
        args += L" --repeat-rate-ms ";
        args += std::to_wstring(ComboValue(g_repeatRate, 35));
    }

    return args;
}

std::wstring BuildStartupCommand()
{
    return Quote(CurrentExePath()) + L" --startup" + BuildMapperArguments(false);
}

bool CommandLineHasArg(const wchar_t* expected)
{
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argv == nullptr)
    {
        return false;
    }

    bool found = false;
    for (int index = 1; index < argc; ++index)
    {
        if (_wcsicmp(argv[index], expected) == 0)
        {
            found = true;
            break;
        }
    }

    LocalFree(argv);
    return found;
}

void PostLog(const wchar_t* message)
{
    const size_t length = wcslen(message) + 1;
    wchar_t* copy = new wchar_t[length];
    StringCchCopyW(copy, length, message);
    PostMessageW(g_window, WmLog, 0, reinterpret_cast<LPARAM>(copy));
}

void AddLog(HWND list, const wchar_t* message)
{
    wchar_t line[1024]{};
    SYSTEMTIME time{};
    GetLocalTime(&time);
    StringCchPrintfW(line, ARRAYSIZE(line), L"%02hu:%02hu:%02hu %s", time.wHour, time.wMinute, time.wSecond, message);
    SendMessageW(list, LB_INSERTSTRING, 0, reinterpret_cast<LPARAM>(line));
    while (SendMessageW(list, LB_GETCOUNT, 0, 0) > 200)
    {
        SendMessageW(list, LB_DELETESTRING, 200, 0);
    }
}

void UpdateStatus()
{
    const bool running = g_mapper.hProcess != nullptr;
    SetWindowTextW(g_status, running ? L"Mapper running." : L"Mapper stopped.");
}

bool IsStartupEnabled()
{
    HKEY key = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, RunKeyPath, 0, KEY_QUERY_VALUE, &key) != ERROR_SUCCESS)
    {
        return false;
    }

    wchar_t value[2048]{};
    DWORD size = sizeof(value);
    const LSTATUS status = RegGetValueW(key, nullptr, StartupValueName, RRF_RT_REG_SZ, nullptr, value, &size);
    RegCloseKey(key);
    return status == ERROR_SUCCESS && value[0] != L'\0';
}

bool SetStartupEnabled(bool enabled)
{
    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, RunKeyPath, 0, nullptr, 0, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS)
    {
        return false;
    }

    bool ok = false;
    if (enabled)
    {
        const std::wstring command = BuildStartupCommand();
        ok = RegSetValueExW(key, StartupValueName, 0, REG_SZ, reinterpret_cast<const BYTE*>(command.c_str()),
            static_cast<DWORD>((command.length() + 1) * sizeof(wchar_t))) == ERROR_SUCCESS;
    }
    else
    {
        const LSTATUS status = RegDeleteValueW(key, StartupValueName);
        ok = status == ERROR_SUCCESS || status == ERROR_FILE_NOT_FOUND;
    }

    RegCloseKey(key);
    return ok;
}

DWORD WINAPI OutputThreadProc(void*)
{
    char buffer[512]{};
    std::string pending;
    DWORD read = 0;
    while (ReadFile(g_outputPipe, buffer, sizeof(buffer) - 1, &read, nullptr) && read > 0)
    {
        buffer[read] = '\0';
        pending.append(buffer, read);
        size_t newline = std::string::npos;
        while ((newline = pending.find('\n')) != std::string::npos)
        {
            std::string line = pending.substr(0, newline);
            pending.erase(0, newline + 1);
            while (!line.empty() && (line.back() == '\r' || line.back() == '\n'))
            {
                line.pop_back();
            }

            if (!line.empty())
            {
                int wideLength = MultiByteToWideChar(CP_UTF8, 0, line.c_str(), -1, nullptr, 0);
                if (wideLength <= 0)
                {
                    wideLength = MultiByteToWideChar(CP_ACP, 0, line.c_str(), -1, nullptr, 0);
                }

                if (wideLength > 0)
                {
                    std::vector<wchar_t> wide(static_cast<size_t>(wideLength));
                    if (MultiByteToWideChar(CP_UTF8, 0, line.c_str(), -1, wide.data(), wideLength) <= 0)
                    {
                        MultiByteToWideChar(CP_ACP, 0, line.c_str(), -1, wide.data(), wideLength);
                    }

                    PostLog(wide.data());
                }
            }
        }
    }

    if (!pending.empty())
    {
        int wideLength = MultiByteToWideChar(CP_UTF8, 0, pending.c_str(), -1, nullptr, 0);
        if (wideLength > 0)
        {
            std::vector<wchar_t> wide(static_cast<size_t>(wideLength));
            MultiByteToWideChar(CP_UTF8, 0, pending.c_str(), -1, wide.data(), wideLength);
            PostLog(wide.data());
        }
    }

    return 0;
}

DWORD WINAPI WaitThreadProc(void* parameter)
{
    WaitContext* context = static_cast<WaitContext*>(parameter);
    WaitForSingleObject(context->Process, INFINITE);
    CloseHandle(context->Process);
    PostMessageW(g_window, WmMapperStopped, static_cast<WPARAM>(context->Generation), 0);
    delete context;
    return 0;
}

void CloseMapperHandles()
{
    if (g_outputPipe != nullptr)
    {
        CloseHandle(g_outputPipe);
        g_outputPipe = nullptr;
    }

    if (g_outputThread != nullptr)
    {
        CloseHandle(g_outputThread);
        g_outputThread = nullptr;
    }

    if (g_waitThread != nullptr)
    {
        CloseHandle(g_waitThread);
        g_waitThread = nullptr;
    }

    if (g_mapper.hThread != nullptr)
    {
        CloseHandle(g_mapper.hThread);
        g_mapper.hThread = nullptr;
    }

    if (g_mapper.hProcess != nullptr)
    {
        CloseHandle(g_mapper.hProcess);
        g_mapper.hProcess = nullptr;
    }

    if (g_stopEvent != nullptr)
    {
        CloseHandle(g_stopEvent);
        g_stopEvent = nullptr;
    }
}

void StopMapper(bool logStop)
{
    if (g_mapper.hProcess == nullptr)
    {
        return;
    }

    g_stopping = true;
    if (g_stopEvent != nullptr)
    {
        SetEvent(g_stopEvent);
    }

    if (WaitForSingleObject(g_mapper.hProcess, 3000) == WAIT_TIMEOUT)
    {
        AddLog(g_log, L"Mapper did not stop gracefully; forcing shutdown.");
        TerminateProcess(g_mapper.hProcess, 0);
        WaitForSingleObject(g_mapper.hProcess, 3000);
    }

    CloseMapperHandles();
    g_stopping = false;
    if (logStop)
    {
        AddLog(g_log, L"Mapper stopped.");
    }

    UpdateStatus();
}

bool StartMapper(const wchar_t* reason)
{
    if (g_mapper.hProcess != nullptr)
    {
        return true;
    }

    const std::wstring mapperPath = MapperPath();
    if (!PathFileExistsW(mapperPath.c_str()))
    {
        AddLog(g_log, L"Mapper engine not found next to wrapper.");
        UpdateStatus();
        return false;
    }

    SECURITY_ATTRIBUTES security{};
    security.nLength = sizeof(security);
    security.bInheritHandle = TRUE;

    HANDLE readPipe = nullptr;
    HANDLE writePipe = nullptr;
    if (!CreatePipe(&readPipe, &writePipe, &security, 0))
    {
        AddLog(g_log, L"Could not create mapper output pipe.");
        return false;
    }

    SetHandleInformation(readPipe, HANDLE_FLAG_INHERIT, 0);

    wchar_t stopEventName[128]{};
    StringCchPrintfW(stopEventName, ARRAYSIZE(stopEventName), L"Local\\MercKeyboardMapperStop_%lu_%lu", GetCurrentProcessId(), GetTickCount());
    g_stopEvent = CreateEventW(nullptr, TRUE, FALSE, stopEventName);
    if (g_stopEvent == nullptr)
    {
        CloseHandle(readPipe);
        CloseHandle(writePipe);
        AddLog(g_log, L"Could not create mapper stop event.");
        return false;
    }

    HANDLE nulInput = CreateFileW(L"NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, &security, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    STARTUPINFOW startup{};
    startup.cb = sizeof(startup);
    startup.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    startup.wShowWindow = SW_HIDE;
    startup.hStdOutput = writePipe;
    startup.hStdError = writePipe;
    startup.hStdInput = nulInput == INVALID_HANDLE_VALUE ? nullptr : nulInput;

    std::wstring commandLine = Quote(mapperPath) + BuildMapperArguments(g_startupLaunch) + L" --stop-event " + Quote(stopEventName);
    std::vector<wchar_t> mutableCommand(commandLine.begin(), commandLine.end());
    mutableCommand.push_back(L'\0');
    PROCESS_INFORMATION process{};
    const BOOL created = CreateProcessW(nullptr, mutableCommand.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW,
        nullptr, CurrentDirectoryPath().c_str(), &startup, &process);

    CloseHandle(writePipe);
    if (nulInput != INVALID_HANDLE_VALUE)
    {
        CloseHandle(nulInput);
    }

    if (!created)
    {
        CloseHandle(readPipe);
        CloseHandle(g_stopEvent);
        g_stopEvent = nullptr;
        AddLog(g_log, L"Mapper process failed to start.");
        UpdateStatus();
        return false;
    }

    HANDLE waitProcess = nullptr;
    if (!DuplicateHandle(GetCurrentProcess(), process.hProcess, GetCurrentProcess(), &waitProcess, SYNCHRONIZE, FALSE, 0))
    {
        TerminateProcess(process.hProcess, 0);
        CloseHandle(process.hThread);
        CloseHandle(process.hProcess);
        CloseHandle(readPipe);
        CloseHandle(g_stopEvent);
        g_stopEvent = nullptr;
        AddLog(g_log, L"Mapper process monitor failed to start.");
        UpdateStatus();
        return false;
    }

    const DWORD generation = ++g_mapperGeneration;
    WaitContext* waitContext = new WaitContext{ waitProcess, generation };

    g_mapper = process;
    g_outputPipe = readPipe;
    g_outputThread = CreateThread(nullptr, 0, OutputThreadProc, nullptr, 0, nullptr);
    g_waitThread = CreateThread(nullptr, 0, WaitThreadProc, waitContext, 0, nullptr);
    if (g_waitThread == nullptr)
    {
        CloseHandle(waitProcess);
        delete waitContext;
        TerminateProcess(process.hProcess, 0);
        WaitForSingleObject(process.hProcess, 3000);
        CloseMapperHandles();
        AddLog(g_log, L"Mapper process monitor failed to start.");
        UpdateStatus();
        return false;
    }

    AddLog(g_log, reason);
    UpdateStatus();
    return true;
}

void RestartMapper(const wchar_t* reason)
{
    StopMapper(false);
    StartMapper(reason);
    if (IsChecked(g_startup))
    {
        SetStartupEnabled(true);
    }
}

void AddTrayIcon(HWND window)
{
    g_tray.cbSize = sizeof(g_tray);
    g_tray.hWnd = window;
    g_tray.uID = TrayIconId;
    g_tray.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    g_tray.uCallbackMessage = WmTray;
    g_tray.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
    StringCchCopyW(g_tray.szTip, ARRAYSIZE(g_tray.szTip), AppName);
    if (!Shell_NotifyIconW(NIM_ADD, &g_tray))
    {
        AddLog(g_log, L"Tray icon could not be added.");
    }
}

void RemoveTrayIcon()
{
    if (g_tray.cbSize != 0)
    {
        Shell_NotifyIconW(NIM_DELETE, &g_tray);
    }
}

void ShowTrayMenu(HWND window)
{
    HMENU menu = CreatePopupMenu();
    AppendMenuW(menu, MF_STRING, 2001, L"Show");
    AppendMenuW(menu, MF_STRING, 2002, L"Exit");
    POINT point{};
    GetCursorPos(&point);
    SetForegroundWindow(window);
    TrackPopupMenu(menu, TPM_RIGHTBUTTON, point.x, point.y, 0, window, nullptr);
    DestroyMenu(menu);
}

void ParseStartupOptions()
{
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    if (argv == nullptr)
    {
        return;
    }

    int delay = 350;
    int rate = 35;
    bool enableQ = true;
    bool repeat = false;

    for (int index = 1; index < argc; ++index)
    {
        if (_wcsicmp(argv[index], L"--startup") == 0)
        {
            g_startupLaunch = true;
        }
        else if (_wcsicmp(argv[index], L"--no-q") == 0)
        {
            enableQ = false;
        }
        else if (_wcsicmp(argv[index], L"--repeat") == 0)
        {
            repeat = true;
        }
        else if (_wcsicmp(argv[index], L"--repeat-delay-ms") == 0 && index + 1 < argc)
        {
            delay = _wtoi(argv[++index]);
        }
        else if (_wcsicmp(argv[index], L"--repeat-rate-ms") == 0 && index + 1 < argc)
        {
            rate = _wtoi(argv[++index]);
        }
    }

    SendMessageW(g_enableQ, BM_SETCHECK, enableQ ? BST_CHECKED : BST_UNCHECKED, 0);
    SendMessageW(g_repeat, BM_SETCHECK, repeat ? BST_CHECKED : BST_UNCHECKED, 0);
    SetComboValue(g_repeatDelay, delay > 0 ? delay : 350);
    SetComboValue(g_repeatRate, rate > 0 ? rate : 35);
    LocalFree(argv);
}

void AddComboItems(HWND combo, const int* values, size_t count, int selected)
{
    for (size_t index = 0; index < count; ++index)
    {
        wchar_t text[32]{};
        StringCchPrintfW(text, ARRAYSIZE(text), L"%d", values[index]);
        SendMessageW(combo, CB_ADDSTRING, 0, reinterpret_cast<LPARAM>(text));
    }

    SetComboValue(combo, selected);
}

void CreateControls(HWND window)
{
    HFONT font = reinterpret_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));

    CreateWindowW(L"STATIC", L"Merc Keyboard Mapper", WS_CHILD | WS_VISIBLE, 24, 20, 520, 24, window, nullptr, nullptr, nullptr);
    CreateWindowW(L"STATIC", L"Native wrapper. Mapper is active while this app is running; closing hides to tray.", WS_CHILD | WS_VISIBLE, 24, 50, 620, 24, window, nullptr, nullptr, nullptr);
    g_status = CreateWindowW(L"STATIC", L"Mapper stopped.", WS_CHILD | WS_VISIBLE, 24, 84, 240, 24, window, nullptr, nullptr, nullptr);

    g_enableQ = CreateWindowW(L"BUTTON", L"Enable Q / refresh key", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 24, 130, 240, 24, window, reinterpret_cast<HMENU>(1001), nullptr, nullptr);
    g_repeat = CreateWindowW(L"BUTTON", L"Repeat key press", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 24, 162, 240, 24, window, reinterpret_cast<HMENU>(1003), nullptr, nullptr);
    g_startup = CreateWindowW(L"BUTTON", L"Launch on startup", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 24, 194, 240, 24, window, reinterpret_cast<HMENU>(1004), nullptr, nullptr);

    CreateWindowW(L"STATIC", L"Initial repeat delay", WS_CHILD | WS_VISIBLE, 300, 130, 160, 24, window, nullptr, nullptr, nullptr);
    g_repeatDelay = CreateWindowW(L"COMBOBOX", L"", WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST, 470, 126, 110, 140, window, reinterpret_cast<HMENU>(1005), nullptr, nullptr);
    CreateWindowW(L"STATIC", L"Repeat rate", WS_CHILD | WS_VISIBLE, 300, 170, 160, 24, window, nullptr, nullptr, nullptr);
    g_repeatRate = CreateWindowW(L"COMBOBOX", L"", WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST, 470, 166, 110, 140, window, reinterpret_cast<HMENU>(1006), nullptr, nullptr);

    CreateWindowW(L"STATIC", L"Recent log", WS_CHILD | WS_VISIBLE, 24, 246, 200, 24, window, nullptr, nullptr, nullptr);
    g_log = CreateWindowExW(WS_EX_CLIENTEDGE, L"LISTBOX", L"", WS_CHILD | WS_VISIBLE | WS_VSCROLL | LBS_NOINTEGRALHEIGHT,
        24, 274, 720, 242, window, reinterpret_cast<HMENU>(1007), nullptr, nullptr);

    SendMessageW(g_enableQ, BM_SETCHECK, BST_CHECKED, 0);
    const int delays[] = { 150, 250, 350, 500 };
    const int rates[] = { 30, 35, 50, 75, 100 };
    AddComboItems(g_repeatDelay, delays, ARRAYSIZE(delays), 350);
    AddComboItems(g_repeatRate, rates, ARRAYSIZE(rates), 35);

    HWND controls[] = { g_status, g_enableQ, g_repeat, g_startup, g_repeatDelay, g_repeatRate, g_log };
    for (HWND control : controls)
    {
        SendMessageW(control, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
    }
}

LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_CREATE:
        g_window = window;
        CreateControls(window);
        ParseStartupOptions();
        SendMessageW(g_startup, BM_SETCHECK, IsStartupEnabled() ? BST_CHECKED : BST_UNCHECKED, 0);
        AddTrayIcon(window);
        AddLog(g_log, L"Wrapper ready.");
        StartMapper(g_startupLaunch ? L"Mapper started from Windows startup." : L"Mapper started.");
        return 0;
    case WM_COMMAND:
        switch (LOWORD(wParam))
        {
        case 1001:
        case 1003:
            RestartMapper(L"Mapper reloaded with updated settings.");
            return 0;
        case 1004:
            AddLog(g_log, SetStartupEnabled(IsChecked(g_startup)) ? L"Startup setting updated." : L"Startup setting update failed.");
            return 0;
        case 1005:
        case 1006:
            if (HIWORD(wParam) == CBN_SELCHANGE)
            {
                RestartMapper(L"Mapper reloaded with updated repeat settings.");
            }
            return 0;
        case 2002:
            DestroyWindow(window);
            return 0;
        case 2001:
            ShowWindow(window, SW_SHOWNORMAL);
            SetForegroundWindow(window);
            return 0;
        default:
            break;
        }
        break;
    case WmTray:
        if (lParam == WM_LBUTTONUP)
        {
            ShowWindow(window, SW_SHOWNORMAL);
            SetForegroundWindow(window);
            return 0;
        }
        if (lParam == WM_RBUTTONUP)
        {
            ShowTrayMenu(window);
            return 0;
        }
        break;
    default:
        if (message == g_taskbarCreatedMessage)
        {
            AddTrayIcon(window);
            return 0;
        }
        break;
    case WmLog:
    {
        wchar_t* text = reinterpret_cast<wchar_t*>(lParam);
        AddLog(g_log, text);
        delete[] text;
        return 0;
    }
    case WmMapperStopped:
        if (!g_stopping && static_cast<DWORD>(wParam) == g_mapperGeneration)
        {
            CloseMapperHandles();
            AddLog(g_log, L"Mapper process exited.");
            UpdateStatus();
        }
        return 0;
    case WM_CLOSE:
        ShowWindow(window, SW_HIDE);
        return 0;
    case WM_DESTROY:
        StopMapper(false);
        RemoveTrayIcon();
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(window, message, wParam, lParam);
}
}

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    HANDLE mutex = CreateMutexW(nullptr, TRUE, L"Local\\MercKeyboardMapperSingleInstance");
    if (mutex == nullptr)
    {
        MessageBoxW(nullptr, L"Could not create Merc Keyboard Mapper single-instance lock.", AppName, MB_ICONERROR | MB_OK);
        return 1;
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        HWND existing = FindWindowW(L"MercKeyboardMapperNativeWindow", nullptr);
        if (existing != nullptr && !CommandLineHasArg(L"--startup"))
        {
            ShowWindow(existing, SW_SHOWNORMAL);
            SetForegroundWindow(existing);
        }

        CloseHandle(mutex);
        return 0;
    }

    g_taskbarCreatedMessage = RegisterWindowMessageW(L"TaskbarCreated");

    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = WindowProc;
    windowClass.hInstance = instance;
    windowClass.lpszClassName = L"MercKeyboardMapperNativeWindow";
    windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    windowClass.hIcon = LoadIconW(nullptr, IDI_APPLICATION);
    windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    RegisterClassW(&windowClass);

    HWND window = CreateWindowExW(0, windowClass.lpszClassName, AppName,
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT, CW_USEDEFAULT, 790, 620,
        nullptr, nullptr, instance, nullptr);
    if (window == nullptr)
    {
        MessageBoxW(nullptr, L"Could not create Merc Keyboard Mapper window.", AppName, MB_ICONERROR | MB_OK);
        return 1;
    }

    ShowWindow(window, g_startupLaunch ? SW_HIDE : SW_SHOWNORMAL);
    UpdateWindow(window);

    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    ReleaseMutex(mutex);
    CloseHandle(mutex);
    return 0;
}
