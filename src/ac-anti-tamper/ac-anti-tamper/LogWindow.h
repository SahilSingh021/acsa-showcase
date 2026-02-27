#pragma once
#include <windows.h>
#include <string>

class LogWindow
{
public:
    LogWindow();
    ~LogWindow();

    void Create();
    void Clear();
    void Log(const std::string& msg);

    static LRESULT CALLBACK LogWndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
    static DWORD WINAPI LogThread(LPVOID lpParam);

private:
    HWND m_hWndMain;
    HWND m_hWndLog;
};

extern LogWindow* g_logWindow;
