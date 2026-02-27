
#pragma once
#include <string>
#include <string_view>
#include <vector>
#include <cwchar>

class NetUtil
{
public:
    // Wide ? UTF-8 (Windows-safe; noop-ish fallback elsewhere)
#ifdef STANDALONE

    static std::string wutf8(const std::wstring& ws);

    static bool http_post_json_bool(const std::string& host,
        const std::wstring& path,
        const std::string& json,
        long* out_http_status = nullptr,
        std::string* out_body = nullptr);
#endif
};
