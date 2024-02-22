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
