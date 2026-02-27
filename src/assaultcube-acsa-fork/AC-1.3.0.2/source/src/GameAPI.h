#pragma once
#ifndef STANDALONE
extern "C" {
    __declspec(dllexport)
        void AC_ConnectToServer(char* servername, int* serverport, char* password);
}
#endif