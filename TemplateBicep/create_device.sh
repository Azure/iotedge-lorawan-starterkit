create_gateway_device () {
    echo "Creating gateway device $EDGE_GATEWAY_NAME located at resourceGroup=$RESOURCE_GROUP, IotHub=$IOTHUB_NAME..."
    az iot hub device-identity create --device-id "$EDGE_GATEWAY_NAME" --am shared_private_key --edge-enabled true --hub-name "$IOTHUB_NAME" --resource-group "$RESOURCE_GROUP"

    # Gets the gateway device module manifest
    # - in bicep the template will be passed as environment variable
    # - when running locally we read directly from the file
    if [ -z "$MODULE_CONFIG" ]; then
        MODULE_CONFIG=$(cat edgeGatewayManifest.json)
    fi

    # Replace place holders in module configuration with values
    local spiDevValue=''
    if [ ! -z "${SPI_DEV}" ] && [ "$SPI_DEV" != "0" ]; then
        spiDevValue=",\"SPI_DEV\": { \"value\": \"$SPI_DEV\" }"
        echo "Setting SPI_DEV to $SPI_DEV"
    fi

    local spiSpeedValue=''
    if [ ! -z "${SPI_SPEED}" ] && [ "$SPI_SPEED" != "8" ]; then
        # Only allows values are 8 and 2
        spiSpeedValue=",\"SPI_SPEED\": { \"value\": \"2\" }"
        echo "Setting SPI_SPEED to 2"
    fi

    LNS_HOST_ADDRESS=${LNS_HOST_ADDRESS-ws://mylns:5000}
    local networkServerModuleConfig=${MODULE_CONFIG/"[\$spi_dev]"/"$spiDevValue"}
    networkServerModuleConfig=${networkServerModuleConfig/"[\$reset_pin]"/"$RESET_PIN"}
    networkServerModuleConfig=${networkServerModuleConfig/"[\$spi_speed]"/"$spiSpeedValue"}
    networkServerModuleConfig=${networkServerModuleConfig/"[\$TWIN_FACADE_SERVER_URL]"/"$FACADE_SERVER_URL"}
    networkServerModuleConfig=${networkServerModuleConfig/"[\$TWIN_FACADE_AUTH_CODE]"/"$FACADE_AUTH_CODE"}
    networkServerModuleConfig=${networkServerModuleConfig/"[\$TWIN_HOST_ADDRESS]"/"$LNS_HOST_ADDRESS"}
    networkServerModuleConfig=${networkServerModuleConfig/"[\$TWIN_NETWORK]"/"$NETWORK"}

    # Write deployment manifest to file and deploy it
    echo "Setting modules in gateway device $EDGE_GATEWAY_NAME..."

    az iot edge set-modules --device-id "$EDGE_GATEWAY_NAME" --hub-name "$IOTHUB_NAME" --content "$networkServerModuleConfig"

    echo "Setting tags in gateway device $EDGE_GATEWAY_NAME..."
    gatewayTags=$(cat <<EOF
{"network":"$NETWORK","lora_device_type": "gateway"}
EOF
)
    az iot hub device-twin update --hub-name "$IOTHUB_NAME" --device-id "$EDGE_GATEWAY_NAME" --tags "$gatewayTags"
}

create_monitoring_deployment_layer () {

    # Gets the layer manifest
    # - in bicep the template will be passed as environment variable
    # - when running locally we read directly from the file
    if [ -z "$MONITORING_LAYER_CONFIG" ]; then
        MONITORING_LAYER_CONFIG=$(cat observabilityLayerManifest.json)
    fi

    MONITORING_LAYER_CONFIG=${MONITORING_LAYER_CONFIG/"[\$iot_hub_resource_id]"/"$IOTHUB_RESOURCE_ID"}
    MONITORING_LAYER_CONFIG=${MONITORING_LAYER_CONFIG/"[\$log_analytics_workspace_id]"/"$LOG_ANALYTICS_WORKSPACE_ID"}
    MONITORING_LAYER_CONFIG=${MONITORING_LAYER_CONFIG/"[\$log_analytics_shared_key]"/"$LOG_ANALYTICS_SHARED_KEY"}

    echo "Setting monitoring layer for gateway device $EDGE_GATEWAY_NAME..."
    az iot edge deployment create --deployment-id "obs-deployment-$RANDOM" --hub-name "$IOTHUB_NAME" --layered true --target-condition "deviceId='$EDGE_GATEWAY_NAME'" --content "$MONITORING_LAYER_CONFIG"
}

create_concentrator_device () {
    # Add concentrator device
    echo "Creating concentrator device $STATION_DEVICE_NAME located at resourceGroup=$RESOURCE_GROUP, IotHub=$IOTHUB_NAME..."
    az iot hub device-identity create --device-id "$STATION_DEVICE_NAME" --am shared_private_key --hub-name "$IOTHUB_NAME" --resource-group "$RESOURCE_GROUP"

    # Gets the concentrator device properties
    if [ "$REGION" = "eu" ]; then
        if [ -f "./EU863.json" ]; then
            CONCENTRATOR_TWIN=$(cat ./EU863.json)
        else
            CONCENTRATOR_TWIN=$(cat ../Tools/Cli-LoRa-Device-Provisioning/DefaultRouterConfig/EU863.json)
        fi
    elif [ "$REGION" = "us" ]; then
        if [ -f "./US902.json" ]; then
            CONCENTRATOR_TWIN=$(cat ./US902.json)
        else
            CONCENTRATOR_TWIN=$(cat ../Tools/Cli-LoRa-Device-Provisioning/DefaultRouterConfig/US902.json)
        fi
    fi

    echo "Setting concentrator device $STATION_DEVICE_NAME twin properties..."
    local concentratorTags=$(cat <<EOF
{"network":"$NETWORK","lora_device_type": "concentrator", "lora_region": "$REGION"}
EOF
)
    az iot hub device-twin update --hub-name "$IOTHUB_NAME" --device-id "$STATION_DEVICE_NAME" --desired "$CONCENTRATOR_TWIN" --tags "$concentratorTags"
}

create_end_devices () {

    local OTAA_DEVICE_ID="47AAC86800430028"
    echo "Creating OTAA device $OTAA_DEVICE_ID..."
    az iot hub device-identity create --device-id "$OTAA_DEVICE_ID" --am shared_private_key --hub-name "$IOTHUB_NAME" --resource-group "$RESOURCE_GROUP"

    local TWINS=$(cat <<EOF
{"AppEUI":"BE7A0000000014E2","AppKey": "8AFE71A145B253E49C3031AD068277A1", "GatewayID": "", "SensorDecoder": "DecoderValueSensor"}
EOF
)

    local TAGS=$(cat <<EOF
{"network":"$NETWORK","lora_device_type": "leaf"}
EOF
)
    az iot hub device-twin update --hub-name "$IOTHUB_NAME" --device-id "$OTAA_DEVICE_ID" --desired "$TWINS" --tags "$TAGS"

    local ABP_DEVICE_ID="46AAC86800430028"
    echo "Creating ABP device $ABP_DEVICE_ID..."
    az iot hub device-identity create --device-id "$ABP_DEVICE_ID" --am shared_private_key --hub-name "$IOTHUB_NAME" --resource-group "$RESOURCE_GROUP"

    TWINS=$(cat <<EOF
{"AppSKey": "2B7E151628AED2A6ABF7158809CF4F3C", "NwkSKey": "3B7E151628AED2A6ABF7158809CF4F3C", "GatewayID": "", "DevAddr": "0228B1B1", "SensorDecoder": "DecoderValueSensor"}
EOF
)
    az iot hub device-twin update --hub-name "$IOTHUB_NAME" --device-id "$ABP_DEVICE_ID" --desired "$TWINS" --tags "$TAGS"
}


# Ensure IoT extension is installed
az extension add --name azure-iot

# Setting default values
STATION_DEVICE_NAME=${STATION_DEVICE_NAME:-AA555A0000000101}
REGION=${REGION:-eu}
NETWORK=${NETWORK-quickstartnetwork}

create_gateway_device

if [ "${MONITORING_ENABLED}" = "1" ]; then
    create_monitoring_deployment_layer
fi

create_concentrator_device

if [ "${DEPLOY_DEVICE}" = "1" ]; then
    create_end_devices
fi
