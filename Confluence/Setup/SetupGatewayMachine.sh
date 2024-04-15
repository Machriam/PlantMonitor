#!/bin/bash

# Add Docker's official GPG key:
sudo apt-get update
sudo apt-get install ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc

# Add the repository to Apt sources:
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian \
  $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update

sudo apt-get install docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin docker-compose

cd ~
sudo apt install openssl
sudo mkdir /srv/secrets
openssl req -newkey rsa:2048 -x509 -nodes -keyout ~/plantmonitor.key -new -out ~/plantmonitor.crt \
    -subj /CN=PlantMonitor/C=DE/ST=PM/L=PM/OU=Plantmonitor/O=Plantmonitor/emailAddress=plant@monitor.com/ -reqexts SAN -extensions SAN -config <(cat /etc/ssl/openssl.cnf \
    <(printf '[SAN]\nsubjectAltName=IP:127.0.0.1, DNS:localhost, ') <(for i in {1..255}; do echo -n "IP:192.168.0.$i, "; done | sed 's/, $//')) -sha256 -days 3650 -addext basicConstraints=CA:true
sudo mv ~/plantmonitor.* /srv/secrets
sudo apt-get install debconf
sudo cp /srv/secrets/plantmonitor.crt /usr/share/ca-certificates/
sudo dpkg-reconfigure ca-certificates # Gives a prompt to confirm adding of the new certificate

cd ../Dockerfiles
sudo docker-compose -f docker-compose-linux.yml start