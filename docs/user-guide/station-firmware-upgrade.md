# Firmware upgrade

The Starterkit offers the functionality of performing a firmware upgrade of the
Basics Station. This document explains which steps are required to execute such
an upgrade.

1. When provisioning a concentrator device that will require future firmware
   upgrades, you will need to generate a signature key and store it on the
   device and in a centralized repository of your choice. You will also need to
   generate a CRC32 checksum of the key and a digest of your firmware upgrade
   executable. You can use the [CUPS Protocol - Firmware Upgrade
   Preparation][cups-firmware-upgrade] tool which generates all these values,
   given a Station EUI and a firmware upgrade file.

1. Use LoRa Device Provisioning CLI tool to trigger the upgrade.

   ```powershell
   dotnet .\Tools\Cli-LoRa-Device-Provisioning\bin\Release\netcoreapp3.1\loradeviceprovisioning.dll upgrade-firmware --stationeui <station_eui> --package <package_version> --firmware-location <firmware_file_path> --digest-location <digest_file_path> --checksum-location <checksum_file_path>
   ```

   Parameters:

   - `stationeui` - `StationEui` of the target device
   - `package` - new version of the Station, e.g. `1.0.1`
   - `firmware-location` - local path of the firmware upgrade executable file
   - `digest-location` - local path of the file containing the generated digest
     of the executable file of the upgrade
   - `checksum-location` - local path of the file containing the CRC32 checksum
     of the key used to generate the digest

   The LoRa Device Provisioning CLI tool will trigger the upgrade by uploading
   the firmware to a storage account and updating the device twin of the
   concentrator with required data.

   For more information about using the LoRa Device Provisioning CLI tool please
   refer to the [LoRa Device Provisioning](../tools/device-provisioning.md#upgrade-firmware) tool
   documentation.

1. During the next startup of the system, the Station will execute the upgrade
   after receiving the updated information from the Network Server. A system
   downtime is to be expected in order for the upgrade to complete. After the
   upgrade is finished, the current version of the Basics Station can be found
   in the desired reported properties of the concentrator twin in IoT Hub.

[cups-firmware-upgrade]:
    https://github.com/Azure/iotedge-lorawan-starterkit/tree/dev/Tools/Cups-Firmware-Upgrade