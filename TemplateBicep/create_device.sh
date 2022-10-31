create_devices_with_lora_cli() {
    echo "Downloading lora-cli from $LORA_CLI_URL..."
    curl -SsL "$LORA_CLI_URL" -o lora-cli.tar.gz
    mkdir -p lora-cli && tar -xzf ./lora-cli.tar.gz -C ./lora-cli

    cd lora-cli
    chmod +x ./loradeviceprovisioning

    local monitoringEnabled="false"
    if [ "${MONITORING_ENABLED}" = "1" ]; then
        monitoringEnabled="true"
    fi

    echo "Creating gateway $EDGE_GATEWAY_NAME..."
    ./loradeviceprovisioning add-gateway --reset-pin "$RESET_PIN" --device-id "$EDGE_GATEWAY_NAME" --spi-dev "$SPI_DEV" --spi-speed "$SPI_SPEED" --api-url "$FACADE_SERVER_URL" --api-key "$FACADE_AUTH_CODE" --lns-host-address "$LNS_HOST_ADDRESS" --network "$NETWORK" --monitoring "$monitoringEnabled" --iothub-resource-id "$IOTHUB_RESOURCE_ID" --log-analytics-workspace-id "$LOG_ANALYTICS_WORKSPACE_ID" --log-analytics-shared-key "$LOG_ANALYTICS_SHARED_KEY" --lora-version "$LORA_VERSION"

    echo "Creating concentrator $STATION_DEVICE_NAME for region $REGION..."
    ./loradeviceprovisioning add --type concentrator --region "$REGION" --stationeui "$STATION_DEVICE_NAME" --no-cups --network "$NETWORK"

    # add leaf devices
    if [ "${DEPLOY_DEVICE}" = "1" ]; then
        echo "Creating leaf devices 46AAC86800430028 and 47AAC86800430028..."
        ./loradeviceprovisioning add --type abp --deveui "46AAC86800430028" --appskey "2B7E151628AED2A6ABF7158809CF4F3C" --nwkskey "3B7E151628AED2A6ABF7158809CF4F3C" --devaddr "0228B1B1" --decoder "DecoderValueSensor" --network "$NETWORK"
        ./loradeviceprovisioning add --type otaa --deveui "47AAC86800430028" --appeui "BE7A0000000014E2" --appkey "8AFE71A145B253E49C3031AD068277A1" --decoder "DecoderValueSensor" --network "$NETWORK"
    fi
}

# Setting default values
STATION_DEVICE_NAME=${STATION_DEVICE_NAME:-AA555A0000000101}
REGION=${REGION:-EU863}
NETWORK=${NETWORK-quickstartnetwork}
LNS_HOST_ADDRESS=${LNS_HOST_ADDRESS-ws://mylns:5000}
SPI_DEV=${SPI_DEV-0}
SPI_SPEED=${SPI_SPEED-8}

create_devices_with_lora_cli
