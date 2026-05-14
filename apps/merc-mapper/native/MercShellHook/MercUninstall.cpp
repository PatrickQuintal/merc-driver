#include <windows.h>
#include <shlobj.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <strsafe.h>

namespace
{
constexpr wchar_t AppName[] = L"Merc Keyboard Mapper";
constexpr wchar_t UninstallKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\MercKeyboardMapper";
constexpr wchar_t RunKey[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Run";

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

bool StartsWithNoCase(const wchar_t* value, const wchar_t* prefix)
{
    const size_t prefixLength = wcslen(prefix);
    return _wcsnicmp(value, prefix, prefixLength) == 0 &&
        (value[prefixLength] == L'\0' || value[prefixLength] == L'\\');
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

void RelaunchElevated(bool quiet)
{
    wchar_t exePath[MAX_PATH]{};
    GetModuleFileNameW(nullptr, exePath, ARRAYSIZE(exePath));
    ShellExecuteW(nullptr, L"runas", exePath, quiet ? L"--quiet-uninstall" : L"--uninstall", nullptr, SW_SHOWNORMAL);
}

void DeleteStartupValues()
{
    HKEY runKey = nullptr;
    if (RegOpenKeyExW(HKEY_CURRENT_USER, RunKey, 0, KEY_SET_VALUE, &runKey) == ERROR_SUCCESS)
    {
        RegDeleteValueW(runKey, L"MercMapper");
        RegDeleteValueW(runKey, L"MercMapperGui");
        RegCloseKey(runKey);
    }
}

void DeleteRegistryTree(HKEY root, const wchar_t* subKey)
{
    RegDeleteTreeW(root, subKey);
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

void DeleteDirectoryTree(const wchar_t* path)
{
    wchar_t from[MAX_PATH + 2]{};
    StringCchCopyW(from, ARRAYSIZE(from), path);

    SHFILEOPSTRUCTW operation{};
    operation.wFunc = FO_DELETE;
    operation.pFrom = from;
    operation.fFlags = FOF_NO_UI;
    SHFileOperationW(&operation);
}

void DeleteStartMenuShortcut(bool machineInstall)
{
    wchar_t root[MAX_PATH]{};
    const int folder = machineInstall ? CSIDL_COMMON_PROGRAMS : CSIDL_PROGRAMS;
    if (FAILED(SHGetFolderPathW(nullptr, folder, nullptr, SHGFP_TYPE_CURRENT, root)))
    {
        return;
    }

    wchar_t appFolder[MAX_PATH]{};
    if (SUCCEEDED(StringCchPrintfW(appFolder, ARRAYSIZE(appFolder), L"%s\\%s", root, AppName)))
    {
        DeleteDirectoryTree(appFolder);
    }
}

void ScheduleInstallDirectoryRemoval(const wchar_t* installDirectory)
{
    wchar_t tempPath[MAX_PATH]{};
    wchar_t scriptPath[MAX_PATH]{};
    GetTempPathW(ARRAYSIZE(tempPath), tempPath);
    StringCchPrintfW(scriptPath, ARRAYSIZE(scriptPath), L"%smerc-mapper-uninstall-cleanup.cmd", tempPath);

    HANDLE file = CreateFileW(scriptPath, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (file == INVALID_HANDLE_VALUE)
    {
        return;
    }

    char buffer[MAX_PATH * 4]{};
    char installDirectoryUtf8[MAX_PATH * 3]{};
    WideCharToMultiByte(CP_UTF8, 0, installDirectory, -1, installDirectoryUtf8, ARRAYSIZE(installDirectoryUtf8), nullptr, nullptr);
    StringCchPrintfA(buffer, ARRAYSIZE(buffer),
        "@echo off\r\n"
        "ping 127.0.0.1 -n 3 > nul\r\n"
        "rmdir /s /q \"%s\"\r\n"
        "del \"%%~f0\"\r\n",
        installDirectoryUtf8);

    DWORD written = 0;
    WriteFile(file, buffer, static_cast<DWORD>(strlen(buffer)), &written, nullptr);
    CloseHandle(file);

    wchar_t parameters[MAX_PATH + 16]{};
    StringCchPrintfW(parameters, ARRAYSIZE(parameters), L"/d /c \"%s\"", scriptPath);
    ShellExecuteW(nullptr, L"open", L"cmd.exe", parameters, nullptr, SW_HIDE);
}

void ScheduleRemainingFilesForRebootDeletion(const wchar_t* installDirectory)
{
    wchar_t pattern[MAX_PATH]{};
    StringCchPrintfW(pattern, ARRAYSIZE(pattern), L"%s\\*", installDirectory);

    WIN32_FIND_DATAW data{};
    HANDLE find = FindFirstFileW(pattern, &data);
    if (find != INVALID_HANDLE_VALUE)
    {
        do
        {
            if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
            {
                continue;
            }

            wchar_t filePath[MAX_PATH]{};
            StringCchPrintfW(filePath, ARRAYSIZE(filePath), L"%s\\%s", installDirectory, data.cFileName);
            MoveFileExW(filePath, nullptr, MOVEFILE_DELAY_UNTIL_REBOOT);
        } while (FindNextFileW(find, &data));
        FindClose(find);
    }

    MoveFileExW(installDirectory, nullptr, MOVEFILE_DELAY_UNTIL_REBOOT);
}
}

int APIENTRY wWinMain(HINSTANCE, HINSTANCE, LPWSTR, int)
{
    int argc = 0;
    LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
    const bool quiet = argv != nullptr && HasArg(argv, argc, L"--quiet-uninstall");

    wchar_t exePath[MAX_PATH]{};
    GetModuleFileNameW(nullptr, exePath, ARRAYSIZE(exePath));

    wchar_t installDirectory[MAX_PATH]{};
    StringCchCopyW(installDirectory, ARRAYSIZE(installDirectory), exePath);
    PathRemoveFileSpecW(installDirectory);

    const bool machineInstall = RequiresAdmin(installDirectory);
    if (machineInstall && !IsRunningAsAdmin())
    {
        DeleteStartupValues();
        RelaunchElevated(quiet);
        if (argv != nullptr)
        {
            LocalFree(argv);
        }

        return 0;
    }

    if (!quiet)
    {
        const int answer = MessageBoxW(nullptr,
            L"Remove Merc Keyboard Mapper from this computer?",
            L"Uninstall Merc Keyboard Mapper",
            MB_ICONQUESTION | MB_YESNO | MB_DEFBUTTON2);
        if (answer != IDYES)
        {
            if (argv != nullptr)
            {
                LocalFree(argv);
            }

            return 0;
        }
    }

    StopRunningMapperProcesses();
    DeleteStartupValues();
    DeleteStartMenuShortcut(machineInstall);
    DeleteRegistryTree(machineInstall ? HKEY_LOCAL_MACHINE : HKEY_CURRENT_USER, UninstallKey);
    ScheduleInstallDirectoryRemoval(installDirectory);
    ScheduleRemainingFilesForRebootDeletion(installDirectory);

    if (!quiet)
    {
        MessageBoxW(nullptr, L"Merc Keyboard Mapper has been uninstalled.", L"Uninstall complete", MB_ICONINFORMATION | MB_OK);
    }

    if (argv != nullptr)
    {
        LocalFree(argv);
    }

    return 0;
}
