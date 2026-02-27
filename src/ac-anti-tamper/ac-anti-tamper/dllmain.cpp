#include "AntiTemper.h"
#include "LogWindow.h"
#include "Monitor.h"
#include "AuthPoll.h"

BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpReserved)
{
    static LogWindow logWindow;
    HANDLE hLog, hMon, hPoll;

    switch (fdwReason)
    {
    case DLL_PROCESS_ATTACH:
        g_logWindow = &logWindow;

#ifdef _DEBUG
        hLog = CreateThread(NULL, 0, LogWindow::LogThread, NULL, 0, NULL);
        if (hLog) CloseHandle(hLog);
#endif

        hMon = CreateThread(NULL, 0, MonitorThread, NULL, 0, NULL);
        if (hMon) CloseHandle(hMon);

        hPoll = CreateThread(NULL, 0, AuthAndPollThread, NULL, 0, NULL);
        if (hPoll) CloseHandle(hPoll);
        break;

    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
