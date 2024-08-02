#!/bin/bash

cp -f /srv/secrets/plantmonitor.crt /usr/local/share/ca-certificates/
/usr/sbin/update-ca-certificates
cp /mnt/emgu_repo/emgucv/libs/runtimes/ubuntu-x64/native/libcvextern.so /PlantMonitor/GatewayApp/Backend/Plantmonitor.Server/dist/libcvextern.so
dotnet /PlantMonitor/GatewayApp/Backend/Plantmonitor.Server/dist/Plantmonitor.Server.dll