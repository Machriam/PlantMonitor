FROM opencv-emgu-net8
RUN apt-get install git
RUN git clone https://github.com/Machriam/PlantMonitor.git
RUN chmod -R +x /PlantMonitor/Confluence
WORKDIR "/PlantMonitor/GatewayApp/Backend/Plantmonitor.ImageWorker"
RUN dotnet build -c Release -o ./dist -r linux-x64 --no-self-contained ./Plantmonitor.ImageWorker.csproj

ENTRYPOINT [ "/PlantMonitor/Confluence/Dockerfiles/runImageWorker.sh" ]
