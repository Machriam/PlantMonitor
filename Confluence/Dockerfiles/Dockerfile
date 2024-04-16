FROM node:21 as frontend
RUN apt-get install git
RUN git clone https://github.com/Machriam/PlantMonitor.git
WORKDIR "/PlantMonitor/GatewayApp/Frontend/Plantmonitor.Website"
RUN npm i
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0
COPY --from=frontend /PlantMonitor /PlantMonitor
RUN chmod -R +x /PlantMonitor/Confluence
WORKDIR "/PlantMonitor/GatewayApp/Backend/Plantmonitor.Server"
RUN dotnet build -c Release -o ./dist -r linux-x64 --no-self-contained ./Plantmonitor.Server.csproj
COPY --from=frontend /PlantMonitor/GatewayApp/Frontend/Plantmonitor.Website/build /PlantMonitor/GatewayApp/Backend/Plantmonitor.Server/wwwroot

ENTRYPOINT [ "/PlantMonitor/Confluence/Dockerfiles/run.sh" ]
