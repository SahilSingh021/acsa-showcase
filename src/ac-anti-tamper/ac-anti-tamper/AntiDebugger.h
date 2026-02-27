#pragma once

class AntiDebugger
{
public:
    void CheckDebugger();

private:
    bool m_debuggerAttached = false;
};
