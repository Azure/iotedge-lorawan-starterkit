#!/bin/sh
if ["$region"=="US"]
  echo "US region detected." 
  cp global_conf.us.json global_conf.json 
fi
./reset_lgw.sh start $RESET_PIN
./lora_pkt_fwd