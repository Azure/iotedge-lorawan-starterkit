#!/bin/bash
set -e
export MODULE_WORKDIR=$(cd $(dirname $0) && pwd)
. "$MODULE_WORKDIR/helper-functions.sh"


if [[ -z "$TC_URI" ]] && [[ -z "$CUPS_URI" ]]; then
    echo "One of CUPS_URI or TC_URI should be specified. Exiting..."
    exit 1
fi

if [[ -z "$STATION_PATH" ]]; then
    $STATION_PATH=/basicstation
fi

resetPin
setFixedStationEui
conditionallySetupCups
conditionallySetupTc

if [[ -z "$SPI_DEV" ]] || [[ $SPI_DEV == '$LBS_SPI_DEV' ]]; then
    echo "No custom SPI dev set up, defaulting to spi dev 0"
    SPI_DEV=0
fi

#start basestation
echo "Starting base station..."
if [[ -z "$SPI_SPEED"  ]] || [[ "$SPI_SPEED" == '$LBS_SPI_SPEED' ]]; then
    echo "No SPI Speed found defaulting to 8mbps"
    RADIODEV=/dev/spidev$SPI_DEV.0 $STATION_PATH/station.std -f
else
    if [ "$SPI_SPEED" == "2" ]; then
        echo "Spi speed set to 2 mbps"
        RADIODEV=/dev/spidev$SPI_DEV.0 $STATION_PATH/station.spispeed2 -f
    else
        if [ "$SPI_SPEED" == "8" ]; then
            echo "Spi speed set to 8 mbps"
            RADIODEV=/dev/spidev$SPI_DEV.0 $STATION_PATH/station.std -f
        else
            echo "The value $SPI_SPEED is not supported as custom value. Supported values are 2 or 8"
            exit 1;
        fi
    fi
fi
