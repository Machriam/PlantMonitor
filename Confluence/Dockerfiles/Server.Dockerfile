FROM node:21 AS frontend
RUN apt-get install git
RUN git clone https://github.com/Machriam/PlantMonitor.git
RUN git checkout origin/main-ffc-at-beginning-with-1-minute-wait
WORKDIR "/PlantMonitor/GatewayApp/Frontend/Plantmonitor.Website"
RUN npm i
RUN npm run build

FROM opencv-emgu-net8
COPY --from=frontend /PlantMonitor /PlantMonitor
RUN chmod -R +x /PlantMonitor/Confluence
WORKDIR "/PlantMonitor/GatewayApp/Backend/Plantmonitor.Server"
RUN dotnet build -c Release -o ./dist -r linux-x64 --no-self-contained ./Plantmonitor.Server.csproj
COPY --from=frontend /PlantMonitor/GatewayApp/Frontend/Plantmonitor.Website/build /PlantMonitor/GatewayApp/Backend/Plantmonitor.Server/wwwroot

ENTRYPOINT [ "/PlantMonitor/Confluence/Dockerfiles/run.sh" ]
