#!/bin/bash
set -e

DEFAULT_CERTS_PATH="/var/lorastarterkit/certs"
DEFAULT_CLIENT_CA_PATH="$DEFAULT_CERTS_PATH/client.ca.crt"
CA_PATH="/usr/local/share/ca-certificates/lnsclientca.crt"

conditionalCopy() {
    if [[ -z "$1" ]]; then
        echo "No proper path detected in environment variables. Trying to check for default location."
        if [ -z "$(ls -A $2 2> /dev/null)" ]; then
            echo "No file found at $2. Nothing was copied over."
        else
            echo "A file was found at $2. Copying it over."
            cp -v $2 $CA_PATH
        fi
    else
        cp -v $1 $CA_PATH
    fi
}

if [[ -z "$LNS_SERVER_PFX_PATH" ]]; then
    echo "No PFX is set for the server side authentication. No need to trust any certificate."
else
    conditionalCopy "$CLIENT_CA_PATH" "$DEFAULT_CLIENT_CA_PATH"
fi

if [ -z "$(ls -A $CA_PATH 2> /dev/null)" ]; then
    echo "No file found at $CA_PATH therefore no update-ca-certificates needed."
else
    update-ca-certificates --fresh
fi

dotnet LoRaWanNetworkSrvModule.dll