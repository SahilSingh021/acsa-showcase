#include "NetUtil.h"
#include "cube.h"

#ifdef STANDALONE
#include <curl/curl.h>
#endif
#ifdef _WIN32
#include <windows.h>
#endif

// wide -> UTF-8
#ifdef STANDALONE
std::string NetUtil::wutf8(const std::wstring& ws)
{
    if (ws.empty()) return {};
    int n = WideCharToMultiByte(CP_UTF8, 0, ws.c_str(), -1, nullptr, 0, nullptr, nullptr);
    if (n <= 1) return {};
    std::string s(n - 1, '\0');
    WideCharToMultiByte(CP_UTF8, 0, ws.c_str(), -1, s.data(), n, nullptr, nullptr);
    return s;
}

bool NetUtil::http_post_json_bool(const std::string& host,
    const std::wstring& path,
    const std::string& json,
    long* out_http_status,
    std::string* out_body)
{
    CURLcode rc = CURLE_OK;
    bool ok = false;
    curl_global_init(CURL_GLOBAL_DEFAULT);

    if (CURL* curl = curl_easy_init()) {
        std::string url = host + wutf8(path);

        struct curl_slist* headers = nullptr;
        headers = curl_slist_append(headers, "Content-Type: application/json");
        // headers = curl_slist_append(headers, "Accept: application/json"); // optional

        std::string response;
        curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
        curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
        curl_easy_setopt(curl, CURLOPT_POSTFIELDS, json.c_str());
        curl_easy_setopt(curl, CURLOPT_TIMEOUT, 10L);
        curl_easy_setopt(curl, CURLOPT_NOSIGNAL, 1L);
        curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION,
            +[](char* ptr, size_t sz, size_t nm, void* ud) -> size_t {
                static_cast<std::string*>(ud)->append(ptr, sz * nm);
                return sz * nm;
            });
        curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response);
        // curl_easy_setopt(curl, CURLOPT_FAILONERROR, 1L); // optional

        rc = curl_easy_perform(curl);

        long http_code = 0;
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &http_code);

        if (out_http_status) *out_http_status = http_code;
        if (out_body) *out_body = response;

        if (rc == CURLE_OK && http_code >= 200 && http_code < 300) {
            // ASP.NET will serialize bool as "true"/"false"
            // Trim whitespace:
            auto first = response.find_first_not_of(" \t\r\n");
            auto last = response.find_last_not_of(" \t\r\n");
            std::string trimmed = (first == std::string::npos) ? "" : response.substr(first, last - first + 1);
            if (trimmed == "true" || trimmed == "True") ok = true;
        }

        curl_slist_free_all(headers);
        curl_easy_cleanup(curl);
    }

    curl_global_cleanup();
    return ok;
}
#endif
