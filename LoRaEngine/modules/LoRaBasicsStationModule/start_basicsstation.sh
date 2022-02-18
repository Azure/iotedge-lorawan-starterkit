#!/bin/bash
set -e
export MODULE_WORKDIR=$(cd $(dirname $0) && pwd)
. "$MODULE_WORKDIR/helper-functions.sh"

if [[ -z "$TC_URI" ]] && [[ -z "$CUPS_URI" ]]; then
    echo "One of CUPS_URI or TC_URI should be specified. Exiting..."
    exit 1
fi

if [[ -z "$STATION_PATH" ]]; then
    STATION_PATH=/basicstation
fi

if [[ "$CORECELL" == true ]]; then
    mv corecell.station.conf station.conf
else
    mv sx1301.station.conf station.conf
fi

resetPin
setFixedStationEui
conditionallySetupCups
conditionallySetupTc
setLogLevel

if [[ -z "$RADIODEV" ]]; then
    if [[ -z "$SPI_DEV" ]] || [[ $SPI_DEV == '$LBS_SPI_DEV' ]]; then
        echo "No custom SPI dev set up, defaulting to spi dev 0"
        SPI_DEV=0
    fi
    export RADIODEV=/dev/spidev$SPI_DEV.0
fi

#start basics station
echo "Starting basics station..."
if [[ "$CORECELL" == true ]]; then
    echo "Starting Corecell (SX1302) binary"
    $STATION_PATH/station.corecell -f
elif [[ -z "$SPI_SPEED" ]] || [[ "$SPI_SPEED" == '$LBS_SPI_SPEED' ]] || [ "$SPI_SPEED" == "8" ]; then
    echo "Starting SX1301 binary with spi speed set to 8 mbps"
    $STATION_PATH/station.std -f
elif [ "$SPI_SPEED" == "2" ]; then
    echo "Starting SX1301 binary with spi speed set to 2 mbps"
    $STATION_PATH/station.spispeed2 -f
else
    echo "The value $SPI_SPEED is not supported as custom value. Supported values are 2 or 8"
    exit 1;
fi
