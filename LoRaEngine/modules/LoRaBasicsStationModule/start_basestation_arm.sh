#!/bin/bash

DEFAULT_TC_TRUST_PATH="/var/lorastarterkit/certs/tc.trust"

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

if [[ -z "$FIXED_STATION_EUI" ]] || [[ $FIXED_STATION_EUI == '$LBS_FIXED_STATION_EUI' ]]; then
    echo "No custom station EUI is set, the basic station will select an EUI"
    sed -i 's/\"routerIdPlaceholder\": \"routerIdPlaceholder\",//g' station.conf
else
    echo "Basic station will start with custom EUI: $FIXED_STATION_EUI"
    sed -i "s/\"routerIdPlaceholder\": \"routerIdPlaceholder\",/\"routerid\":\"$FIXED_STATION_EUI\",/g" station.conf
fi


if [[ -z "$TC_TRUST_PATH" ]]; then
    echo "No TC_TRUST_PATH detected in environment variables. Trying to check for default location."
    if [ -z "$(ls -A $DEFAULT_TC_TRUST_PATH 2> /dev/null)" ]; then
        echo "No file found at $DEFAULT_TC_TRUST_PATH. Nothing was copied over."
    else
        echo "A file was found at $DEFAULT_TC_TRUST_PATH. Copying it over."
        cp -v $DEFAULT_TC_TRUST_PATH .
    fi
else
    cp -v $TC_TRUST_PATH tc.trust
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
