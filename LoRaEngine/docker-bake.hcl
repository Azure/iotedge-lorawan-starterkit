
group "default" {
    targets = ["lorawannwksrv"]
}

target "lorawannwksrv" {
    context = ".."
    dockerfile = "LoRaEngine/modules/LoRaWanNetworkSrvModule/Dockerfile.amd64"
    tags = ["docker.io/username/webapp"]
    platforms = ["linux/amd64", "linux/arm/v7"]
}
