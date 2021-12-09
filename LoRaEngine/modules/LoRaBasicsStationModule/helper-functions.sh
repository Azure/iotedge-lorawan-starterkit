#!/bin/bash

DEFAULT_CERTS_PATH="/var/lorastarterkit/certs"
DEFAULT_TC_TRUST_PATH="$DEFAULT_CERTS_PATH/tc.trust"
DEFAULT_TC_CRT_PATH="$DEFAULT_CERTS_PATH/tc.crt"
DEFAULT_TC_KEY_PATH="$DEFAULT_CERTS_PATH/tc.key"
DEFAULT_CUPS_TRUST_PATH="$DEFAULT_CERTS_PATH/cups.trust"
DEFAULT_CUPS_CRT_PATH="$DEFAULT_CERTS_PATH/cups.crt"
DEFAULT_CUPS_KEY_PATH="$DEFAULT_CERTS_PATH/cups.key"

conditionalCopy() {
    if [[ -z "$1" ]]; then
        echo "No proper path detected in environment variables. Trying to check for default location."
        if [ -z "$(ls -A $2 2> /dev/null)" ]; then
            echo "No file found at $2. Nothing was copied over."
        else
            echo "A file was found at $2. Copying it over."
            cp -v $2 .
        fi
    else
        cp -v $1 tc.trust
    fi
}

tcCertCopy() {
    if [[ "$TC_URI" == *"wss"* ]]; then
        echo "A secure protocol was specified for LNS endpoint. Copying over certificate files".
        conditionalCopy "$TC_TRUST_PATH" "$DEFAULT_TC_TRUST_PATH"
        conditionalCopy "$TC_CRT_PATH" "$DEFAULT_TC_CRT_PATH"
        conditionalCopy "$TC_KEY_PATH" "$DEFAULT_TC_KEY_PATH"
    fi
}

cupsCertCopy() {
    if [[ "$CUPS_URI" == *"https"* ]]; then
        echo "A CUPS endpoint was specified. Copying over certificate files".
        conditionalCopy "$CUPS_TRUST_PATH" "$DEFAULT_CUPS_TRUST_PATH"
        conditionalCopy "$CUPS_CRT_PATH" "$DEFAULT_CUPS_CRT_PATH"
        conditionalCopy "$CUPS_KEY_PATH" "$DEFAULT_CUPS_KEY_PATH"
    fi
}

resetPin() {
    if [[ -z "$RESET_PIN" ]]; then
        echo "No RESET_PIN environment variable set, skipping the pin reset. If you experience problem with starting the concentrator please set this variable to your manufacturer reset pin"
    else
        echo "Resetting the pin"
        ./reset_lgw.sh stop $RESET_PIN
        ./reset_lgw.sh start $RESET_PIN
        echo "Finished resetting the pin"
    fi
}

setFixedStationEui() {
    if [[ -z "$FIXED_STATION_EUI" ]] || [[ $FIXED_STATION_EUI == '$LBS_FIXED_STATION_EUI' ]]; then
        echo "No custom station EUI is set, the basic station will select an EUI"
        sed -i 's/\"routerIdPlaceholder\": \"routerIdPlaceholder\",//g' station.conf
    else
        echo "Basic station will start with custom EUI: $FIXED_STATION_EUI"
        sed -i "s/\"routerIdPlaceholder\": \"routerIdPlaceholder\",/\"routerid\":\"$FIXED_STATION_EUI\",/g" station.conf
    fi
}

conditionallySetupCups() {
    if [[ -z "$CUPS_URI" ]]; then
        echo "Will start in NO_CUPS mode as no CUPS_URI has been specified."
    else
        cupsCertCopy
        echo "CUPS_URI is set to: $CUPS_URI"
        touch cups.uri && echo "$CUPS_URI" > cups.uri
    fi
}

conditionallySetupTc() {
    if [[ -z "$TC_URI" ]]; then
        echo "No TC_URI detected in environment variables."
    else
        tcCertCopy
        echo "TC_URI is set to: $TC_URI"
        touch tc.uri && echo "$TC_URI" > tc.uri
    fi
}