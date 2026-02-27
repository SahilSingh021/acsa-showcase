#pragma once
#include <Windows.h>
#include <iostream>

DWORD WINAPI AuthAndPollThread(LPVOID);
std::string GetCurrentJwt();
bool ACSA_SendUserLog(int level, const std::string& message);
