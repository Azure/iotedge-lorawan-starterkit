#!/bin/bash

#get current pkt fwd region
if [ -z "$REGION" ]; then
     echo "No region detected in environment variables, defaulting to EU" 
else
    if [ "$REGION" == "US" ]; then
        echo "US region detected." 
        cp global_conf.us.json global_conf.json 
    else
        if [ "$REGION" == "EU" ]; then
            echo "EU region detected." 
            cp global_conf.eu.json global_conf.json 
        fi
    fi
fi

./reset_lgw.sh start $RESET_PIN

#get current architecture for the mess processor
arch="$(uname -m)"
if [[ $arch != *"arm"* ]]; then
    if [ "$SPI_DEV" == "2" ]; then
        ./lora_pkt_fwd_spidev2
    else 
        if [ "$SPI_DEV" == "1" ]; then
            ./lora_pkt_fwd_spidev1
        else
            echo "SPI_DEV variables not present or valid in a x86 architecture. Please select a valid value (1 or 2)."
        fi
    fi
else
    ./lora_pkt_fwd
fi  