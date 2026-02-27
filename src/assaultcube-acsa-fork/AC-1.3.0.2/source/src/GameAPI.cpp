#include "GameAPI.h"
#include "cube.h"
#ifndef STANDALONE
void connectserv(char* servername, int* serverport, char* password);

extern "C" __declspec(dllexport)
void AC_ConnectToServer(char* servername, int* serverport, char* password)
{
    if (!servername || !*servername) return;
    connectserv(servername, serverport, password);
}
#endif