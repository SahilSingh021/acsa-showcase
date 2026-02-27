#include "AntiTemper.h"
#include "LogWindow.h"
#include "IntegrityChecker.h"
#include "ModuleScanner.h"
#include "AntiDebugger.h"
#include <iomanip>
#include "AuthPoll.h"

extern "C" __declspec(dllexport) bool ac_anti_tamper_heartbeat()
{
    std::time_t now = std::time(nullptr);
    std::tm localTime;
    localtime_s(&localTime, &now);

    std::ostringstream oss;
    oss << "[AntiTemper] Heartbeat, [";
    oss << std::put_time(&localTime, "%Y-%m-%d %H:%M:%S");
    oss << "]!";

    g_logWindow->Log(oss.str().c_str());

    return true;
}

DWORD WINAPI MonitorThread(LPVOID lpParam)
{
    Sleep(5000);
    g_logWindow->Clear();
    g_logWindow->Log("[AntiTemper] Monitor thread started.");

    IntegrityChecker integrity;
    ModuleScanner scanner;
    AntiDebugger debugger;

    uint32_t baselineCRC = 0;
    if (!integrity.ComputeTextSectionCRC(GetModuleHandleA(NULL), baselineCRC))
    {
        g_logWindow->Log("[AntiTemper] Could not compute baseline .text CRC.");
        Sleep(5000);
        ExitProcess(0);
    }
#ifndef _DEBUG
    /*else if (baselineCRC != 0x53499133) {
        g_logWindow->Log("[AntiTemper] Baseline .text CRC mismatch!");
        ACSA_SendUserLog(2, "[AntiTemper] Baseline .text CRC mismatch!");
        Sleep(5000);
        ExitProcess(0);
    }*/
#endif
    else
    {
        std::ostringstream ss;
        ss << "[AntiTemper] Baseline .text CRC (0x" << std::hex << baselineCRC << ")";
        g_logWindow->Log(ss.str());
        Sleep(1000);
    }

    std::string gameFolder = scanner.GetModuleFile(GetModuleHandleA(NULL));
    gameFolder = gameFolder.substr(0, gameFolder.find_last_of("\\/"));

    g_logWindow->Log("[AntiTemper] Assault Cube Anti-Temper started.");

    while (true) {
        debugger.CheckDebugger();
        scanner.ScanModules(gameFolder);
        if (baselineCRC != 0) integrity.ScanIntegrity(baselineCRC);

        Sleep(5000);
    }

    return 0;
}
