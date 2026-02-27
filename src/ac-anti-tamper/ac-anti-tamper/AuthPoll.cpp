#include "AuthPoll.h"
#include "LogWindow.h"

#include <Windows.h>
#include <ShlObj.h>
#include <wincrypt.h>
#pragma comment(lib, "Crypt32.lib")

#include <string>
#include <vector>
#include <thread>
#include <chrono>
#include <mutex>

#include <curl/curl.h>

// globals
static std::mutex g_authMutex;
static std::string g_currentJwt;
static std::string g_currentUid;

static void SetCurrentAuth(const std::string& jwt, const std::string& uid)
{
    std::lock_guard<std::mutex> lk(g_authMutex);
    g_currentJwt = jwt;
    g_currentUid = uid;
}

std::string GetCurrentJwt()
{
    std::lock_guard<std::mutex> lk(g_authMutex);
    return g_currentJwt;
}

std::string GetCurrentPersistentUid()
{
    std::lock_guard<std::mutex> lk(g_authMutex);
    return g_currentUid;
}

extern "C" __declspec(dllexport) const char* ACSA_GetPersistentUid()
{
    static thread_local std::string s;
    s = GetCurrentPersistentUid();
    return s.c_str();
}

// forward declare existing functions
void HandleWebCommand(const std::string& body);
std::string get_hwid();
std::string get_public_ipv4();

static void AuthFailExit(const wchar_t* msg)
{
    MessageBoxW(NULL, msg, L"AC Secure Arena - AntiCheat", MB_ICONERROR | MB_OK);
    Sleep(5000);
    ExitProcess(0);
}

// file path
static std::wstring GetTokenPath()
{
    PWSTR appData = nullptr;
    if (FAILED(SHGetKnownFolderPath(FOLDERID_RoamingAppData, 0, NULL, &appData)) || !appData)
        return L"";

    std::wstring dir = std::wstring(appData) + L"\\ACSecureArena";
    CoTaskMemFree(appData);

    CreateDirectoryW(dir.c_str(), nullptr);
    return dir + L"\\token.bin";
}

static std::string JsonEscape(const std::string& s);
static bool ExtractJsonString(const std::string& json, const char* key, std::string& out);

struct AuthBundle
{
    std::string jwt;
    std::string uid;
};

static std::string MakeBundleJson(const AuthBundle& b)
{
    return std::string("{\"token\":\"") + JsonEscape(b.jwt) +
        "\",\"persistentUid\":\"" + JsonEscape(b.uid) + "\"}";
}

static bool ParseBundleJson(const std::string& json, AuthBundle& out)
{
    out = {};
    if (!ExtractJsonString(json, "token", out.jwt)) return false;
    ExtractJsonString(json, "persistentUid", out.uid);
    return true;
}

static bool SaveBundleDPAPI(const AuthBundle& b)
{
    auto path = GetTokenPath();
    if (path.empty()) return false;

    const std::string plain = MakeBundleJson(b);

    DATA_BLOB inBlob{};
    inBlob.pbData = (BYTE*)plain.data();
    inBlob.cbData = (DWORD)plain.size();

    DATA_BLOB outBlob{};
    if (!CryptProtectData(&inBlob, L"ACSA AUTH", nullptr, nullptr, nullptr, 0, &outBlob))
        return false;

    HANDLE h = CreateFileW(path.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE)
    {
        LocalFree(outBlob.pbData);
        return false;
    }

    DWORD written = 0;
    BOOL ok = WriteFile(h, outBlob.pbData, outBlob.cbData, &written, nullptr);
    CloseHandle(h);
    LocalFree(outBlob.pbData);

    return ok && written == outBlob.cbData;
}

