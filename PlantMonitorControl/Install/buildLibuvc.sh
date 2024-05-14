#!/bin/bash

### Thermal Camera Libraries --> move libusv.so file to the following repo folder: https://github.com/groupgets/purethermal1-uvc-capture/tree/master/python
### sudo python3 uvc-radiometry.py should work 
cd ~ || exit
git clone https://github.com/Machriam/ThermalLibuvc.git
sudo apt-get install -y cmake libusb-1.0-0-dev
cd ThermalLibuvc || exit
mkdir build
cd build || exit
cmake ..
make
