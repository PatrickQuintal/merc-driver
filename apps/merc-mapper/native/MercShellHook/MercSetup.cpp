#include <windows.h>
#include <commdlg.h>
#include <shlobj.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <strsafe.h>

namespace
{
constexpr int PayloadResourceId = 101;
constexpr wchar_t AppName[] = L"Merc Keyboard Mapper";
constexpr wchar_t SetupTitle[] = L"Merc Keyboard Mapper Setup";
constexpr wchar_t RuntimeDownloadUrl[] = L"https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe";
constexpr wchar_t UninstallKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MercKeyboardMapper";
constexpr wchar_t RunKeyPath[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
constexpr wchar_t StartupValueName[] = L"MercMapperGui";

HWND g_installDirEdit = nullptr;
HWND g_launchCheck = nullptr;
HWND g_startWithWindowsCheck = nullptr;
HWND g_status = nullptr;
HWND g_installButton = nullptr;
HWND g_browseButton = nullptr;
HWND g_cancelButton = nullptr;

bool HasArg(LPWSTR* argv, int argc, const wchar_t* expected)
{
    for (int index = 1; index < argc; ++index)
    {
        if (_wcsicmp(argv[index], expected) == 0)
        {
            return true;
        }
    }

    return false;
}

const wchar_t* GetArgValue(LPWSTR* argv, int argc, const wchar_t* optionName)
{
    for (int index = 1; index < argc - 1; ++index)
    {
        if (_wcsicmp(argv[index], optionName) == 0)
        {
            return argv[index + 1];
        }
    }

    return nullptr;
}

bool StartsWithNoCase(const wchar_t* value, const wchar_t* prefix)
{
    const size_t prefixLength = wcslen(prefix);
    return _wcsnicmp(value, prefix, prefixLength) == 0 &&
        (value[prefixLength] == L'\0' || value[prefixLength] == L'\\');
}

bool IsRunningAsAdmin()
{
    BOOL isAdmin = FALSE;
    PSID adminGroup = nullptr;
    SID_IDENTIFIER_AUTHORITY authority = SECURITY_NT_AUTHORITY;
    if (AllocateAndInitializeSid(&authority, 2, SECURITY_BUILTIN_DOMAIN_RID, DOMAIN_ALIAS_RID_ADMINS,
        0, 0, 0, 0, 0, 0, &adminGroup))
    {
        CheckTokenMembership(nullptr, adminGroup, &isAdmin);
        FreeSid(adminGroup);
    }

    return isAdmin == TRUE;
}

bool RequiresAdmin(const wchar_t* installDirectory)
{
    wchar_t programFiles[MAX_PATH]{};
    if (FAILED(SHGetFolderPathW(nullptr, CSIDL_PROGRAM_FILES, nullptr, SHGFP_TYPE_CURRENT, programFiles)))
    {
        return false;
    }

    return StartsWithNoCase(installDirectory, programFiles);
}

void QuoteAppend(wchar_t* buffer, size_t count, const wchar_t* value)
{
    StringCchCatW(buffer, count, L"\"");
    StringCchCatW(buffer, count, value);
    StringCchCatW(buffer, count, L"\"");
}

void PowerShellSingleQuoteAppend(wchar_t* buffer, size_t count, const wchar_t* value)
{
    StringCchCatW(buffer, count, L"'");
    for (const wchar_t* cursor = value; *cursor != L'\0'; ++cursor)
    {
        if (*cursor == L'\'')
        {
            StringCchCatW(buffer, count, L"''");
        }
        else
        {
            const wchar_t character[] = { *cursor, L'\0' };
            StringCchCatW(buffer, count, character);
        }
    }

    StringCchCatW(buffer, count, L"'");
}

bool RelaunchElevatedAndWait(const wchar_t* arguments, DWORD* exitCode)
{
    wchar_t exePath[MAX_PATH]{};
    GetModuleFileNameW(nullptr, exePath, ARRAYSIZE(exePath));

    SHELLEXECUTEINFOW execute{};
    execute.cbSize = sizeof(execute);
    execute.fMask = SEE_MASK_NOCLOSEPROCESS;
    execute.lpVerb = L"runas";
    execute.lpFile = exePath;
    execute.lpParameters = arguments;
    execute.nShow = SW_SHOWNORMAL;
    if (!ShellExecuteExW(&execute) || execute.hProcess == nullptr)
    {
        return false;
    }

    WaitForSingleObject(execute.hProcess, INFINITE);
    DWORD childExitCode = 1;
    GetExitCodeProcess(execute.hProcess, &childExitCode);
    CloseHandle(execute.hProcess);
    if (exitCode != nullptr)
    {
        *exitCode = childExitCode;
    }

    return true;
}

void GetDefaultInstallDirectory(wchar_t* buffer, size_t count)
{
    wchar_t programFiles[MAX_PATH]{};
    SHGetFolderPathW(nullptr, CSIDL_PROGRAM_FILES, nullptr, SHGFP_TYPE_CURRENT, programFiles);
    StringCchPrintfW(buffer, count, L"%s\\%s", programFiles, AppName);
}

bool HasRequiredDotNetRuntime()
{
    wchar_t runtimeGlob[MAX_PATH]{};
    wchar_t programFiles[MAX_PATH]{};
    if (FAILED(SHGetFolderPathW(nullptr, CSIDL_PROGRAM_FILES, nullptr, SHGFP_TYPE_CURRENT, programFiles)))
    {
        return false;
    }

    StringCchPrintfW(runtimeGlob, ARRAYSIZE(runtimeGlob), L"%s\\dotnet\\shared\\Microsoft.NETCore.App\\8.*", programFiles);

    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(runtimeGlob, &data);
    if (find == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    FindClose(find);
    return true;
}

void DeleteDirectoryTree(const wchar_t* path)
{
    if (!PathFileExistsW(path))
    {
        return;
    }

    wchar_t from[MAX_PATH + 2]{};
    StringCchCopyW(from, ARRAYSIZE(from), path);

    SHFILEOPSTRUCTW operation{};
    operation.wFunc = FO_DELETE;
    operation.pFrom = from;
    operation.fFlags = FOF_NO_UI;
    SHFileOperationW(&operation);
}

void AppendSetupLog(const wchar_t* message)
{
    wchar_t tempPath[MAX_PATH]{};
    wchar_t logPath[MAX_PATH]{};
    GetTempPathW(ARRAYSIZE(tempPath), tempPath);
    StringCchPrintfW(logPath, ARRAYSIZE(logPath), L"%sMercKeyboardMapperSetup.log", tempPath);

    HANDLE file = CreateFileW(logPath, FILE_APPEND_DATA, FILE_SHARE_READ, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
    {
        return;
    }

    SYSTEMTIME time{};
    GetLocalTime(&time);
    wchar_t line[1024]{};
    StringCchPrintfW(line, ARRAYSIZE(line), L"%04hu-%02hu-%02hu %02hu:%02hu:%02hu %s\r\n",
        time.wYear, time.wMonth, time.wDay, time.wHour, time.wMinute, time.wSecond, message);

    char utf8[4096]{};
    const int count = WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8, ARRAYSIZE(utf8), nullptr, nullptr);
    if (count > 1)
    {
        DWORD written = 0;
        WriteFile(file, utf8, static_cast<DWORD>(count - 1), &written, nullptr);
    }

    CloseHandle(file);
}

bool WritePayloadZip(const wchar_t* zipPath)
{
    HRSRC resource = FindResourceW(nullptr, MAKEINTRESOURCEW(PayloadResourceId), RT_RCDATA);
    if (resource == nullptr)
    {
        return false;
    }

    HGLOBAL loaded = LoadResource(nullptr, resource);
    if (loaded == nullptr)
    {
        return false;
    }

    void* data = LockResource(loaded);
    DWORD size = SizeofResource(nullptr, resource);
    if (data == nullptr || size == 0)
    {
        return false;
    }

    HANDLE file = CreateFileW(zipPath, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
    {
        return false;
    }

    DWORD written = 0;
    const BOOL ok = WriteFile(file, data, size, &written, nullptr);
    CloseHandle(file);
    return ok == TRUE && written == size;
}

bool RunProcessAndWait(const wchar_t* fileName, const wchar_t* arguments)
{
    SHELLEXECUTEINFOW execute{};
    execute.cbSize = sizeof(execute);
    execute.fMask = SEE_MASK_NOCLOSEPROCESS;
    execute.lpFile = fileName;
    execute.lpParameters = arguments;
    execute.nShow = SW_HIDE;
    if (!ShellExecuteExW(&execute) || execute.hProcess == nullptr)
    {
        return false;
    }

    WaitForSingleObject(execute.hProcess, INFINITE);
    DWORD exitCode = 1;
    GetExitCodeProcess(execute.hProcess, &exitCode);
    CloseHandle(execute.hProcess);
    return exitCode == 0;
}

void RunProcessIgnoringExitCode(const wchar_t* fileName, const wchar_t* arguments)
{
    SHELLEXECUTEINFOW execute{};
    execute.cbSize = sizeof(execute);
    execute.fMask = SEE_MASK_NOCLOSEPROCESS;
    execute.lpFile = fileName;
    execute.lpParameters = arguments;
    execute.nShow = SW_HIDE;
    if (!ShellExecuteExW(&execute) || execute.hProcess == nullptr)
    {
        return;
    }

    WaitForSingleObject(execute.hProcess, 3000);
    CloseHandle(execute.hProcess);
}

void StopRunningMapperProcesses()
{
    RunProcessIgnoringExitCode(L"taskkill.exe", L"/im MercKeyboardMapper.exe /f");
    RunProcessIgnoringExitCode(L"taskkill.exe", L"/im MercKeyboardMapperEngine.exe /f");
    RunProcessIgnoringExitCode(L"taskkill.exe", L"/im Merc.Mapper.exe /f");
    RunProcessIgnoringExitCode(L"taskkill.exe", L"/im MercShellHookHost32.exe /f");
}

bool LaunchInstalledApp(const wchar_t* installDirectory)
{
    wchar_t appPath[MAX_PATH]{};
    StringCchPrintfW(appPath, ARRAYSIZE(appPath), L"%s\\MercKeyboardMapper.exe", installDirectory);

    SHELLEXECUTEINFOW execute{};
    execute.cbSize = sizeof(execute);
    execute.lpFile = appPath;
    execute.lpDirectory = installDirectory;
    execute.nShow = SW_SHOWNORMAL;
    if (!ShellExecuteExW(&execute))
    {
        AppendSetupLog(L"Failed to launch installed app.");
        return false;
    }

    return true;
}

bool HasRequiredPayloadFiles(const wchar_t* directory)
{
    const wchar_t* requiredFiles[] = {
        L"MercKeyboardMapper.exe",
        L"MercKeyboardMapperEngine.exe",
        L"MercKeyboardMapperEngine.dll",
        L"MercKeyboardMapperEngine.runtimeconfig.json",
        L"Merc.Mapper.Core.dll",
        L"MercShellHook64.dll",
        L"MercShellHook32.dll",
        L"MercShellHookHost32.exe",
        L"MercKeyboardMapperUninstall.exe"
    };

    for (const wchar_t* requiredFile : requiredFiles)
    {
        wchar_t path[MAX_PATH]{};
        StringCchPrintfW(path, ARRAYSIZE(path), L"%s\\%s", directory, requiredFile);
        if (!PathFileExistsW(path))
        {
            wchar_t message[512]{};
            StringCchPrintfW(message, ARRAYSIZE(message), L"Missing payload file after extraction: %s", requiredFile);
            AppendSetupLog(message);
            return false;
        }
    }

    return true;
}

bool CopyExtractedPayload(const wchar_t* extractDirectory, const wchar_t* installDirectory)
{
    DeleteDirectoryTree(installDirectory);
    for (int attempt = 0; attempt < 30 && PathFileExistsW(installDirectory); ++attempt)
    {
        Sleep(100);
    }

    if (!CreateDirectoryW(installDirectory, nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
    {
        AppendSetupLog(L"Failed to create install directory.");
        return false;
    }

    wchar_t command[4096]{};
    StringCchCopyW(command, ARRAYSIZE(command), L"-NoProfile -ExecutionPolicy Bypass -Command \"$src = ");
    PowerShellSingleQuoteAppend(command, ARRAYSIZE(command), extractDirectory);
    StringCchCatW(command, ARRAYSIZE(command), L"; $dst = ");
    PowerShellSingleQuoteAppend(command, ARRAYSIZE(command), installDirectory);
    StringCchCatW(command, ARRAYSIZE(command),
        L"; $lockedOk = @('MercShellHook32.dll','MercShellHook64.dll'); Get-ChildItem -LiteralPath $src -Force | ForEach-Object { $item = $_; $target = Join-Path $dst $item.Name; try { Copy-Item -LiteralPath $item.FullName -Destination $target -Recurse -Force -ErrorAction Stop } catch { if (($lockedOk -notcontains $item.Name) -or (-not (Test-Path -LiteralPath $target))) { throw } } }\"");

    if (!RunProcessAndWait(L"powershell.exe", command))
    {
        AppendSetupLog(L"Failed to copy extracted payload into install directory.");
        return false;
    }

    if (!HasRequiredPayloadFiles(installDirectory))
    {
        AppendSetupLog(L"Final install directory failed payload validation.");
        return false;
    }

    return true;
}

bool ExtractPayload(const wchar_t* installDirectory)
{
    wchar_t tempPath[MAX_PATH]{};
    wchar_t zipPath[MAX_PATH]{};
    wchar_t extractDirectory[MAX_PATH]{};
    GetTempPathW(ARRAYSIZE(tempPath), tempPath);
    StringCchPrintfW(zipPath, ARRAYSIZE(zipPath), L"%smerc-mapper-payload-native.zip", tempPath);
    StringCchPrintfW(extractDirectory, ARRAYSIZE(extractDirectory), L"%smerc-mapper-extract-%lu", tempPath, GetCurrentProcessId());

    if (!WritePayloadZip(zipPath))
    {
        AppendSetupLog(L"Failed to write embedded payload zip.");
        return false;
    }

    DeleteDirectoryTree(extractDirectory);
    if (!CreateDirectoryW(extractDirectory, nullptr))
    {
        AppendSetupLog(L"Failed to create temporary extraction directory.");
        DeleteFileW(zipPath);
        return false;
    }

    wchar_t command[4096]{};
    StringCchCopyW(command, ARRAYSIZE(command), L"-NoProfile -ExecutionPolicy Bypass -Command \"Expand-Archive -LiteralPath ");
    PowerShellSingleQuoteAppend(command, ARRAYSIZE(command), zipPath);
    StringCchCatW(command, ARRAYSIZE(command), L" -DestinationPath ");
    PowerShellSingleQuoteAppend(command, ARRAYSIZE(command), extractDirectory);
    StringCchCatW(command, ARRAYSIZE(command), L" -Force\"");

    const bool ok = RunProcessAndWait(L"powershell.exe", command);
    DeleteFileW(zipPath);
    if (!ok)
    {
        AppendSetupLog(L"Expand-Archive failed.");
        DeleteDirectoryTree(extractDirectory);
        return false;
    }

    if (!HasRequiredPayloadFiles(extractDirectory))
    {
        DeleteDirectoryTree(extractDirectory);
        return false;
    }

    if (!CopyExtractedPayload(extractDirectory, installDirectory))
    {
        DeleteDirectoryTree(extractDirectory);
        return false;
    }

    DeleteDirectoryTree(extractDirectory);
    return true;
}

bool CreateShortcut(const wchar_t* shortcutPath, const wchar_t* targetPath, const wchar_t* arguments, const wchar_t* workingDirectory)
{
    IShellLinkW* link = nullptr;
    HRESULT result = CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER, IID_IShellLinkW, reinterpret_cast<void**>(&link));
    if (FAILED(result) || link == nullptr)
    {
        return false;
    }

    link->SetPath(targetPath);
    link->SetArguments(arguments);
    link->SetWorkingDirectory(workingDirectory);

    IPersistFile* file = nullptr;
    result = link->QueryInterface(IID_IPersistFile, reinterpret_cast<void**>(&file));
    if (SUCCEEDED(result) && file != nullptr)
    {
        result = file->Save(shortcutPath, TRUE);
        file->Release();
    }

    link->Release();
    return SUCCEEDED(result);
}

bool CreateStartMenuShortcuts(const wchar_t* installDirectory, bool machineInstall)
{
    wchar_t root[MAX_PATH]{};
    const int folder = machineInstall ? CSIDL_COMMON_PROGRAMS : CSIDL_PROGRAMS;
    if (FAILED(SHGetFolderPathW(nullptr, folder, nullptr, SHGFP_TYPE_CURRENT, root)))
    {
        AppendSetupLog(L"Failed to resolve Start Menu folder.");
        return false;
    }

    wchar_t appFolder[MAX_PATH]{};
    StringCchPrintfW(appFolder, ARRAYSIZE(appFolder), L"%s\\%s", root, AppName);
    if (!CreateDirectoryW(appFolder, nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
    {
        AppendSetupLog(L"Failed to create Start Menu app folder.");
        return false;
    }

    wchar_t appPath[MAX_PATH]{};
    wchar_t uninstallPath[MAX_PATH]{};
    wchar_t appShortcut[MAX_PATH]{};
    wchar_t uninstallShortcut[MAX_PATH]{};
    StringCchPrintfW(appPath, ARRAYSIZE(appPath), L"%s\\MercKeyboardMapper.exe", installDirectory);
    StringCchPrintfW(uninstallPath, ARRAYSIZE(uninstallPath), L"%s\\MercKeyboardMapperUninstall.exe", installDirectory);
    StringCchPrintfW(appShortcut, ARRAYSIZE(appShortcut), L"%s\\Merc Keyboard Mapper.lnk", appFolder);
    StringCchPrintfW(uninstallShortcut, ARRAYSIZE(uninstallShortcut), L"%s\\Uninstall Merc Keyboard Mapper.lnk", appFolder);

    if (!CreateShortcut(appShortcut, appPath, L"", installDirectory))
    {
        AppendSetupLog(L"Failed to create app Start Menu shortcut.");
        return false;
    }

    if (!CreateShortcut(uninstallShortcut, uninstallPath, L"--uninstall", installDirectory))
    {
        AppendSetupLog(L"Failed to create uninstall Start Menu shortcut.");
        return false;
    }

    return true;
}

DWORD EstimateSizeKiB(const wchar_t* installDirectory)
{
    wchar_t pattern[MAX_PATH]{};
    StringCchPrintfW(pattern, ARRAYSIZE(pattern), L"%s\\*", installDirectory);

    ULONGLONG total = 0;
    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(pattern, &data);
    if (find != INVALID_HANDLE_VALUE)
    {
        do
        {
            if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == 0)
            {
                ULARGE_INTEGER size{};
                size.HighPart = data.nFileSizeHigh;
                size.LowPart = data.nFileSizeLow;
                total += size.QuadPart;
            }
        } while (FindNextFileW(find, &data));
        FindClose(find);
    }

    const ULONGLONG kib = total / 1024;
    return static_cast<DWORD>(kib == 0 ? 1 : kib);
}

bool RegisterUninstall(const wchar_t* installDirectory, bool machineInstall)
{
    HKEY key = nullptr;
    HKEY root = machineInstall ? HKEY_LOCAL_MACHINE : HKEY_CURRENT_USER;
    if (RegCreateKeyExW(root, UninstallKey, 0, nullptr, 0, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS)
    {
        AppendSetupLog(L"Failed to create Add/Remove Programs registry key.");
        return false;
    }

    wchar_t appPath[MAX_PATH]{};
    wchar_t uninstallPath[MAX_PATH]{};
    wchar_t uninstallCommand[MAX_PATH + 32]{};
    StringCchPrintfW(appPath, ARRAYSIZE(appPath), L"%s\\MercKeyboardMapper.exe", installDirectory);
    StringCchPrintfW(uninstallPath, ARRAYSIZE(uninstallPath), L"%s\\MercKeyboardMapperUninstall.exe", installDirectory);
    StringCchPrintfW(uninstallCommand, ARRAYSIZE(uninstallCommand), L"\"%s\" --uninstall", uninstallPath);

    DWORD one = 1;
    DWORD estimatedSize = EstimateSizeKiB(installDirectory);
    wchar_t quietUninstallCommand[MAX_PATH + 32]{};
    StringCchPrintfW(quietUninstallCommand, ARRAYSIZE(quietUninstallCommand), L"\"%s\" --quiet-uninstall", uninstallPath);

    bool ok = true;
    ok = ok && RegSetValueExW(key, L"DisplayName", 0, REG_SZ, reinterpret_cast<const BYTE*>(AppName), static_cast<DWORD>((wcslen(AppName) + 1) * sizeof(wchar_t))) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"DisplayVersion", 0, REG_SZ, reinterpret_cast<const BYTE*>(L"1.0.1"), sizeof(L"1.0.1")) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"Publisher", 0, REG_SZ, reinterpret_cast<const BYTE*>(L"merc-driver"), sizeof(L"merc-driver")) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"InstallLocation", 0, REG_SZ, reinterpret_cast<const BYTE*>(installDirectory), static_cast<DWORD>((wcslen(installDirectory) + 1) * sizeof(wchar_t))) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"DisplayIcon", 0, REG_SZ, reinterpret_cast<const BYTE*>(appPath), static_cast<DWORD>((wcslen(appPath) + 1) * sizeof(wchar_t))) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"UninstallString", 0, REG_SZ, reinterpret_cast<const BYTE*>(uninstallCommand), static_cast<DWORD>((wcslen(uninstallCommand) + 1) * sizeof(wchar_t))) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"QuietUninstallString", 0, REG_SZ, reinterpret_cast<const BYTE*>(quietUninstallCommand), static_cast<DWORD>((wcslen(quietUninstallCommand) + 1) * sizeof(wchar_t))) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"NoModify", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&one), sizeof(one)) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"NoRepair", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&one), sizeof(one)) == ERROR_SUCCESS;
    ok = ok && RegSetValueExW(key, L"EstimatedSize", 0, REG_DWORD, reinterpret_cast<const BYTE*>(&estimatedSize), sizeof(estimatedSize)) == ERROR_SUCCESS;
    RegCloseKey(key);

    if (!ok)
    {
        AppendSetupLog(L"Failed to write Add/Remove Programs registry values.");
    }

    return ok;
}

