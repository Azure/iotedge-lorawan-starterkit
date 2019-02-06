FROM microsoft/dotnet:2.1-sdk AS build-env

WORKDIR /build/Samples/DecoderSample/
COPY ./Samples/DecoderSample ./

RUN dotnet restore

RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.1-aspnetcore-runtime-stretch-slim-arm32v7 AS runtime
WORKDIR /app
COPY --from=build-env /build/Samples/DecoderSample/out/* ./
ENTRYPOINT ["dotnet", "SensorDecoderModule.dll"]
