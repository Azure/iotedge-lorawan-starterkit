#!/bin/bash

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

./lora_pkt_fwd