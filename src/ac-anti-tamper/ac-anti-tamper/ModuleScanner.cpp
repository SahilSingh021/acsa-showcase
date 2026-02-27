#include "AntiTemper.h"
#include "ModuleScanner.h"
#include "LogWindow.h"
#include "AuthPoll.h"

ModuleScanner::ModuleScanner()
{
    m_whitelist = { "mdnsNSP.dll", "medal-hook32.dll" };
}

bool ModuleScanner::IsSystemModule(const std::string& path)
{
    char windowsDir[MAX_PATH];
    GetWindowsDirectoryA(windowsDir, MAX_PATH);
    std::string winDir(windowsDir);

    if (path.size() < winDir.size()) return false;
    return _stricmp(path.substr(0, winDir.size()).c_str(), winDir.c_str()) == 0;
}

std::string ModuleScanner::GetModuleFile(HMODULE hMod)
{
    char buf[MAX_PATH];
    DWORD r = GetModuleFileNameA(hMod, buf, MAX_PATH);
    return (r == 0) ? std::string() : std::string(buf);
}
void ModuleScanner::ScanModules(const std::string& gameFolder)
{
    if (gameFolder.empty()) return;

    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, GetCurrentProcessId());
    if (snap == INVALID_HANDLE_VALUE) return;

    MODULEENTRY32 me{ sizeof(me) };
    if (Module32First(snap, &me)) {
        do {
            std::string modPath = me.szExePath;

            bool whitelisted = std::any_of(
                m_whitelist.begin(), m_whitelist.end(),
                [&](const std::string& name) { return _stricmp(me.szModule, name.c_str()) == 0; });

            if (whitelisted || IsSystemModule(modPath)) continue;

            // Check if module is outside the game folder
            if (_strnicmp(modPath.c_str(), gameFolder.c_str(), gameFolder.size()) != 0) {

                // Only log if not already in the suspicious list
                if (std::find(m_suspiciousModules.begin(), m_suspiciousModules.end(), me.szModule) == m_suspiciousModules.end()) {
                    m_suspiciousModules.push_back(me.szModule); // Add to list

                    std::ostringstream ss;
                    ss << "[AntiTemper] Suspicious module outside game folder: " << me.szModule;
                    g_logWindow->Log(ss.str());
                    ACSA_SendUserLog(2, ss.str());
                }
            }

        } while (Module32Next(snap, &me));
    }
    CloseHandle(snap);
}