variable "NET_SRV_VERSION" {
    default = "0.0.1"
}

variable "LBS_VERSION" {
    default = "0.0.1"
}

variable "CONTAINER_REGISTRY_ADDRESS" {
}

group "default" {
    targets = ["LoRaWanNetworkServerx64", "LoRaWanNetworkServerarm32", "LoraBasicsStationx64", "LoraBasicsStationarm32v7"]
}

target "LoRaWanNetworkServer" {
    context = ".."
    tags = ["${CONTAINER_REGISTRY_ADDRESS}/lorawannetworksrvmodule:${NET_SRV_VERSION}"]
}

target "LoraBasicsStation" {
    context = ".."
    tags = ["${CONTAINER_REGISTRY_ADDRESS}/lorabasicsstation:${NET_SRV_VERSION}"]
}

target "LoRaWanNetworkServerx64" {
    inherits = ["LoRaWanNetworkServer"]
    dockerfile = "LoRaEngine/modules/LoRaWanNetworkSrvModule/Dockerfile.amd64"
    tags = ["${CONTAINER_REGISTRY_ADDRESS}/lorawannetworksrvmodule:${NET_SRV_VERSION}-amd64"]
    platforms = ["linux/amd64"]
}

target "LoRaWanNetworkServerarm32" {
    inherits = ["LoRaWanNetworkServer"]
    dockerfile = "LoRaEngine/modules/LoRaWanNetworkSrvModule/Dockerfile.arm32v7"
    tags = ["${CONTAINER_REGISTRY_ADDRESS}/lorawannetworksrvmodule:${NET_SRV_VERSION}-arm32v7"]
    platforms = ["linux/arm/v7"]
}

target "LoraBasicsStationx64" {
    inherits = ["LoraBasicsStation"]
    dockerfile = "LoRaEngine/modules/LoRaBasicsStationModule/Dockerfile.amd64"
    tags = ["${CONTAINER_REGISTRY_ADDRESS}/lorabasicsstation:${LBS_VERSION}-amd64"]
    platforms = ["linux/amd64"]
}

target "LoraBasicsStationarm32v7" {
    inherits = ["LoraBasicsStation"]
    dockerfile = "LoRaEngine/modules/LoRaBasicsStationModule/Dockerfile.arm32v7"
    tags = ["${CONTAINER_REGISTRY_ADDRESS}/lorabasicsstation:${LBS_VERSION}-arm32v7"]
    platforms = ["linux/arm/v7"]
}
