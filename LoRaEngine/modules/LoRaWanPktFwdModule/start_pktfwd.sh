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
    if [ -z "$SPI_DEV" ]; then
        echo "No SPI Dev version detected in environment variables on x86, defaulting to SPI Dev 2" 
        ./lora_pkt_fwd_spidev2
    else
        if [ "$SPI_DEV" == "2" ]; then
            echo "Using SPI dev 2 from environment variables" 
            ./lora_pkt_fwd_spidev2
        else 
            if [ "$SPI_DEV" == "1" ]; then
                echo "Using SPI dev 1 from environment variables"
                ./lora_pkt_fwd_spidev1
            else
                echo "SPI_DEV variables not valid in a x86 architecture. Please select a valid value (1 or 2)."
            fi
        fi
    fi
else
    if [[ -z "$SPI_SPEED" ]]; then
        ./lora_pkt_fwd
    else
        if [ "$SPI_SPEED" == "2" ]; then
            echo "The SPI speed is set to 2Mbps"
            ./lora_pkt_fwd_spi_speed
        else
            echo "Currently only a customized value of 2Mbps is supported as custom SPI speed"
        fi
    fi
fi  