bool RegisterStartupForInstalledApp(const wchar_t* installDirectory)
{
    HKEY key = nullptr;
    if (RegCreateKeyExW(HKEY_CURRENT_USER, RunKeyPath, 0, nullptr, 0, KEY_SET_VALUE, nullptr, &key, nullptr) != ERROR_SUCCESS)
    {
        AppendSetupLog(L"Failed to open HKCU Run key for startup registration.");
        return false;
    }

    wchar_t appPath[MAX_PATH]{};
    wchar_t command[MAX_PATH + 16]{};
    StringCchPrintfW(appPath, ARRAYSIZE(appPath), L"%s\\MercKeyboardMapper.exe", installDirectory);
    StringCchPrintfW(command, ARRAYSIZE(command), L"\"%s\" --startup", appPath);
    const LSTATUS status = RegSetValueExW(key, StartupValueName, 0, REG_SZ, reinterpret_cast<const BYTE*>(command),
        static_cast<DWORD>((wcslen(command) + 1) * sizeof(wchar_t)));
    RegCloseKey(key);

    if (status != ERROR_SUCCESS)
    {
        AppendSetupLog(L"Failed to write startup Run value.");
        return false;
    }

    return true;
}

bool Install(const wchar_t* installDirectory, bool launchAfterInstall, bool startWithWindows)
{
    StopRunningMapperProcesses();

    if (!ExtractPayload(installDirectory))
    {
        return false;
    }

    const bool machineInstall = RequiresAdmin(installDirectory);
    if (!CreateStartMenuShortcuts(installDirectory, machineInstall))
    {
        return false;
    }

    if (!RegisterUninstall(installDirectory, machineInstall))
    {
        return false;
    }
    if (startWithWindows && !RegisterStartupForInstalledApp(installDirectory))
    {
        return false;
    }

    if (launchAfterInstall)
    {
        return LaunchInstalledApp(installDirectory);
    }

    return true;
}

