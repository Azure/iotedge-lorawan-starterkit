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
    SPI_DEV=0
fi

#Generate tc.uri file
if [[ -z "$TC_URI" ]]; then
    echo "No TC_URI detected in environment variables."
else
    echo "TC_URI is set to: $TC_URI"
    touch tc.uri && echo "$TC_URI" > tc.uri

    #start basestation
    echo "Starting base station..."
    if [[ -z "$SPI_SPEED"  ]] || [[ "$SPI_SPEED" == '$LBS_SPI_SPEED' ]]; then
        echo "No SPI Speed found defaulting to 8mbps"
        RADIODEV=/dev/spidev$SPI_DEV.0 /bin/station.std -f
    else
        if [ "$SPI_SPEED" == "2" ]; then
            echo "Spi speed set to 2 mbps"
            RADIODEV=/dev/spidev$SPI_DEV.0 /bin/station.spispeed2 -f
        else
            if [ "$SPI_SPEED" == "8" ]; then
                echo "Spi speed set to 8 mbps"
                RADIODEV=/dev/spidev$SPI_DEV.0 /bin/station.std -f
            else
                echo "The value $SPI_SPEED is not supported as custom value. Supported values are 2 or 8"
                exit 1;
            fi
        fi
    fi
fi
