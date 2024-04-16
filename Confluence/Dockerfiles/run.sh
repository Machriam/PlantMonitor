#!/bin/bash

cp -f /srv/secrets/plantmonitor.crt /usr/local/share/ca-certificates/
/usr/sbin/update-ca-certificates
dotnet /PlantMonitor/GatewayApp/Backend/Plantmonitor.Server/dist/Plantmonitor.Server.dll