static bool LoadBundleDPAPI(AuthBundle& out)
{
    out = {};

    auto path = GetTokenPath();
    if (path.empty()) return false;

    HANDLE h = CreateFileW(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE)
        return false;

    DWORD size = GetFileSize(h, nullptr);
    if (size == INVALID_FILE_SIZE || size == 0)
    {
        CloseHandle(h);
        return false;
    }

    std::vector<BYTE> enc(size);
    DWORD read = 0;
    if (!ReadFile(h, enc.data(), size, &read, nullptr) || read != size)
    {
        CloseHandle(h);
        return false;
    }
    CloseHandle(h);

    DATA_BLOB inBlob{};
    inBlob.pbData = enc.data();
    inBlob.cbData = size;

    DATA_BLOB outBlob{};
    if (!CryptUnprotectData(&inBlob, nullptr, nullptr, nullptr, nullptr, 0, &outBlob))
        return false;

    std::string plain((char*)outBlob.pbData, (char*)outBlob.pbData + outBlob.cbData);
    LocalFree(outBlob.pbData);

    return ParseBundleJson(plain, out) && !out.jwt.empty();
}

// jwt expiry check
static bool Base64UrlDecode(const std::string& in, std::string& out)
{
    out.clear();
    std::string b64 = in;
    for (char& c : b64) { if (c == '-') c = '+'; else if (c == '_') c = '/'; }
    while (b64.size() % 4 != 0) b64.push_back('=');

    DWORD needed = 0;
    if (!CryptStringToBinaryA(b64.c_str(), 0, CRYPT_STRING_BASE64, nullptr, &needed, nullptr, nullptr))
        return false;

    std::string buf;
    buf.resize(needed);

    if (!CryptStringToBinaryA(b64.c_str(), 0, CRYPT_STRING_BASE64, (BYTE*)buf.data(), &needed, nullptr, nullptr))
        return false;

    buf.resize(needed);
    out = std::move(buf);
    return true;
}

static bool IsJwtExpired(const std::string& jwt)
{
    size_t p1 = jwt.find('.');
    if (p1 == std::string::npos) return true;
    size_t p2 = jwt.find('.', p1 + 1);
    if (p2 == std::string::npos) return true;

    std::string payloadB64 = jwt.substr(p1 + 1, p2 - (p1 + 1));
    std::string payloadJson;
    if (!Base64UrlDecode(payloadB64, payloadJson)) return true;

    // find expiry
    auto pos = payloadJson.find("\"exp\"");
    if (pos == std::string::npos) return true;
    pos = payloadJson.find(':', pos);
    if (pos == std::string::npos) return true;
    pos++;

    while (pos < payloadJson.size() && payloadJson[pos] == ' ') pos++;

    long long exp = 0;
    while (pos < payloadJson.size() && payloadJson[pos] >= '0' && payloadJson[pos] <= '9')
    {
        exp = exp * 10 + (payloadJson[pos] - '0');
        pos++;
    }
    if (exp <= 0) return true;

    FILETIME ft;
    GetSystemTimeAsFileTime(&ft);
    ULARGE_INTEGER ui;
    ui.LowPart = ft.dwLowDateTime;
    ui.HighPart = ft.dwHighDateTime;

    const unsigned long long EPOCH_DIFF = 11644473600ULL;
    unsigned long long seconds = (ui.QuadPart / 10000000ULL) - EPOCH_DIFF;

    // expire a bit early
    return seconds + 30 >= (unsigned long long)exp;
}

// libcurl helpers
static size_t write_cb(char* ptr, size_t size, size_t nmemb, void* userdata)
{
    auto* out = static_cast<std::string*>(userdata);
    out->append(ptr, size * nmemb);
    return size * nmemb;
}

static bool HttpGetBearer(const std::string& url, const std::string& jwt, long& status, std::string& body)
{
    body.clear();
    status = 0;

    CURL* curl = curl_easy_init();
    if (!curl) return false;

    struct curl_slist* headers = nullptr;
    headers = curl_slist_append(headers, ("Authorization: Bearer " + jwt).c_str());
    headers = curl_slist_append(headers, "Accept: application/json");

    curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
    curl_easy_setopt(curl, CURLOPT_TIMEOUT, 30L);
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &body);

    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 1L);
    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 2L);

    CURLcode res = curl_easy_perform(curl);
    if (res == CURLE_OK)
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &status);

    curl_slist_free_all(headers);
    curl_easy_cleanup(curl);
    return res == CURLE_OK;
}

