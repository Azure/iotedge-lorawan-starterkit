FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /build/Samples/Cayenne/

# copy everything else and build app
COPY ./Samples/CayenneSample ./
WORKDIR /build/Samples/Cayenne/CayenneDecoder

RUN dotnet restore
RUN dotnet publish -c Release -o out

FROM microsoft/dotnet:2.1-aspnetcore-runtime AS runtime
WORKDIR /app
COPY --from=build /build/Samples/Cayenne/CayenneDecoder/out ./
ENTRYPOINT ["dotnet", "CayenneDecoderModule.dll"]
