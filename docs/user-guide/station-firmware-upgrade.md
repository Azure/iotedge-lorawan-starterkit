# Firmware upgrade

The Starterkit offers the functionality of performing a firmware upgrade of the
Basics Station. This document explains which steps are required to execute such
an upgrade.

1. When provisioning a concentrator device that will require future firmware
   upgrades, generate a signature key and store it on the device and in a
   centralized repository of your choice.

   ```shell title="Example for how to generate a signature key (sig-0.key) with openssl"
   openssl ecparam -name prime256v1 -genkey | openssl ec -out sig-0.pem
   openssl ec -in sig-0.pem -pubout -out sig-0.pub
   openssl ec -in sig-0.pub -inform PEM -outform DER -pubin | tail -c 64 > sig-0.key
   ```

1. When a firmware upgrade of the Station is needed, generate a digest of the
   executable file of the upgrade.
   The digest of an upgrade file `upgrade.sh` can be calculated using the previously
   generated signature key using the command:

   ```shell
   openssl dgst -sha512 -sign sig-0.pem update.sh > update.sh.sig-0.sha512
   ```

1. Retrieve the CRC32 Checksum of the signature key

   ```shell
   cat sig-0.key | gzip -1 | tail -c 8 | od -t ${1:-u}4 -N 4 -An --endian=little | xargs echo > sig-0.crc
   ```

1. Use LoRa Device Provisioning CLI tool to trigger the upgrade

   ```powershell
   dotnet run -- upgrade --deveui <station_eui> --version <version> --file <upgrade_file> --digest <file_digest> --checksum <checksum>
   ```

   Parameters:

   - `deveui` - `StationEui` of the target device
   - `version` - new version of the Station, e.g. `1.0.1`
   - `file` - path to the firmware upgrade executable file
   - `digest` - generated digest of the executable file of the upgrade
   - `checksum` - CRC32 checksum of the key used to generate the digest

   The LoRa Device Provisioning CLI tool will trigger the upgrade by uploading the
   firmware to a storage account and updating the device twin of the concentrator
   with required data.

1. During the next startup of the system, the Station will execute the upgrade
   after receiving the updated information from the Network Server. A system
   downtime is to be expected in order for the upgrade to complete. After the
   upgrade is finished, the current version of the Basics Station can be found
   in the desired reported properties of the concentrator twin in IoT Hub.
