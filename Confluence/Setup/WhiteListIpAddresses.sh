#!/bin/bash

sudo apt-get update
sudo apt-get install iptables iptables-persistent -y
echo "Enter IPs of allowed device connections, seperated by space"
read -r ips

sudo iptables --flush INPUT
sudo iptables -P INPUT ACCEPT

for ip in $ips; do
  sudo iptables -A INPUT -p tcp --dport 22 --source "$ip" -j ACCEPT
  sudo iptables -A INPUT -p tcp --dport 443 --source "$ip" -j ACCEPT
  sudo iptables -A INPUT -p tcp --dport 450 --source "$ip" -j ACCEPT
  sudo iptables -A INPUT -p tcp --dport 5432 --source "$ip" -j ACCEPT
  sudo iptables -A INPUT -p tcp --dport 3389 --source "$ip" -j ACCEPT
done

sudo iptables -A INPUT -p tcp --dport 22 -j DROP
sudo iptables -A INPUT -p tcp --dport 443 -j DROP
sudo iptables -A INPUT -p tcp --dport 450 -j DROP
sudo iptables -A INPUT -p tcp --dport 5432 -j DROP
sudo iptables -A INPUT -p tcp --dport 3389 -j DROP

sudo netfilter-persistent save