#!/bin/bash

sudo pkill -9 -f dotnet
sudo /srv/dotnet build -c Release -o /srv/dist -r linux-arm --no-self-contained ~/PlantMonitor/PlantMonitorControl/PlantMonitorControl.csproj
sudo /srv/dotnet /srv/dist/PlantMonitorControl.dll