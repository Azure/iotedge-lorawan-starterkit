#!/bin/bash

# This file generate an IoT Edge deployment file using token replacement to replace secrets with environment variables
# then the file proceed to remove the previous deployment and replace it with the current one
sudo -E iotedgedev genconfig -f $2/$1 -P $3  --fail-on-validation-error
sudo az extension add --name azure-iot
sudo -E az iot edge deployment delete --login "$IOTHUB_CONNECTION_STRING" --deployment-id "$IOT_EDGE_DEPLOYMENT_ID"
sudo -E az iot edge deployment create --login "$IOTHUB_CONNECTION_STRING" --content "config/${1//'.template'}" --deployment-id "$IOT_EDGE_DEPLOYMENT_ID" --target-condition "deviceId='$DEVICE_ID'"

