#include "AntiTemper.h"
#include "IntegrityChecker.h"
#include "LogWindow.h"
#include "AuthPoll.h"

uint32_t IntegrityChecker::ComputeCRC32(const uint8_t* data, size_t len)
{
    static uint32_t table[256];
    static bool inited = false;
    if (!inited) {
        for (uint32_t i = 0; i < 256; ++i) {
            uint32_t c = i;
            for (size_t j = 0; j < 8; ++j)
                c = c & 1 ? 0xEDB88320UL ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        inited = true;
    }

    uint32_t crc = 0xFFFFFFFFu;
    for (size_t i = 0; i < len; ++i)
        crc = table[(crc ^ data[i]) & 0xFFu] ^ (crc >> 8);
    return crc ^ 0xFFFFFFFFu;
}

bool IntegrityChecker::ComputeTextSectionCRC(HMODULE module, uint32_t& outCRC)
{
    uint8_t* base = reinterpret_cast<uint8_t*>(module);
    if (!base) return false;

    auto dos = reinterpret_cast<IMAGE_DOS_HEADER*>(base);
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) return false;
    auto nt = reinterpret_cast<IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
    if (nt->Signature != IMAGE_NT_SIGNATURE) return false;

    auto sections = IMAGE_FIRST_SECTION(nt);
    for (unsigned i = 0; i < nt->FileHeader.NumberOfSections; ++i) {
        auto& s = sections[i];
        if (strncmp((char*)s.Name, ".text", 5) == 0) {
            outCRC = ComputeCRC32(base + s.VirtualAddress, s.Misc.VirtualSize);
            return true;
        }
    }
    return false;
}

void IntegrityChecker::ScanIntegrity(uint32_t baselineCRC)
{
    static bool sentMismatch = false;

    uint32_t currCRC = 0;
    if (!ComputeTextSectionCRC(GetModuleHandleA(NULL), currCRC)) {
        g_logWindow->Log("[AntiTemper] Failed to compute .text CRC.");
        return;
    }

    if (currCRC != baselineCRC) {
        if (!sentMismatch)
        {
            std::ostringstream ss;
            ss << "[AntiTemper] .text CRC mismatch! baseline=0x"
                << std::hex << baselineCRC << " current=0x" << currCRC;
            g_logWindow->Log(ss.str());
            ACSA_SendUserLog(2, ss.str());
            sentMismatch = true;
        }
    }
    else
    {
        sentMismatch = false;
    }
}
