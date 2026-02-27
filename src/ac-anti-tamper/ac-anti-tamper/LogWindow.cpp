#include "AntiTemper.h"
#include "LogWindow.h"
#include <windows.h>
#include <string>

LogWindow* g_logWindow = nullptr;

LogWindow::LogWindow()
    : m_hWndMain(NULL), m_hWndLog(NULL)
{
}

LogWindow::~LogWindow()
{
}

void LogWindow::Create()
{
    if (m_hWndLog) return;

    WNDCLASSA wc = {};
    wc.lpfnWndProc = LogWndProc;
    wc.hInstance = GetModuleHandleA(nullptr);
    wc.lpszClassName = "AntiTemperLogWnd";
    RegisterClassA(&wc);

    int screenWidth = GetSystemMetrics(SM_CXSCREEN);
    int screenHeight = GetSystemMetrics(SM_CYSCREEN);

    m_hWndMain = CreateWindowExA(
        WS_EX_TOPMOST,
        wc.lpszClassName, "AntiTemper Console",
        WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_MINIMIZEBOX | WS_THICKFRAME | WS_MAXIMIZEBOX,
        200, 200, screenWidth / 3, (int)400,
        nullptr, nullptr, wc.hInstance, nullptr
    );

    SetWindowPos(m_hWndMain, nullptr, 0, 0, 0, 0, SWP_NOZORDER | SWP_NOSIZE);

    m_hWndLog = CreateWindowExW(
        0, L"EDIT", L"",
        WS_CHILD | WS_VISIBLE | ES_MULTILINE | ES_AUTOVSCROLL |
        ES_AUTOHSCROLL | WS_VSCROLL | ES_READONLY,
        0, 0, 600, 400,
        m_hWndMain, nullptr, wc.hInstance, nullptr
    );

    EnableWindow(m_hWndLog, FALSE);

    HFONT hFont = CreateFontA(
        14, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
        ANSI_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
        DEFAULT_QUALITY, FIXED_PITCH | FF_MODERN, "Consolas"
    );
    SendMessageA(m_hWndLog, WM_SETFONT, (WPARAM)hFont, TRUE);

    ShowWindow(m_hWndMain, SW_SHOW);
    UpdateWindow(m_hWndMain);
}

void LogWindow::Clear()
{
    if (m_hWndLog)
        SetWindowTextW(m_hWndLog, L"");
}

void LogWindow::Log(const std::string& msg)
{
    if (!m_hWndLog) return;

    std::wstring wmsg(msg.begin(), msg.end());
    wmsg += L"\r\n";

    int len = GetWindowTextLengthW(m_hWndLog);
    SendMessageW(m_hWndLog, EM_SETSEL, len, len);
    SendMessageW(m_hWndLog, EM_REPLACESEL, FALSE, (LPARAM)wmsg.c_str());
    SendMessageW(m_hWndLog, EM_SCROLLCARET, 0, 0);
}

LRESULT CALLBACK LogWindow::LogWndProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    switch (uMsg)
    {
    case WM_SIZE:
    {
        if (g_logWindow && g_logWindow->m_hWndLog)
        {
            int width = LOWORD(lParam);
            int height = HIWORD(lParam);

            // resize the edit control to fill the client area
            SetWindowPos(g_logWindow->m_hWndLog, nullptr, 0, 0, width, height,
                SWP_NOZORDER | SWP_NOACTIVATE);
        }
        return 0;
    }

    case WM_USER + 1:
    {
        auto* text = reinterpret_cast<const char*>(wParam);
        if (text)
            g_logWindow->Log(text);
        return 0;
    }
    }

    return DefWindowProc(hwnd, uMsg, wParam, lParam);
}

DWORD WINAPI LogWindow::LogThread(LPVOID lpParam)
{
    g_logWindow->Create();

    MSG msg;
    while (GetMessage(&msg, NULL, 0, 0)) {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }
    return 0;
}