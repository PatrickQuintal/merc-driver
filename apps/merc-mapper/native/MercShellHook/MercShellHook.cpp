#include <windows.h>

extern "C" __declspec(dllexport) BOOL __stdcall UninstallMercShellHook();

namespace
{
    HINSTANCE g_instance = nullptr;
    HHOOK g_shellHook = nullptr;
    HHOOK g_getMessageHook = nullptr;

    int GetAppCommand(LPARAM lParam)
    {
        return static_cast<short>(HIWORD(static_cast<DWORD_PTR>(lParam)));
    }

    bool ShouldSuppressAppCommand(int command)
    {
        switch (command)
        {
        case APPCOMMAND_BROWSER_BACKWARD:
        case APPCOMMAND_BROWSER_FORWARD:
        case APPCOMMAND_BROWSER_REFRESH:
        case APPCOMMAND_BROWSER_STOP:
        case APPCOMMAND_BROWSER_SEARCH:
        case APPCOMMAND_BROWSER_FAVORITES:
        case APPCOMMAND_BROWSER_HOME:
        case APPCOMMAND_VOLUME_MUTE:
        case APPCOMMAND_VOLUME_DOWN:
        case APPCOMMAND_VOLUME_UP:
        case APPCOMMAND_MEDIA_NEXTTRACK:
        case APPCOMMAND_MEDIA_PREVIOUSTRACK:
        case APPCOMMAND_MEDIA_STOP:
        case APPCOMMAND_MEDIA_PLAY_PAUSE:
        case APPCOMMAND_LAUNCH_MAIL:
        case APPCOMMAND_LAUNCH_MEDIA_SELECT:
        case APPCOMMAND_LAUNCH_APP1:
        case APPCOMMAND_LAUNCH_APP2:
            return true;
        default:
            return false;
        }
    }

    LRESULT CALLBACK MercShellProc(int code, WPARAM wParam, LPARAM lParam)
    {
        UNREFERENCED_PARAMETER(wParam);

        if (code == HSHELL_APPCOMMAND && ShouldSuppressAppCommand(GetAppCommand(lParam)))
        {
            return 1;
        }

        return CallNextHookEx(nullptr, code, wParam, lParam);
    }

    LRESULT CALLBACK MercGetMessageProc(int code, WPARAM wParam, LPARAM lParam)
    {
        UNREFERENCED_PARAMETER(wParam);

        if (code >= 0)
        {
            auto message = reinterpret_cast<MSG*>(lParam);
            if (message != nullptr &&
                message->message == WM_APPCOMMAND &&
                ShouldSuppressAppCommand(GetAppCommand(message->lParam)))
            {
                message->message = WM_NULL;
                message->wParam = 0;
                message->lParam = 0;
                return 1;
            }
        }

        return CallNextHookEx(nullptr, code, wParam, lParam);
    }
}

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID reserved)
{
    UNREFERENCED_PARAMETER(reserved);

    if (reason == DLL_PROCESS_ATTACH)
    {
        g_instance = module;
        DisableThreadLibraryCalls(module);
    }

    return TRUE;
}

extern "C" __declspec(dllexport) BOOL __stdcall InstallMercShellHook()
{
    if (g_shellHook != nullptr && g_getMessageHook != nullptr)
    {
        return TRUE;
    }

    if (g_shellHook == nullptr)
    {
        g_shellHook = SetWindowsHookExW(WH_SHELL, MercShellProc, g_instance, 0);
    }

    if (g_getMessageHook == nullptr)
    {
        g_getMessageHook = SetWindowsHookExW(WH_GETMESSAGE, MercGetMessageProc, g_instance, 0);
    }

    if (g_shellHook != nullptr && g_getMessageHook != nullptr)
    {
        return TRUE;
    }

    UninstallMercShellHook();
    return FALSE;
}

extern "C" __declspec(dllexport) BOOL __stdcall UninstallMercShellHook()
{
    BOOL result = TRUE;

    if (g_shellHook != nullptr)
    {
        const BOOL shellResult = UnhookWindowsHookEx(g_shellHook);
        if (shellResult)
        {
            g_shellHook = nullptr;
        }

        result = shellResult && result;
    }

    if (g_getMessageHook != nullptr)
    {
        const BOOL getMessageResult = UnhookWindowsHookEx(g_getMessageHook);
        if (getMessageResult)
        {
            g_getMessageHook = nullptr;
        }

        result = getMessageResult && result;
    }

    return result;
}
