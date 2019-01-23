FROM microsoft/dotnet:2.1-runtime-stretch-slim AS base

RUN apt-get update && \
    apt-get install -y --no-install-recommends unzip procps && \
    rm -rf /var/lib/apt/lists/*

RUN useradd -ms /bin/bash moduleuser
USER moduleuser
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l ~/vsdbg


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

RUN dotnet publish -c Debug -o out

FROM base
WORKDIR /app
COPY --from=build-env /build/LoRaEngine/modules/LoRaWanNetworkSrvModule/LoRaWanNetworkSrvModule/out/* ./

ENTRYPOINT ["dotnet", "LoRaWanNetworkSrvModule.dll"]