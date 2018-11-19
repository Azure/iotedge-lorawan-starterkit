#!/bin/bash
# argument 1 is repo name
# argument 2 is v-number

if [ "$1" == "-h" ]; then
  echo "'$1' is repo name, $2 version number of type X.Y.Z"
  exit 0
fi
echo $2;
if ! [[ $2 =~ [0-9]+\.[0-9]+\.[0-9]+ ]]; then
  echo "'$2' must be of the form X.Y.Z" ; 
  exit 0
fi
cd  LoRaWanPktFwdModule

docker build -f Dockerfile -t $1/lorawanpktfwdmodule:x86-$2 .
docker push $1/lorawanpktfwdmodule:x86-$2 
docker build -f Dockerfile.arm32v7 -t $1/lorawanpktfwdmodule:arm32v7-$2 .
docker push $1/lorawanpktfwdmodule:arm32v7-$2 
cd ..
cd LoRaWanNetworkSrvModule
docker build -f Dockerfile -t $1/lorawannetworksrvmodule:x86-$2 .
docker push $1/lorawannetworksrvmodule:x86-$2 
docker build -f Dockerfile.arm32v7 -t $1/lorawannetworksrvmodule:arm32v7-$2 .
docker push $1/lorawannetworksrvmodule:arm32v7-$2 