static bool HttpPostBearerJson(
    const std::string& url,
    const std::string& jwt,
    const std::string& jsonBody,
    long& status,
    std::string& responseBody)
{
    responseBody.clear();
    status = 0;

    CURL* curl = curl_easy_init();
    if (!curl) return false;

    struct curl_slist* headers = nullptr;
    headers = curl_slist_append(headers, ("Authorization: Bearer " + jwt).c_str());
    headers = curl_slist_append(headers, "Content-Type: application/json");
    headers = curl_slist_append(headers, "Accept: application/json");

    curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
    curl_easy_setopt(curl, CURLOPT_POST, 1L);
    curl_easy_setopt(curl, CURLOPT_POSTFIELDS, jsonBody.c_str());
    curl_easy_setopt(curl, CURLOPT_TIMEOUT, 10L);

    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &responseBody);

    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 1L);
    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 2L);

    CURLcode res = curl_easy_perform(curl);
    if (res == CURLE_OK)
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &status);

    curl_slist_free_all(headers);
    curl_easy_cleanup(curl);
    return res == CURLE_OK;
}

// Send User Log to web app
static bool SendUserLog(
    const std::string& jwt,
    int level,
    const std::string& message)
{
    if (jwt.empty() || message.empty())
        return false;

    std::string body =
        std::string("{\"message\":\"") + JsonEscape(message) +
        "\",\"level\":" + std::to_string(level) +
        "}";

    long status = 0;
    std::string resp;

    bool ok = HttpPostBearerJson(
        "https://ac-secure-arena.com/api/ac/saveuserlog",
        jwt,
        body,
        status,
        resp);

    return ok && (status == 200 || status == 204);
}

bool ACSA_SendUserLog(int level, const std::string& message)
{
    const std::string jwt = GetCurrentJwt();
    return SendUserLog(jwt, level, message);
}

// json helpers
static std::string JsonEscape(const std::string& s)
{
    std::string o;
    o.reserve(s.size() + 8);
    for (char c : s)
    {
        switch (c)
        {
        case '\\': o += "\\\\"; break;
        case '"':  o += "\\\""; break;
        case '\n': o += "\\n";  break;
        case '\r': o += "\\r";  break;
        case '\t': o += "\\t";  break;
        default:   o += c;      break;
        }
    }
    return o;
}

static bool ExtractJsonString(const std::string& json, const char* key, std::string& out)
{
    out.clear();
    std::string k = std::string("\"") + key + "\"";
    size_t p = json.find(k);
    if (p == std::string::npos) return false;
    p = json.find(':', p);
    if (p == std::string::npos) return false;
    p = json.find('"', p);
    if (p == std::string::npos) return false;
    size_t e = json.find('"', p + 1);
    if (e == std::string::npos) return false;
    out = json.substr(p + 1, e - (p + 1));
    return !out.empty();
}

static bool ExtractLoginTokenAndUid(const std::string& json, std::string& tokenOut, std::string& uidOut)
{
    tokenOut.clear();
    uidOut.clear();

    if (!ExtractJsonString(json, "token", tokenOut)) return false;
    ExtractJsonString(json, "persistentUid", uidOut);
    return true;
}

static bool HttpPostLogin(
    const std::string& email,
    const std::string& pass,
    long& status,
    std::string& responseBody)
{
    responseBody.clear();
    status = 0;

    CURL* curl = curl_easy_init();
    if (!curl) return false;

    const std::string hwid = get_hwid();
    const std::string ip = get_public_ipv4();

    if (ip.empty() || ip == "127.0.0.1")
    {
        curl_easy_cleanup(curl);
        return false;
    }

    std::string body =
        std::string("{\"email\":\"") + JsonEscape(email) +
        "\",\"password\":\"" + JsonEscape(pass) +
        "\",\"hardwareId\":\"" + JsonEscape(hwid) +
        "\",\"ip\":\"" + JsonEscape(ip) + "\"}";

    struct curl_slist* headers = nullptr;
    headers = curl_slist_append(headers, "Content-Type: application/json");
    headers = curl_slist_append(headers, "Accept: application/json");

    curl_easy_setopt(curl, CURLOPT_URL, "https://ac-secure-arena.com/api/auth/login");
    curl_easy_setopt(curl, CURLOPT_POST, 1L);
    curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
    curl_easy_setopt(curl, CURLOPT_POSTFIELDS, body.c_str());
    curl_easy_setopt(curl, CURLOPT_TIMEOUT, 20L);
    curl_easy_setopt(curl, CURLOPT_NOSIGNAL, 1L);

    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &responseBody);

    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYPEER, 1L);
    curl_easy_setopt(curl, CURLOPT_SSL_VERIFYHOST, 2L);

    CURLcode res = curl_easy_perform(curl);
    if (res == CURLE_OK)
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &status);

    curl_slist_free_all(headers);
    curl_easy_cleanup(curl);

    return res == CURLE_OK;
}

