# CUPS - Firmware update file preparation

The ['firmware update file preparation' bash script](https://github.com/Azure/iotedge-lorawan-starterkit/blob/ba6dfbc57aee5c72b9378516c555f104d5aa03b7/Tools/Cups-Firmware-Upgrade/firmwarePrep.sh) is helping Azure IoT Edge LoRaWAN Starter Kit users to generate the files needed for executing a firmware upgrade.

More information on how to execute a firmware update can be found in ['Firmware upgrade'](../user-guide/station-firmware-upgrade.md) section of this documentation.

Usage: `./firmwarePrep.sh stationEui firmwareUpgradeFilePath`

Arguments:

- `stationEui` (REQUIRED) EUI of the target Basics Station
- `firmwareUpgradeFilePath` (REQUIRED) The path of the binary to be executed on Basics Station for upgrading the firmware

The tool will generate three output files being:

- A `sig-0.key` to be placed on the Basics Station
- A `sig-0.crc` containing the checksum of the sig-0.key. This is required to understand which key generated the digest of the firmware upgrade file. This value has to be saved in the IoT Hub Device Twin for the Station, using the Device Provisioning tool.
- A `fwUpdate.digest` file containing the base64 encoding of the digest computed for the firmware upgrade file. This value has to be saved in the IoT Hub Device Twin for the Station, using the Device Provisioning tool.
