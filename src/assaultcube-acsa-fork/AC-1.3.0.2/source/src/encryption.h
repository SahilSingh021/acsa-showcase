#pragma once
#include <string>

std::string xor_encrypt(const std::string& text, const std::string& key);
std::string xor_decrypt(const std::string& cipher, const std::string& key);
std::string to_hex(const std::string& input);