// Win32 login popup
static HWND g_hEmail = nullptr;
static HWND g_hPass = nullptr;
static HWND g_hStatus = nullptr;
static HWND g_hLoginBtn = nullptr;

static bool g_loginDone = false;
static bool g_loginOk = false;
static std::string g_loginJwt;
static std::string g_loginUid;

struct LoginResult
{
    bool ok = false;
    std::string jwt;
    std::string uid;
    std::string error;
};

static std::string GetEditTextUtf8(HWND hEdit)
{
    int len = GetWindowTextLengthW(hEdit);
    std::wstring w; w.resize(len);
    GetWindowTextW(hEdit, w.data(), len + 1);

    int bytes = WideCharToMultiByte(CP_UTF8, 0, w.c_str(), len, nullptr, 0, nullptr, nullptr);
    std::string s; s.resize(bytes);
    WideCharToMultiByte(CP_UTF8, 0, w.c_str(), len, s.data(), bytes, nullptr, nullptr);
    return s;
}

static void SetStatusText(const wchar_t* text)
{
    if (g_hStatus) SetWindowTextW(g_hStatus, text);
}

static void SetStatusTextUtf8(const std::string& s)
{
    int n = MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), nullptr, 0);
    std::wstring w; w.resize(n);
    MultiByteToWideChar(CP_UTF8, 0, s.c_str(), (int)s.size(), w.data(), n);
    SetStatusText(w.c_str());
}

