FROM microsoft/dotnet:2.0-sdk AS build-env
WORKDIR /app

COPY ./LoRaWan.NetworkServer ./LoRaWan.NetworkServer
COPY ./LoRaWanNetworkSrvModule ./LoRaWanNetworkSrvModule
COPY ./LoraTools ./LoraTools

WORKDIR ./LoRaWanNetworkSrvModule
RUN dotnet restore

RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.0-runtime
WORKDIR /app
COPY --from=build-env /app/LoRaWanNetworkSrvModule/out/* ./

RUN useradd -ms /bin/bash moduleuser
USER moduleuser

ENTRYPOINT ["dotnet", "LoRaWanNetworkSrvModule.dll"]