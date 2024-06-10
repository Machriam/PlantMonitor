#include "RCSwitch.h"
#include <stdlib.h>
#include <stdio.h>

int main(int argc, char *argv[]) {

    if (argc!=3) return 1;
    int PIN = atoi(argv[1]); 
    int command = atoi(argv[2]);
    if (wiringPiSetup() == -1) return 1;

    RCSwitch mySwitch = RCSwitch();
    mySwitch.enableTransmit(PIN);
    mySwitch.send(command, 24);
    return 0;
}