static LRESULT CALLBACK LoginWndProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_CREATE:
    {
        HFONT hFont = (HFONT)GetStockObject(DEFAULT_GUI_FONT);

        CreateWindowW(L"STATIC", L"Email:", WS_CHILD | WS_VISIBLE,
            16, 18, 70, 18, hwnd, nullptr, nullptr, nullptr);

        CreateWindowW(L"STATIC", L"Password:", WS_CHILD | WS_VISIBLE,
            16, 58, 70, 18, hwnd, nullptr, nullptr, nullptr);

        g_hEmail = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
            WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL,
            95, 14, 260, 24, hwnd, (HMENU)1001, nullptr, nullptr);

        g_hPass = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
            WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL | ES_PASSWORD,
            95, 54, 260, 24, hwnd, (HMENU)1002, nullptr, nullptr);

        g_hLoginBtn = CreateWindowW(L"BUTTON", L"Login",
            WS_CHILD | WS_VISIBLE | BS_DEFPUSHBUTTON,
            95, 92, 120, 28, hwnd, (HMENU)1003, nullptr, nullptr);

        CreateWindowW(L"BUTTON", L"Cancel",
            WS_CHILD | WS_VISIBLE,
            235, 92, 120, 28, hwnd, (HMENU)1004, nullptr, nullptr);

        g_hStatus = CreateWindowW(L"STATIC", L"",
            WS_CHILD | WS_VISIBLE,
            16, 132, 340, 20, hwnd, nullptr, nullptr, nullptr);

        SendMessageW(g_hEmail, WM_SETFONT, (WPARAM)hFont, TRUE);
        SendMessageW(g_hPass, WM_SETFONT, (WPARAM)hFont, TRUE);
        SendMessageW(g_hLoginBtn, WM_SETFONT, (WPARAM)hFont, TRUE);
        SendMessageW(g_hStatus, WM_SETFONT, (WPARAM)hFont, TRUE);

        SetFocus(g_hEmail);
        return 0;
    }

    case WM_COMMAND:
    {
        int id = LOWORD(wParam);

        if (id == 1004) // Cancel
        {
            g_loginDone = true;
            g_loginOk = false;
            DestroyWindow(hwnd);
            return 0;
        }

        if (id == 1003) // Login
        {
            EnableWindow(g_hLoginBtn, FALSE);
            SetStatusText(L"Logging in...");

            std::string email = GetEditTextUtf8(g_hEmail);
            std::string pass = GetEditTextUtf8(g_hPass);

            if (email.empty() || pass.empty())
            {
                SetStatusText(L"Email and password required.");
                EnableWindow(g_hLoginBtn, TRUE);
                return 0;
            }

            std::thread([hwnd, email, pass]() {
                long status = 0;
                std::string resp;

                bool okCurl = HttpPostLogin(email, pass, status, resp);

                auto* r = new LoginResult();

                if (!okCurl)
                {
                    r->ok = false;
                    r->error = "Network error (curl)";
                }
                else if (status != 200)
                {
                    r->ok = false;
                    r->error = "Login failed (HTTP " + std::to_string(status) + ")";
                }
                else
                {
                    std::string jwt, uid;
                    if (!ExtractLoginTokenAndUid(resp, jwt, uid))
                    {
                        r->ok = false;
                        r->error = "Could not parse token/uid.";
                    }
                    else
                    {
                        r->ok = true;
                        r->jwt = std::move(jwt);
                        r->uid = std::move(uid);
                    }
                }

                PostMessageW(hwnd, WM_APP + 1, 0, (LPARAM)r);
                }).detach();

            return 0;
        }

        return 0;
    }

    case WM_APP + 1:
    {
        auto* r = (LoginResult*)lParam;

        if (r->ok)
        {
            g_loginJwt = r->jwt;
            g_loginUid = r->uid;
            g_loginOk = true;
            g_loginDone = true;
            delete r;
            DestroyWindow(hwnd);
        }
        else
        {
            SetStatusTextUtf8(r->error);
            EnableWindow(g_hLoginBtn, TRUE);
            delete r;
        }
        return 0;
    }

    case WM_CLOSE:
        g_loginDone = true;
        g_loginOk = false;
        DestroyWindow(hwnd);
        return 0;

    case WM_DESTROY:
        PostQuitMessage(0);
        return 0;
    }

    return DefWindowProcW(hwnd, msg, wParam, lParam);
}

static bool ShowLoginPopupAndGetToken(std::string& outJwt, std::string& outUid)
{
    outJwt.clear();
    outUid.clear();
    g_loginJwt.clear();
    g_loginUid.clear();
    g_loginDone = false;
    g_loginOk = false;

    HINSTANCE hInst = GetModuleHandleW(nullptr);

    WNDCLASSW wc{};
    wc.lpfnWndProc = LoginWndProc;
    wc.hInstance = hInst;
    wc.lpszClassName = L"ACSA_LoginWnd";
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW);

    RegisterClassW(&wc);

    HWND hwnd = CreateWindowExW(
        WS_EX_TOPMOST,
        wc.lpszClassName,
        L"AC Secure Arena - Login (90 seconds)",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU,
        CW_USEDEFAULT, CW_USEDEFAULT,
        400, 220,
        nullptr, nullptr, hInst, nullptr);

    if (!hwnd) return false;

    ShowWindow(hwnd, SW_SHOW);
    UpdateWindow(hwnd);

    MSG msg{};
    while (GetMessageW(&msg, nullptr, 0, 0) > 0)
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
        if (g_loginDone) break;
    }

    if (g_loginOk && !g_loginJwt.empty())
    {
        outJwt = g_loginJwt;
        outUid = g_loginUid;
        return true;
    }
    return false;
}

