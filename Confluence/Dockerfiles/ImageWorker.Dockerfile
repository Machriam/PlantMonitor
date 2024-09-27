FROM opencv-emgu-net8
RUN chmod -R +x /PlantMonitor/Confluence
WORKDIR "/PlantMonitor/GatewayApp/Backend/Plantmonitor.ImageWorker"
RUN dotnet build -c Release -o ./dist -r linux-x64 --no-self-contained ./Plantmonitor.ImageWorker.csproj

ENTRYPOINT [ "/PlantMonitor/Confluence/Dockerfiles/runImageWorker.sh" ]
