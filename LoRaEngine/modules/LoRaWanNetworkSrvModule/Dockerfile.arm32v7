FROM microsoft/dotnet:2.1-sdk AS build-env

WORKDIR /build
COPY ./stylecop.* ./

WORKDIR /build/LoRaEngine/modules/LoRaWanNetworkSrvModule/
COPY ./LoRaEngine/modules/LoRaWanNetworkSrvModule/Logger ./Logger
COPY ./LoRaEngine/modules/LoRaWanNetworkSrvModule/LoraTools ./LoraTools
COPY ./LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWan.NetworkServer ./LoRaWan.NetworkServer
COPY ./LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWanNetworkSrvModule ./LoRaWanNetworkSrvModule

WORKDIR /build/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWanNetworkSrvModule
RUN dotnet restore

RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.1.5-runtime-stretch-slim-arm32v7
WORKDIR /app
COPY --from=build-env /build/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWanNetworkSrvModule/out/* ./
ENTRYPOINT ["dotnet", "LoRaWanNetworkSrvModule.dll"]