// restart game and anti cheat
static void RestartSelf()
{
    wchar_t exePath[MAX_PATH]{};
    if (!GetModuleFileNameW(nullptr, exePath, MAX_PATH))
        ExitProcess(0);

    std::wstring cmd = GetCommandLineW() ? GetCommandLineW() : L"";

    std::vector<wchar_t> cmdBuf(cmd.begin(), cmd.end());
    cmdBuf.push_back(L'\0');

    STARTUPINFOW si{};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi{};

    BOOL ok = CreateProcessW(
        exePath,
        cmdBuf.data(),
        nullptr, nullptr,
        FALSE,
        0,
        nullptr,
        nullptr,
        &si,
        &pi);

    if (ok)
    {
        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
    }

    ExitProcess(0);
}

static bool HttpPostBearerEmptyJson(
    const std::string& url,
    const std::string& jwt,
    long& status,
    std::string& responseBody)
{
    return HttpPostBearerJson(url, jwt, "{}", status, responseBody);
}

static void ClearServerQueueOnConnect(const std::string& jwt)
{
    long st = 0;
    std::string resp;

    bool ok = HttpPostBearerEmptyJson(
        "https://ac-secure-arena.com/api/ac/clear",
        jwt,
        st,
        resp
    );

    if (ok && st == 200)
        g_logWindow->Log("[AntiTemper] Cleared queued commands on connect.");
    else
        g_logWindow->Log("[AntiTemper] Warning: could not clear queue on connect.");
}

// thread function
DWORD WINAPI AuthAndPollThread(LPVOID)
{
    Sleep(5000);
    g_logWindow->Log("[AntiTemper] Authentication and Polling thread started.");

    curl_global_init(CURL_GLOBAL_DEFAULT);

    AuthBundle bundle;

    // load bundle or login
    if (!LoadBundleDPAPI(bundle) || IsJwtExpired(bundle.jwt))
    {
        g_logWindow->Log("[AntiTemper] No valid token saved. Showing login UI...");

        std::string newJwt, newUid;
        if (!ShowLoginPopupAndGetToken(newJwt, newUid))
        {
            g_logWindow->Log("[AntiTemper] Login cancelled/failed.");
            AuthFailExit(L"AntiCheat login failed.\n\nYou must log in to play.\n\nThe game will now close.");
            return 0;
        }

        bundle.jwt = std::move(newJwt);
        bundle.uid = std::move(newUid);

        if (!SaveBundleDPAPI(bundle))
        {
            g_logWindow->Log("[AntiTemper] Failed to save token bundle.");
            AuthFailExit(L"AntiCheat could not save authentication token.\n\nThe game will now close.");
            return 0;
        }

        SetCurrentAuth(bundle.jwt, bundle.uid);
        g_logWindow->Log("[AntiTemper] Login OK. Token+UID saved. Restarting...");
        RestartSelf();
    }
    else
    {
        g_logWindow->Log("[AntiTemper] Token bundle loaded.");
        SetCurrentAuth(bundle.jwt, bundle.uid);
    }

    std::string jwt = bundle.jwt;
    ClearServerQueueOnConnect(jwt);

    g_logWindow->Log("[AntiTemper] Starting command polling...");

    const std::string url = "https://ac-secure-arena.com/api/ac/next";

    while (true)
    {
        long status = 0;
        std::string body;

        bool ok = HttpGetBearer(url, jwt, status, body);

        if (ok && status == 200 && !body.empty())
        {
            HandleWebCommand(body);
        }
        else if (ok && status == 401)
        {
            g_logWindow->Log("[AntiTemper] Token rejected (401). Re-login required.");

            std::string newJwt, newUid;
            if (!ShowLoginPopupAndGetToken(newJwt, newUid))
            {
                AuthFailExit(L"Session expired and re-login was cancelled.\n\nThe game will now close.");
                return 0;
            }

            bundle.jwt = std::move(newJwt);
            bundle.uid = std::move(newUid);

            if (!SaveBundleDPAPI(bundle))
            {
                AuthFailExit(L"Could not save new session token.\n\nThe game will now close.");
                return 0;
            }

            SetCurrentAuth(bundle.jwt, bundle.uid);
            ClearServerQueueOnConnect(bundle.jwt);
            g_logWindow->Log("[AntiTemper] Re-login OK. Token+UID saved. Restarting...");
            RestartSelf();
        }

        std::this_thread::sleep_for(std::chrono::milliseconds(250));
    }

    curl_global_cleanup();
    return 0;
}
