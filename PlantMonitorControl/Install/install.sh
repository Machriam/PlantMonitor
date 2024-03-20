#!/bin/bash

curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --verbose --dry-run

dotnet= ~/.dotnet/dotnet 

cd ~/PlantMonitor/PlantMonitorControl
sudo dotnet build -c Release -o /srv/dist -r linux-arm --no-self-contained
sudo ln -sf "$(pwd)"/.dotnet/dotnet /srv/dotnet