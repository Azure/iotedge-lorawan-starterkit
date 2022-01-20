#!/bin/bash
set -e
export OUTPUT_DIR=$(cd $(dirname $0) && pwd)

echoerr() { echo "$@" 1>&2; }

usage() {
  programName=${0##*/}
  echo "
CUPS Protocol - Firmware Upgrade Preparation

This CLI tool is helping Azure IoT Edge LoRaWAN Starter Kit users to generate the files needed for executing a firmware upgrade.

Usage: $programName stationEui firmwareUpgradeFilePath

Arguments:
  stationEui               (REQUIRED) EUI of the target Basics Station
  firmwareUpgradeFilePath  (REQUIRED) The path of the binary to be executed on Basics Station for upgrading the firmware
"
  exit 1
}

if [ $# -eq 0 ]; then
    echo "No arguments provided."
    usage
    exit 1
fi

if [ $# -eq 1 ]; then
    echo "Not enough arguments provided."
    usage
    exit 1
fi

if [ $# -gt 2 ]; then
    echo "Too many arguments provided."
    usage
    exit 1
fi

export STATION_OUTPUT_DIR="$OUTPUT_DIR/$1"

echoerr "## All outputs will be saved into $STATION_OUTPUT_DIR"

mkdir -p $STATION_OUTPUT_DIR

# Generate the signature
openssl ecparam -name prime256v1 -genkey | openssl ec -out $STATION_OUTPUT_DIR/sig-0.pem &> /dev/null
openssl ec -in $STATION_OUTPUT_DIR/sig-0.pem -pubout -out $STATION_OUTPUT_DIR/sig-0.pub &> /dev/null
openssl ec -in $STATION_OUTPUT_DIR/sig-0.pub -inform PEM -outform DER -pubin 2> /dev/null | tail -c 64 > $STATION_OUTPUT_DIR/sig-0.key

echoerr "## Key has been generated"
echo "KEY $STATION_OUTPUT_DIR/sig-0.key"

# Calculate the checksum of the signature
SIGNATURE_CRC=`cat $STATION_OUTPUT_DIR/sig-0.key | gzip -1 | tail -c 8 | od -t u4 -N 4 -An --endian=little`

echo -n $SIGNATURE_CRC > $STATION_OUTPUT_DIR/sig-0.crc
echoerr "## Key signature has been computed"
echo "CRC $STATION_OUTPUT_DIR/sig-0.crc"

# Calculate the digest of an update.sh file
openssl dgst -sha512 -sign $STATION_OUTPUT_DIR/sig-0.pem $2 | base64 -w0 > $STATION_OUTPUT_DIR/fwUpdate.digest
echoerr "## Base64 encoded digest has been computed"
echo "DGST $STATION_OUTPUT_DIR/fwUpdate.digest"

rm $STATION_OUTPUT_DIR/sig-0.pem $STATION_OUTPUT_DIR/sig-0.pub
echoerr "## Done"
