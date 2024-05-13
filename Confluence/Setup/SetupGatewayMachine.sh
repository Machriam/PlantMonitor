#!/bin/bash

# Add Docker's official GPG key:
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

# Add the repository to Apt sources:
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update

sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin docker-compose
sudo systemctl mask sleep.target suspend.target hibernate.target hybrid-sleep.target

# Create Self Signed Certificate for IP-Range 1-255
sudo apt install -y openssl
sudo mkdir /srv/secrets

echo "Enter IP of Gateway-PC, if not within 192.168.0.1 - 192.168.1.255"
read -r ips
additionalIps="IP: 127.0.0.1"

for ip in $ips; do
  additionalIps="$additionalIps, IP: $ip"
done
additionalIps="$additionalIps, "

echo "Creating certificate with additional IPs: $additionalIps"

openssl req -newkey rsa:2048 -x509 -nodes -keyout ~/plantmonitor.key -new -out ~/plantmonitor.crt \
    -subj /CN=PlantMonitor/C=DE/ST=PM/L=PM/OU=Plantmonitor/O=Plantmonitor/emailAddress=plant@monitor.com/ -reqexts SAN -extensions SAN -config <(cat /etc/ssl/openssl.cnf \
    <(printf '[SAN]\nsubjectAltName=%s DNS:localhost, ' "$additionalIps") <(for i in {1..255}; do echo -n "IP:192.168.0.$i, IP:192.168.1.$i, "; done | sed 's/, $//')) -sha256 -days 3650 -addext basicConstraints=CA:true
sudo mv ~/plantmonitor.* /srv/secrets
# Trusting self signed certificate in Chrome
sudo apt install -y libnss3-tools debconf
certutil -d ~/.pki/nssdb/ -A -t "TC,," -n "PlantMonitor" -i /srv/secrets/plantmonitor.crt
sudo cp /srv/secrets/plantmonitor.crt /usr/share/ca-certificates/
sudo update-ca-certificates

sudo apt-get install uuid-runtime -y
envFile="$(pwd)/../Dockerfiles/database.env"
postgresMasterPassword=$(uuidgen)
echo -e "POSTGRES_PASSWORD=$postgresMasterPassword" | sudo tee "$envFile"


cd ../Dockerfiles || exit
sudo docker-compose down
sudo docker-compose build --no-cache
sudo docker-compose up --detach

### Optional QTCreator Setup
# Enables using sudo over XRDP
xhost si:localuser:root