#include "cube.h"
#include "encryption.h"

std::string xor_encrypt(const std::string& text, const std::string& key)
{
    std::string out = text;
    for (size_t i = 0; i < text.size(); ++i)
        out[i] = text[i] ^ key[i % key.size()];
    return out;
}

std::string xor_decrypt(const std::string& cipher, const std::string& key)
{
    return xor_encrypt(cipher, key);
}

std::string to_hex(const std::string& input)
{
    static const char* hex = "0123456789ABCDEF";
    std::string out;
    out.reserve(input.size() * 2);

    for (unsigned char c : input)
    {
        out.push_back(hex[c >> 4]);
        out.push_back(hex[c & 0xF]);
    }

    return out;
}