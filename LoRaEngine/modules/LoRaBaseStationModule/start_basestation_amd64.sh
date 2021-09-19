#!/bin/bash

#Generate tc.uri file

if [[ -z "$TC_URI" ]]; then
    echo "No TC_URI detected in environment variables."
else
    echo "TC_URI is set to: $TC_URI"
    cd examples/live-s2.sm.tc
    touch tc.uri && echo "ws://$TC_URI" > tc.uri

    #start basestation
    echo "Starting base station..."
    RADIODEV=$SPI_DEV ../../build-linux-std/bin/station -f
fi
