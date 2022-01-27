# Firmware upgrade with CUPS

The Azure IoT Edge LoRaWAN Starter Kit offers the functionality of performing a
firmware upgrade of the Basics Station. This document explains which steps are
required to execute such upgrade.

1. When provisioning a concentrator device that will require future firmware
   upgrades, you will need to generate a *signature key* and store it on the
   device and in a centralized repository of your choice.  
   During the update process, CUPS Protocol is using the CRC32 checksum of
   the signature key on the device to compare the digest of the firmware
   upgrade executable generated with such key.  
   You can use the
   [CUPS Protocol - Firmware Upgrade Preparation](../tools/cups-firmware-file-preparation.md)
   tool to generate all needed files.

1. Use LoRa Device Provisioning CLI tool to upload the upgrade files in the cloud.

   ```powershell
   dotnet .\Tools\Cli-LoRa-Device-Provisioning\bin\Release\net6.0\loradeviceprovisioning.dll upgrade-firmware 
    --stationeui <station_eui> 
    --package <package_version> 
    --firmware-location <firmware_file_path> 
    --digest-location <digest_file_path> 
    --checksum-location <checksum_file_path>
    --iothub-connection-string <iothub_connection_string> 
    --storage-connection-string <storage_connection_string>
   ```

   Parameters:

   - `stationeui` - `StationEui` of the target device
   - `package` - new version of the Station, e.g. `1.0.1`
   - `firmware-location` - local path of the firmware upgrade executable file
   - `digest-location` - local path of the file containing the generated digest
     of the executable file of the upgrade
   - `checksum-location` - local path of the file containing the CRC32 checksum
     of the key used to generate the digest

   The LoRa Device Provisioning CLI tool will save the upgrade data by uploading
   the firmware to a storage account and updating the device twin of the
   concentrator with required data.

   For more information about using the LoRa Device Provisioning CLI tool please
   refer to the [LoRa Device Provisioning](../tools/device-provisioning.md#upgrade-firmware) tool
   documentation.

1. During the next reconnection of the Basics Station to the CUPS endpoint,
   it will execute the upgrade after receiving the updated information from the
   Network Server.  
   A system downtime is to be expected in order for the upgrade to complete.  
   After the upgrade is finished, and the Basics Station is reconnecting to the
   LNS Data endpoint, the updated version of the Basics Station will be reported
   in the properties of the concentrator twin in IoT Hub.
