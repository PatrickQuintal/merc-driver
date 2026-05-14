#include <windows.h>

using InstallHook = BOOL(__stdcall*)();
using UninstallHook = BOOL(__stdcall*)();

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE previousInstance, PWSTR commandLine, int showCommand)
{
    UNREFERENCED_PARAMETER(instance);
    UNREFERENCED_PARAMETER(previousInstance);
    UNREFERENCED_PARAMETER(commandLine);
    UNREFERENCED_PARAMETER(showCommand);

    HMODULE hookModule = LoadLibraryW(L"MercShellHook32.dll");
    if (hookModule == nullptr)
    {
        return 2;
    }

    auto installHook = reinterpret_cast<InstallHook>(GetProcAddress(hookModule, "InstallMercShellHook"));
    auto uninstallHook = reinterpret_cast<UninstallHook>(GetProcAddress(hookModule, "UninstallMercShellHook"));
    if (installHook == nullptr || uninstallHook == nullptr)
    {
        FreeLibrary(hookModule);
        return 3;
    }

    if (!installHook())
    {
        FreeLibrary(hookModule);
        return 4;
    }

    MSG message;
    while (GetMessageW(&message, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    uninstallHook();
    FreeLibrary(hookModule);
    return 0;
}
