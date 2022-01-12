#!/bin/bash
set -e

DEFAULT_CERTS_PATH="/var/lorastarterkit/certs"
DEFAULT_CLIENT_CA_PATH="$DEFAULT_CERTS_PATH/client.ca.crt"
CA_PATH="/usr/local/share/ca-certificates/"

conditionalCopy() {
    if [ -z "$(ls -A $1 2> /dev/null)" ]; then
        echo "No file found at $1. Nothing was copied over."
    else
        cp -v $1 $CA_PATH
        certificateCopied=0
    fi
}

if [[ -z "$LNS_SERVER_PFX_PATH" ]]; then
    echo "No PFX is set for the server side authentication. No need to trust any certificate."
else
    if [[ -z "$CLIENT_CA_PATH" ]]; then
        conditionalCopy $DEFAULT_CLIENT_CA_PATH
    else
        #Split the CLIENT_CA_PATH based on the delimiter ';'
        readarray -d ';' -t strarr <<< "$CLIENT_CA_PATH"
        for (( n=0; n < ${#strarr[*]}; n++))
        do
            conditionalCopy "${strarr[n]}"
        done
    fi
fi

if [[ ! -z $certificateCopied ]] && [[ $certificateCopied -eq 0 ]]; then
    update-ca-certificates --fresh
else
    echo "No certifiate copied, therefore no update-ca-certificates needed."
fi

dotnet LoRaWanNetworkSrvModule.dll
