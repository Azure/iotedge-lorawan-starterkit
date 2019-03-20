#! /bin/bash
docker push "$1.azurecr.io/decodersample:$2-amd64"
docker push "$1.azurecr.io/decodersample:$2-arm32v7"

docker manifest create "$1.azurecr.io/decodersample:$2" "$1.azurecr.io/decodersample:$2-amd64" "$1.azurecr.io/decodersample:$2-arm32v7"
docker manifest push "$1.azurecr.io/decodersample:$2"