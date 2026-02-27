#define WIN32_LEAN_AND_MEAN
#include <winsock2.h>
#include <ws2tcpip.h>
#include <windows.h>
#include <curl/curl.h>
#include <string>
#include <sstream>
#include <thread>
#include "httplib.h"
#include "LogWindow.h"

#pragma comment(lib, "Ws2_32.lib")

std::string get_hwid()
{
    HW_PROFILE_INFOW hwpi{};
    if (!GetCurrentHwProfileW(&hwpi) || !hwpi.szHwProfileGuid[0]) return "unknown";

    std::wstring ws(hwpi.szHwProfileGuid);
    if (!ws.empty() && ws.front() == L'{' && ws.back() == L'}')
        ws = ws.substr(1, ws.size() - 2);

    int len = WideCharToMultiByte(CP_UTF8, 0, ws.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (len <= 0) return "unknown";

    std::string out(len - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, ws.c_str(), -1, &out[0], len, nullptr, nullptr);
    return out;
}

std::string get_public_ipv4()
{
    std::string ip = "127.0.0.1";

    curl_global_init(CURL_GLOBAL_DEFAULT);
    if (CURL* curl = curl_easy_init())
    {
        std::string response;

        curl_easy_setopt(curl, CURLOPT_URL, "https://api.ipify.org");
        curl_easy_setopt(curl, CURLOPT_TIMEOUT, 5L);
        curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION,
            +[](char* ptr, size_t size, size_t nmemb, void* userdata) -> size_t {
                std::string* out = static_cast<std::string*>(userdata);
                out->append(ptr, size * nmemb);
                return size * nmemb;
            });
        curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);

        CURLcode res = curl_easy_perform(curl);
        if (res == CURLE_OK && !response.empty())
            ip = response;

        curl_easy_cleanup(curl);
    }
    curl_global_cleanup();

    return ip;
}

typedef void(__cdecl* AC_ConnectToServer)(char*, int*, char*);

bool CallGameConnect(char* servername, int* serverport, char* password)
{
    HMODULE hGame = GetModuleHandleW(nullptr);
    if (!hGame) return false;

    auto fn = (AC_ConnectToServer)GetProcAddress(hGame, "AC_ConnectToServer");
    if (!fn) return false;

    fn(servername, serverport, password);
    return true;
}

void HandleWebCommand(const std::string& body)
{
    std::string cmd, ip, pass;
    int port = 0;

    auto getString = [&](const std::string& key) {
        std::string pattern = "\"" + key + "\"";
        size_t pos = body.find(pattern);
        if (pos == std::string::npos) return std::string();
        pos = body.find('"', pos + pattern.size());
        if (pos == std::string::npos) return std::string();
        size_t end = body.find('"', pos + 1);
        if (end == std::string::npos) return std::string();
        return body.substr(pos + 1, end - pos - 1);
        };

    auto getInt = [&](const std::string& key) {
        std::string pattern = "\"" + key + "\"";
        size_t pos = body.find(pattern);
        if (pos == std::string::npos) return 0;
        pos = body.find(':', pos);
        if (pos == std::string::npos) return 0;
        return std::atoi(body.c_str() + pos + 1);
        };

    cmd = getString("command");
    ip = getString("ip");
    port = getInt("port");
    pass = getString("password");

    if (cmd == "StartMatch")
    {
        CallGameConnect((char*)ip.c_str(), &port, (char*)pass.c_str());
        g_logWindow->Log("[AntiTemper] Loading into a match!");
    }
    else
    {
        g_logWindow->Log("[AntiTemper] Unknown or missing command!");
    }
}