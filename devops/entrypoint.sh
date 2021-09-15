#!/bin/bash

if [ $1 = "build" ]; then
    echo "Building iot edge module"
    sudo -E iotedgedev $1 -f $2/$3 -P $4
elif [ $1 = "push" ]; then
    echo "Pushing iot edge module"
    sudo -E iotedgedev $1 -f $2/$3 -P $4
elif [ $1 = "deploy" ]; then
    echo "Deploying iot edge module"
    sudo -E iotedgedev genconfig -f $2/$3 -P $4  --fail-on-validation-error
    sudo az extension add --name azure-iot
    sudo -E az iot edge deployment delete --login "$IOTHUB_CONNECTION_STRING" --deployment-id "$IOT_EDGE_DEPLOYMENT_ID"
    sudo -E az iot edge deployment create --login "$IOTHUB_CONNECTION_STRING" --content "config/${3//'.template'}" --deployment-id "$IOT_EDGE_DEPLOYMENT_ID" --target-condition "deviceId='$DEVICE_ID'"
fi
