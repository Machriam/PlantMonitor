#!/bin/bash

curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --verbose 

echo -e "\nRuntimeWatchdogSec=10\nRebootWatchdogSec=2min\nDefaultTimeoutStopSec=10s" | sudo tee -a /etc/systemd/system.conf
cd ~/PlantMonitor/PlantMonitorControl
sudo ln -sf "$HOME"/.dotnet/dotnet /srv/dotnet

sudo pkill -9 -f dotnet
sudo /srv/dotnet build -c Release -o /srv/dist -r linux-arm --no-self-contained ~/PlantMonitor/PlantMonitorControl/PlantMonitorControl.csproj

sudo openssl pkcs12 -password pass: -export -out /srv/certs/plantmonitor.pfx -inkey /srv/certs/plantmonitor.key -in /srv/certs/plantmonitor.crt
sudo cp ./Install/PlantMonitorStart.service /lib/systemd/system/
sudo chmod 644 /lib/systemd/system/PlantMonitorStart.service 
sudo systemctl daemon-reload
sudo systemctl enable PlantMonitorStart.service
sudo systemctl start PlantMonitorStart.service
echo -e "\nover_voltage=4\n" | sudo tee -a /boot/config.txt
sudo dphys-swapfile swapoff
sudo sed -i 's/CONF_SWAPSIZE=100/CONF_SWAPSIZE=1024/g' /etc/dphys-swapfile
sudo dphys-swapfile setup
sudo dphys-swapfile swapon

### Thermal Camera Libraries --> move libusv.so file to the following repo folder: https://github.com/groupgets/purethermal1-uvc-capture/tree/master/python
### sudo python3 uvc-radiometry.py should work 
cd ~
git clone https://github.com/Machriam/ThermalLibuvc.git
sudo apt-get install -y cmake libusb-1.0-0-dev
cd ThermalLibuvc
mkdir build
cd build
cmake ..
make
