FROM microsoft/dotnet:2.1-sdk AS build-env
WORKDIR /app

COPY ./Logger ./Logger
COPY ./LoRaSimulator ./LoRaSimulator
COPY ./LoraTools ./LoraTools
COPY ./SimulatorLaunch ./SimulatorLaunch

WORKDIR ./SimulatorLaunch
RUN dotnet restore

RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.1-runtime-stretch-slim-arm32v7
WORKDIR /app
COPY --from=build-env /app/SimulatorLaunch/out ./
# COPY ./LoRaSimulator/testconfig.json ./

RUN useradd -ms /bin/bash moduleuser
USER moduleuser

ENTRYPOINT ["dotnet", "SimulatorLaunch.dll"]