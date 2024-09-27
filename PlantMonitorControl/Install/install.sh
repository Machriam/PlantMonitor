#!/bin/bash

updateComment="# Install Update Applied"

sudo dpkg --configure -a
sudo apt-get install -y libusb-1.0-0-dev python3-numpy i2c-tools

curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --verbose 

# Setup Reboot and Runtime Watchdog
if ! grep -q "$updateComment" "/etc/systemd/system.conf"; then
    echo -e "\n$updateComment\nRuntimeWatchdogSec=10\nRebootWatchdogSec=2min\nDefaultTimeoutStopSec=10s" | sudo tee -a /etc/systemd/system.conf
fi
cd ~/PlantMonitor/PlantMonitorControl
sudo ln -sf "$HOME"/.dotnet/dotnet /srv/dotnet

# Setup PlantMonitor
sudo pkill -9 -f dotnet
sudo /srv/dotnet build -c Release -o /srv/dist -r linux-arm --no-self-contained ./PlantMonitorControl.csproj
sudo rm /srv/dist/appsettings.Development.json 

# Setup IR-Camera
sudo mkdir /srv/leptonPrograms
sudo cp ~/PlantMonitor/PlantMonitorControl/Install/Lepton/* /srv/leptonPrograms/

# Setup Switchable Outlets
git clone https://github.com/WiringPi/WiringPi.git
cd WiringPi
git pull
./build
cd ..
sudo mkdir /srv/switchableOutlet
sudo cp ./Install/SwitchableOutlets/* /srv/switchableOutlet/
cd /srv/switchableOutlet
sudo make
cd ~/PlantMonitor/PlantMonitorControl

# Setup Temperature Sensors
sudo python3 -m venv /srv/pythonClick
sudo /srv/pythonClick/bin/pip3 install adt7422
sudo /srv/pythonClick/bin/pip3 install smbus2
sudo mkdir /srv/pythonClick/temp2ClickPrograms
sudo cp ./Install/Temp2Click/* /srv/pythonClick/temp2ClickPrograms/

# Setup MotorMovement

sudo mkdir /srv/motorMovement
sudo cp ./Install/MotorMovement/* /srv/motorMovement
g++ -o /srv/motorMovement/movemotor /srv/motorMovement/movemotor.cc /srv/motorMovement/Realtime.cc -lpigpio -lpthread

# Setup PlantMonitor Service
sudo openssl pkcs12 -password pass: -export -out /srv/certs/plantmonitor.pfx -inkey /srv/certs/plantmonitor.key -in /srv/certs/plantmonitor.crt
sudo cp ./Install/PlantMonitorStart.service /lib/systemd/system/
sudo chmod 644 /lib/systemd/system/PlantMonitorStart.service 
sudo systemctl daemon-reload
sudo systemctl enable PlantMonitorStart.service
sudo systemctl start PlantMonitorStart.service

# Setup Raspberry Pi Zero 2W performance configuration
if ! grep -q "$updateComment" "/boot/firmware/config.txt"; then
    echo -e "\n$updateComment\nover_voltage=4\ndtparam=i2c_arm=on\ndtparam=spi=on" | sudo tee -a /boot/firmware/config.txt
    echo -e " isolcpus=3" | sudo tee -a /boot/firmware/cmdline.txt
    echo -e "\ni2c-dev" | sudo tee -a /etc/modules
fi
sudo dphys-swapfile swapoff
sudo sed -i 's/CONF_SWAPSIZE=100/CONF_SWAPSIZE=1024/g' /etc/dphys-swapfile
sudo dphys-swapfile setup
sudo dphys-swapfile swapon