void BrowseForInstallDirectory(HWND owner)
{
    wchar_t current[MAX_PATH]{};
    GetWindowTextW(g_installDirEdit, current, ARRAYSIZE(current));

    BROWSEINFOW browse{};
    browse.hwndOwner = owner;
    browse.lpszTitle = L"Choose where Merc Keyboard Mapper should be installed.";
    browse.ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE;
    PIDLIST_ABSOLUTE item = SHBrowseForFolderW(&browse);
    if (item == nullptr)
    {
        return;
    }

    wchar_t selected[MAX_PATH]{};
    if (SHGetPathFromIDListW(item, selected))
    {
        SetWindowTextW(g_installDirEdit, selected);
    }

    CoTaskMemFree(item);
}

void SetInstallingUi(bool installing)
{
    EnableWindow(g_installButton, !installing);
    EnableWindow(g_browseButton, !installing);
    EnableWindow(g_cancelButton, !installing);
    SetWindowTextW(g_status, installing ? L"Installing..." : L"");
}

void RunInteractiveInstall(HWND window)
{
    wchar_t installDirectory[MAX_PATH]{};
    GetWindowTextW(g_installDirEdit, installDirectory, ARRAYSIZE(installDirectory));
    if (installDirectory[0] == L'\0')
    {
        MessageBoxW(window, L"Choose an install location.", SetupTitle, MB_ICONWARNING | MB_OK);
        return;
    }

    if (!HasRequiredDotNetRuntime())
    {
        const int answer = MessageBoxW(window,
            L"Merc Keyboard Mapper requires Microsoft .NET 8 Runtime x64. Open the Microsoft download page now?",
            L"Required runtime missing",
            MB_ICONINFORMATION | MB_YESNO);
        if (answer == IDYES)
        {
            ShellExecuteW(nullptr, L"open", RuntimeDownloadUrl, nullptr, nullptr, SW_SHOWNORMAL);
        }

        return;
    }

    if (RequiresAdmin(installDirectory) && !IsRunningAsAdmin())
    {
        wchar_t args[2048]{};
        StringCchCopyW(args, ARRAYSIZE(args), L"--quiet-install --install-dir ");
        QuoteAppend(args, ARRAYSIZE(args), installDirectory);

        SetInstallingUi(true);
        DWORD exitCode = 1;
        const bool elevated = RelaunchElevatedAndWait(args, &exitCode);
        SetInstallingUi(false);
        if (elevated && exitCode == 0)
        {
            if (SendMessageW(g_startWithWindowsCheck, BM_GETCHECK, 0, 0) == BST_CHECKED &&
                !RegisterStartupForInstalledApp(installDirectory))
            {
                MessageBoxW(window,
                    L"Merc Keyboard Mapper was installed, but startup registration failed. Check %TEMP%\\MercKeyboardMapperSetup.log for details.",
                    SetupTitle,
                    MB_ICONWARNING | MB_OK);
            }

            if (SendMessageW(g_launchCheck, BM_GETCHECK, 0, 0) == BST_CHECKED)
            {
                if (!LaunchInstalledApp(installDirectory))
                {
                    MessageBoxW(window,
                        L"Merc Keyboard Mapper was installed, but launch failed. Use the Start Menu shortcut to open it.",
                        SetupTitle,
                        MB_ICONWARNING | MB_OK);
                }
            }

            MessageBoxW(window, L"Merc Keyboard Mapper has been installed.", SetupTitle, MB_ICONINFORMATION | MB_OK);
            PostQuitMessage(0);
        }
        else
        {
            MessageBoxW(window,
                L"Installation failed during elevated setup. Check %TEMP%\\MercKeyboardMapperSetup.log for details.",
                SetupTitle,
                MB_ICONERROR | MB_OK);
        }

        return;
    }

    SetInstallingUi(true);
    const bool launch = SendMessageW(g_launchCheck, BM_GETCHECK, 0, 0) == BST_CHECKED;
    const bool startWithWindows = SendMessageW(g_startWithWindowsCheck, BM_GETCHECK, 0, 0) == BST_CHECKED;
    if (!Install(installDirectory, launch, startWithWindows))
    {
        SetInstallingUi(false);
        MessageBoxW(window, L"Installation failed.", SetupTitle, MB_ICONERROR | MB_OK);
        return;
    }

    SetInstallingUi(false);
    MessageBoxW(window, L"Merc Keyboard Mapper has been installed.", SetupTitle, MB_ICONINFORMATION | MB_OK);
    PostQuitMessage(0);
}

