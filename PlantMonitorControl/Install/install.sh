#!/bin/bash

curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --verbose 

cd ~/PlantMonitor/PlantMonitorControl
sudo ln -sf "$HOME"/.dotnet/dotnet /srv/dotnet

sudo /srv/dotnet build -c Release -o /srv/dist -r linux-arm --no-self-contained

sudo openssl pkcs12 -password pass: -export -out /srv/certs/plantmonitor.pfx -inkey /srv/certs/plantmonitor.key -in /srv/certs/plantmonitor.crt
sudo cp ./Install/PlantMonitorStart.service /lib/systemd/system/
sudo chmod 644 /lib/systemd/system/PlantMonitorStart.service 
sudo systemctl daemon-reload
sudo systemctl enable PlantMonitorStart.service