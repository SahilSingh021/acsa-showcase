#include "AntiTemper.h"
#include "AntiDebugger.h"
#include "LogWindow.h"
#include "AuthPoll.h"

void AntiDebugger::CheckDebugger()
{
    BOOL isAttached = false;

    if (IsDebuggerPresent())
        isAttached = true;

    BOOL remoteDebug = false;
    if (CheckRemoteDebuggerPresent(GetCurrentProcess(), &remoteDebug) && remoteDebug)
        isAttached = true;

    if (isAttached && !m_debuggerAttached)
    {
        g_logWindow->Log("[AntiTemper] Debugger attached.");
        ACSA_SendUserLog(2, "[AntiTemper] Debugger attached.");
        m_debuggerAttached = true;
    }
    else if (!isAttached && m_debuggerAttached)
    {
        g_logWindow->Log("[AntiTemper] Debugger detached.");
        ACSA_SendUserLog(2, "[AntiTemper] Debugger detected.");
        m_debuggerAttached = false;
    }
}