LRESULT CALLBACK WindowProc(HWND window, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_CREATE:
    {
        HFONT font = reinterpret_cast<HFONT>(GetStockObject(DEFAULT_GUI_FONT));
        CreateWindowW(L"STATIC", L"Merc Keyboard Mapper Setup", WS_CHILD | WS_VISIBLE, 24, 22, 560, 24, window, nullptr, nullptr, nullptr);
        CreateWindowW(L"STATIC", L"Install Merc Keyboard Mapper. The mapper requires Microsoft .NET 8 Runtime x64; setup will prompt if it is missing.", WS_CHILD | WS_VISIBLE, 24, 60, 560, 48, window, nullptr, nullptr, nullptr);

        g_installDirEdit = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"", WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL, 24, 142, 448, 25, window, reinterpret_cast<HMENU>(1001), nullptr, nullptr);
        g_browseButton = CreateWindowW(L"BUTTON", L"Browse...", WS_CHILD | WS_VISIBLE, 486, 140, 96, 29, window, reinterpret_cast<HMENU>(1002), nullptr, nullptr);
        g_launchCheck = CreateWindowW(L"BUTTON", L"Launch Merc Keyboard Mapper after installation", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 24, 186, 420, 24, window, reinterpret_cast<HMENU>(1003), nullptr, nullptr);
        SendMessageW(g_launchCheck, BM_SETCHECK, BST_CHECKED, 0);
        g_startWithWindowsCheck = CreateWindowW(L"BUTTON", L"Start Merc Keyboard Mapper when Windows starts", WS_CHILD | WS_VISIBLE | BS_AUTOCHECKBOX, 24, 214, 420, 24, window, reinterpret_cast<HMENU>(1006), nullptr, nullptr);
        SendMessageW(g_startWithWindowsCheck, BM_SETCHECK, BST_CHECKED, 0);
        g_status = CreateWindowW(L"STATIC", L"", WS_CHILD | WS_VISIBLE, 24, 258, 558, 24, window, nullptr, nullptr, nullptr);
        g_installButton = CreateWindowW(L"BUTTON", L"Install", WS_CHILD | WS_VISIBLE, 408, 322, 88, 30, window, reinterpret_cast<HMENU>(1004), nullptr, nullptr);
        g_cancelButton = CreateWindowW(L"BUTTON", L"Cancel", WS_CHILD | WS_VISIBLE, 502, 322, 88, 30, window, reinterpret_cast<HMENU>(1005), nullptr, nullptr);

        wchar_t defaultDirectory[MAX_PATH]{};
        GetDefaultInstallDirectory(defaultDirectory, ARRAYSIZE(defaultDirectory));
        SetWindowTextW(g_installDirEdit, defaultDirectory);

        HWND controls[] = { g_installDirEdit, g_browseButton, g_launchCheck, g_startWithWindowsCheck, g_status, g_installButton, g_cancelButton };
        for (HWND control : controls)
        {
            SendMessageW(control, WM_SETFONT, reinterpret_cast<WPARAM>(font), TRUE);
        }

        return 0;
    }
    case WM_COMMAND:
        switch (LOWORD(wParam))
        {
        case 1002:
            BrowseForInstallDirectory(window);
            return 0;
        case 1004:
            RunInteractiveInstall(window);
            return 0;
        case 1005:
            PostQuitMessage(0);
            return 0;
        default:
            break;
        }
        break;
    case WM_CLOSE:
        PostQuitMessage(0);
        return 0;
    default:
        break;
    }

    return DefWindowProcW(window, message, wParam, lParam);
}
}

