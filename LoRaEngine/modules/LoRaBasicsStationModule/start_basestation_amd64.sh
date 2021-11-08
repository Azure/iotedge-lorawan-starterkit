#!/bin/bash

if [[ -z "$RESET_PIN" ]]; then
    echo "No RESET_PIN environment variable set, skipping the pin reset. If you experience problem with starting the concentrator please set this variable to your manufacturer reset pin"
else
    echo "Resetting the pin"
    ./reset_lgw.sh stop $RESET_PIN
    ./reset_lgw.sh start $RESET_PIN
    echo "Finished resetting the pin"
fi


if [[ -z "$SPI_DEV" ]] || [[ $SPI_DEV == '$LBS_SPI_DEV' ]]; then
    echo "No custom SPI dev set up, defaulting to spi dev 0"
    SPI_DEV = 0
fi

if [[ -z "$TC_TRUST_PATH" ]]; then
    echo "No TC_TRUST_PATH detected in environment variables."
else
    cp $TC_TRUST_PATH tc.trust
fi

if [[ -z "$TC_URI" ]]; then
    echo "No TC_URI detected in environment variables."
else
    echo "TC_URI is set to: $TC_URI"
    touch tc.uri && echo "$TC_URI" > tc.uri

    #start basestation
    echo "Starting base station..."
    RADIODEV=/dev/spidev$SPI_DEV.0 ./station -f
fi
