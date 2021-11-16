#!/bin/bash

sudo -E iotedgedev genconfig -f $2/$1 -P $3  --fail-on-validation-error
sudo az extension add --name azure-iot
sudo -E az iot edge deployment delete --login "$IOTHUB_CONNECTION_STRING" --deployment-id "$IOT_EDGE_DEPLOYMENT_ID"
sudo -E az iot edge deployment create --login "$IOTHUB_CONNECTION_STRING" --content "config/${1//'.template'}" --deployment-id "$IOT_EDGE_DEPLOYMENT_ID" --target-condition "deviceId='$DEVICE_ID'"

