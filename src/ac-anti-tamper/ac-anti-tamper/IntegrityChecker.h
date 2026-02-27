#pragma once
#include <windows.h>
#include <cstdint>

class IntegrityChecker
{
public:
    IntegrityChecker() = default;

    uint32_t ComputeCRC32(const uint8_t* data, size_t len);
    bool ComputeTextSectionCRC(HMODULE module, uint32_t& outCRC);
    void ScanIntegrity(uint32_t baselineCRC);
};
