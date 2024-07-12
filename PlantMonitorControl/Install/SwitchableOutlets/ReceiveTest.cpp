#include "RCSwitch.h"
#include <stdlib.h>
#include <stdio.h>
#include <unistd.h>

int main(int argc, char *argv[]) {

    if (argc!=2) return 1;
    int PIN = atoi(argv[1]); 
    if (wiringPiSetup() == -1) return 1;

    RCSwitch mySwitch = RCSwitch();
    mySwitch.enableReceive(PIN);
    printf("Waiting for data");
    while(1) {
      if (mySwitch.available()) {
        unsigned long value = mySwitch.getReceivedValue();
        if (value == 0) {
          printf("Unknown encoding\n");
        } else {    
          printf("%lu\n", value );
          return 0;
        }
      }
      usleep(100); 
  }
    return 0;
}