int APIENTRY wWinMain(HINSTANCE instance, HINSTANCE, LPWSTR, int)
{
    CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    const bool quietInstall = argv != nullptr && HasArg(argv, argc, L"--quiet-install");
    const bool skipRuntimeCheck = argv != nullptr && HasArg(argv, argc, L"--skip-runtime-check");

    if (quietInstall)
    {
        wchar_t installDirectory[MAX_PATH]{};
        const wchar_t* specifiedDirectory = GetArgValue(argv, argc, L"--install-dir");
        if (specifiedDirectory != nullptr)
        {
            StringCchCopyW(installDirectory, ARRAYSIZE(installDirectory), specifiedDirectory);
        }
        else
        {
            GetDefaultInstallDirectory(installDirectory, ARRAYSIZE(installDirectory));
        }

        if (RequiresAdmin(installDirectory) && !IsRunningAsAdmin())
        {
            const wchar_t* commandLine = GetCommandLineW();
            const wchar_t* args = wcschr(commandLine, L' ');
            DWORD exitCode = 1;
            if (!RelaunchElevatedAndWait(args == nullptr ? L"" : args + 1, &exitCode))
            {
                LocalFree(argv);
                CoUninitialize();
                return 3;
            }

            LocalFree(argv);
            CoUninitialize();
            return static_cast<int>(exitCode);
        }

        if (!skipRuntimeCheck && !HasRequiredDotNetRuntime())
        {
            LocalFree(argv);
            CoUninitialize();
            return 2;
        }

        const bool launch = HasArg(argv, argc, L"--launch");
        const bool startWithWindows = HasArg(argv, argc, L"--start-with-windows");
        const bool ok = Install(installDirectory, launch, startWithWindows);
        LocalFree(argv);
        CoUninitialize();
        return ok ? 0 : 1;
    }

    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = WindowProc;
    windowClass.hInstance = instance;
    windowClass.lpszClassName = L"MercKeyboardMapperSetupWindow";
    windowClass.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    windowClass.hbrBackground = reinterpret_cast<HBRUSH>(COLOR_WINDOW + 1);
    RegisterClassW(&windowClass);

    HWND window = CreateWindowExW(0, windowClass.lpszClassName, SetupTitle,
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX,
        CW_USEDEFAULT, CW_USEDEFAULT, 630, 410,
        nullptr, nullptr, instance, nullptr);
    if (window == nullptr)
    {
        MessageBoxW(nullptr, L"Could not create setup window.", SetupTitle, MB_ICONERROR | MB_OK);
        if (argv != nullptr)
        {
            LocalFree(argv);
        }
        CoUninitialize();
        return 1;
    }

    ShowWindow(window, SW_SHOWNORMAL);
    UpdateWindow(window);

    MSG message{};
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    if (argv != nullptr)
    {
        LocalFree(argv);
    }
    CoUninitialize();
    return 0;
}
