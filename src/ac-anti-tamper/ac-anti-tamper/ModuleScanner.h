#pragma once
#include <string>
#include <vector>

class ModuleScanner
{
public:
    ModuleScanner();
    void ScanModules(const std::string& gameFolder);
    std::string GetModuleFile(HMODULE hMod);

private:
    bool IsSystemModule(const std::string& path);

private:
    std::vector<std::string> m_whitelist;
    std::vector<std::string> m_suspiciousModules;
};
