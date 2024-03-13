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


- Add WLAN Hotspot: [Link](https://www.raspberryconnect.com/projects/65-raspberrypi-hotspot-accesspoints/168-raspberry-pi-hotspot-access-point-dhcpcd-method)

### Initial Config

- /etc/hostapd/hostapd.conf
```
interface=wlan0
driver=nl80211
ssid=PlantMonitorSpot
hw_mode=g
channel=6
wmm_enabled=0
macaddr_acl=0
auth_algs=1
ignore_broadcast_ssid=0
wpa=2
wpa_passphrase=plantmonitor
wpa_key_mgmt=WPA-PSK
rsn_pairwise=CCMP
```

- /etc/dhcpcd.conf
```
interface wlan0
nohook wpa_supplicant
static ip_address=192.168.50.10/24
static routers=192.168.50.1
```
