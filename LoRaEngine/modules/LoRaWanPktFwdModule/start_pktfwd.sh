#!/bin/sh


if [[ -z "${region}" ]]; then
     echo "No region detected in environment variables, defaulting to EU" 
else
    if ["$region"=="US"]
        echo "US region detected." 
        cp global_conf.us.json global_conf.json 
    fi
fi
./reset_lgw.sh start $RESET_PIN
./lora_pkt_fwd