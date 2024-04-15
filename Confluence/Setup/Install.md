# WLAN-Router Installation

## bintec  RS232jw

1.  Detach power from Router and hold the reset key pressed. While pressing the key attach the power again and wait while still pressing the key, until the status LED has blinked 5 times
2.  Go to Assistants and Internet Access
    1.  Add a new external gateway/cable modem (Lan cable to the university LAN must be connected to ETH5)
    2.  Configure Physical Ethernet Port as ETH5
    3.  Internet Service Provider can remain undefined
    4.  IP parameters obtained dynamically is disabled.
    5.  Enter IP settings for static IP
3.  Go to Wireless LAN 
    1.  Change the Operation Mode to Access Point, 2,4 Ghz
    2.  Add a new vss. 
    3.  Network Name should be the one later used for Raspberry Pis to automatically connect to
    4.  WPA-PSK with a password
    5.  Physical Ethernet Port as ETH1
    6.  Address Mode static and Bridge Interface should automatically be br0
    7.  Gateway IP Adress as it is (192.168.0.254)
    8.  Use as DHCP Server enabled and IP-Address Range from 192.168.0.100-192.168.0.149
4.  Start WLAN and press Save configuration on top


# Raspberry Pi Zero 2W Installation

1. Download Raspberry Pi Imager [Link](https://www.raspberrypi.com/software/)
2. Download Raspberry Pi Image [Link](https://downloads.raspberrypi.com/raspios_lite_armhf/images/raspios_lite_armhf-2023-12-11/2023-12-11-raspios-bookworm-armhf-lite.img.xz)
   1. If the Image is not available anymore, any image without desktop and with 32 bit should work
3. Install and start the program
4. Select No Filtering for Raspberry Pi Device
5. Select the downloaded OS
6. Select the SD-Card to override
7. Select Edit Settings:
   1. Set username and password
   2. Configure WLAN with SSID and Password --> The Raspberry Pi then attempts to connect automatically to the WLAN
   3. Locale Settings Europe/Berlin and Keyboard layout DE
   4. Under Services Enable SSH with password authentication
   5. Press save and press Yes to the dialog to apply OS customisation settings

# Gateway Server Installation

## Debian Bookworm Setup

- Install Debian
- If DNS is not available --> resolvconf must be downloaded on a stick, if internet is not working: 
    - `sudo apt install resolvconf`
    - `resolvconf --enable-updates`
    - `resolvconf -u`
- Install SSH-Server if not available:
  - `sudo apt install openssh-server`
  - `sudo systemctl enable ssh`
  - `sudo reboot now`
- Install Git and pull Repo for starting install script
  - `sudo apt install git`
  - `git clone https://github.com/Machriam/PlantMonitor.git`
  - `sudo chmod -R +x PlantMonitor`
  - `cd PlantMonitor/Confluence/Setup`
  - `./SetupGatewayMachine.sh`
- WLAN Hotspot, when WLAN card is available
  - Find names lan interface and wlan interface. Here: `enp0s31f6` and `wlp3s0`
  - `sudo apt-get install hostapd wireless-tools dnsmasq iw`
  - `sudo apt-get purge network-manager`
  - `sudo echo -e "interface=wlp3s0\nssid=HOTSPOTNAME\nhw_mode=g\nchannel=7\nmacaddr_acl=0\nauth_algs=1\nignore_broadcast_ssid=0\nwpa=2\nwpa_passphrase=PASSWORD\nwpa_key_mgmt=WPA-PSK\nwpa_pairwise=TKIP\nrsn_pairwise=CCMP" | sudo tee /etc/hostapd/hostapd.conf`
    - Replace HOTSPOTNAME with the WLAN name and PASSWORD with the password. The raspberry pi should be configured to use the same wifi by default
  - `DAEMON_CONF="/etc/hostapd/hostapd.conf"`
  - `sudo echo -e "\ninterface=wlp3s0\ndhcp-range=192.168.1.100,192.168.1.150,255.255.255.0,infinite" | sudo tee -a /etc/dnsmasq.conf`
  - `sudo echo -e "\nauto wlp3s0\niface wlp3s0 inet static\n    address 192.168.1.1\n    netmask 255.255.255.0\n" | sudo tee -a /etc/network/interfaces`
  - `echo -e "\nnet.ipv4.ip_forward=1" | sudo tee -a /etc/sysctl.conf`
  - `sudo iptables -t nat -A POSTROUTING -o enp0s31f6 -j MASQUERADE`
  - `sudo iptables -A FORWARD -i enp0s31f6 -o wlp3s0 -m state --state RELATED,ESTABLISHED -j ACCEPT`
  - `sudo iptables -A FORWARD -i wlp3s0 -o enp0s31f6 -j ACCEPT`
  - `sudo apt install iptables-persistent` --> Yes when prompted to save current settings
  - `sudo systemctl unmask hostapd`
  - `sudo systemctl enable hostapd`
  - `sudo systemctl start hostapd`
  - `sudo systemctl restart dnsmasq`


## Generate in a Linux environment the following certificate

```
openssl req -newkey rsa:2048 -x509 -nodes -keyout ./plantmonitor.key -new -out ./plantmonitor.crt \
    -subj /CN=PlantMonitor/C=DE/ST=PM/L=PM/OU=Plantmonitor/O=Plantmonitor/emailAddress=plant@monitor.com/ -reqexts SAN -extensions SAN -config <(cat /etc/ssl/openssl.cnf \
    <(printf '[SAN]\nsubjectAltName=IP:127.0.0.1, DNS:localhost, ') <(for i in {1..255}; do echo -n "IP:192.168.0.$i, "; done | sed 's/, $//')) -sha256 -days 3650 -addext basicConstraints=CA:true
```

1. The generated certificate paths must be added in the appsettings file of the Gateway Server
2. The certificate can be installed by clicking on the crt file
3. During the installation place the certificate into the `Trusted Root Certification Authorities`

