#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <unistd.h>
#include <sys/time.h>
#include <string>
#include <vector>
#include <sstream>
#include <pigpio.h>
#include "Realtime.h"

std::vector<int> getMotorDelays(char* times){
    std::string input = times;
    std::vector<int> values;
    std::stringstream ss(input);
    std::string item;
    while (std::getline(ss, item, ',')) {
        values.push_back(std::stoi(item));
    }
    return values;
}

int main(int argc, char *argv[])
{
    if (argc != 5){
        printf("Usage: %s <Direction_Pin> <Pulse_Pin> <Direction> <Delays>\n", argv[0]);
        return 1;
    }
    int Direction_Pin=std::stoi(argv[1]);
    int Pulse_Pin=std::stoi(argv[2]);
    int Direction=std::stoi(argv[3]);
    std::vector<int> delays = getMotorDelays(argv[4]);
	gpioInitialise();
	gpioSetMode(Direction_Pin,PI_OUTPUT);
	gpioSetMode(Pulse_Pin,PI_OUTPUT);
	gpioWrite(Direction_Pin,Direction);
	Realtime::setup();
    for (int i = 0; i < delays.size(); i++){
        gpioWrite(Pulse_Pin, 1);
        Realtime::delay(delays[i]*0.5);
        gpioWrite(Pulse_Pin, 0);
        Realtime::delay(delays[i]*0.5);
    }
}