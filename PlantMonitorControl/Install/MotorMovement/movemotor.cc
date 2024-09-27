#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>
#include <unistd.h>
#include <sys/time.h>
#include <string>
#include <vector>
#include <sstream>
#include <fstream>
#include <pigpio.h>
#include "Realtime.h"
int _positionReadFailed = -999999;
uint32_t _fileUpdateInterval = 50000;
struct CurrentPosition
{
    int position;
    bool dirty;
};

std::vector<int> getMotorDelays(char *times)
{
    std::string input = times;
    std::vector<int> values;
    std::stringstream ss(input);
    std::string item;
    while (std::getline(ss, item, ','))
    {
        values.push_back(std::stoi(item));
    }
    return values;
}

CurrentPosition readCurrentPosition(const std::string &filePath)
{
    std::ifstream file(filePath);
    if (!file.is_open())
    {
        perror("Failed to open position file");
        return {_positionReadFailed, true};
    }
    int position;
    file >> position;
    if (file.peek() == '?')
    {
        file.close();
        return {position, true};
    }
    file.close();
    return {position, false};
}

void writeCurrentPosition(const std::string &filePath, CurrentPosition position)
{
    std::ofstream file(filePath, std::ofstream::trunc);
    if (!file.is_open())
    {
        perror("Failed to open position file for writing");
        return;
    }
    file << position.position;
    if (position.dirty)
        file << '?';
    file.close();
}

int main(int argc, char *argv[])
{
    if (argc != 9)
    {
        printf("Usage: %s <Direction_Pin> <Pulse_Pin> <Direction> <Position_File_Path> <Step_Unit> <MaxAllowedPosition> <MinAllowedPosition> <Delays>\n", argv[0]);
        return 1;
    }
    int directionPin = std::stoi(argv[1]);
    int pulsePin = std::stoi(argv[2]);
    int direction = std::stoi(argv[3]);
    CurrentPosition currentPosition = readCurrentPosition(argv[4]);
    int stepUnit = std::stoi(argv[5]);
    int maxPosition = std::stoi(argv[6]);
    int minPosition = std::stoi(argv[7]);
    std::vector<int> delays = getMotorDelays(argv[8]);
    gpioInitialise();
    gpioSetMode(directionPin, PI_OUTPUT);
    gpioSetMode(pulsePin, PI_OUTPUT);
    gpioWrite(directionPin, direction);
    Realtime::setup();
    if (currentPosition.position == _positionReadFailed)
        return 1;
    printf("Current Position: %d%s\n", currentPosition.position, currentPosition.dirty ? " (dirty)" : "");
    if (currentPosition.dirty)
    {
        printf("Position file is dirty, position must be zeroed\n");
        return 1;
    }

    currentPosition.dirty = true;
    uint32_t time = Realtime::micros();
    for (int i = 0; i < delays.size(); i++)
    {
        printf("%u\n", Realtime::micros());
        gpioWrite(pulsePin, 1);
        Realtime::delay(delays[i] * 0.5);
        printf("%u\n", Realtime::micros());
        gpioWrite(pulsePin, 0);
        Realtime::delay(delays[i] * 0.5);
        currentPosition.position += stepUnit;
        if (Realtime::micros() - time > _fileUpdateInterval)
            writeCurrentPosition(argv[4], currentPosition);
        if (currentPosition.position > maxPosition || currentPosition.position < minPosition)
        {
            writeCurrentPosition(argv[4], currentPosition);
            printf("Position out of bounds\n");
            return 0;
        }
    }
    currentPosition.dirty = false;
    writeCurrentPosition(argv[4], currentPosition);
    printf("Movement finished. New Position %d\n", currentPosition.position);
    return 0;
}