# LoRa Device Provisioning

This is a Command Line Interface Provisioning Tool to list, query, verify add,
update, and remove LoRaWAN leaf devices and LoRaWAN Basic Station devices
configured in Azure IoT Hub for the Azure IoT Edge LoRaWAN Gateway project
located at: <http://aka.ms/lora>

## Building

You can create an platform specific executable by running

```powershell
dotnet publish -c Release -r win10-x64
dotnet publish -c Release -r linux-x64
dotnet publish -c Release -r osx-x64
```

See the [.NET Core RID Catalog
](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog) for a list of valid
runtime identifiers.

## Running

You can run the tool from the command line using .NET Core by executing the
dotnet run command from the project folder

```powershell
dotnet run -- (add verbs and parameters here)
```

or dotnet loradeviceprovisioning.dll from the bin folder.

```powershell
dotnet .\bin\Release\net6.0\loradeviceprovisioning.dll -- (add verbs and parameters here)
```

## Setting up

The tool can be configured through command line arguments and an
`appsettings.json` file.

Required command line arguments are specific to the command the user wants to
perform and are described later in this document. `iothub-connection-string`
parameter is required for all commands and needs to contain a connection string
for the Azure IoT Hub you want to work with. The connection string needs to
belong to a shared access policy with **registry read**, **registry write** and
**service connect** permissions enabled. You can use the default policy named
**iothubowner**.

[appsettings.json](https://github.com/Azure/iotedge-lorawan-starterkit/blob/dev/Tools/Cli-LoRa-Device-Provisioning/appsettings.json)
file only contains the Network Id (`NetId`) value which can be optionally
overridden, in case your solution does not use the default Network Id 000001. To
set your custom Network Id, create a local `appsettings.local.json` file with
your own `NetId` value. Since just the last byte from this 3 hex string byte
array (6 characters) are used to create a valid DevAddr for ABP LoRa devices,
the setting can be either the full 3 bytes (000000 to FFFFFF) or just the
shortened, last byte (0 to FF). Both appsettings files need to be in the same
directory as the cli-lora-device-provisioning binary
(`loradeviceprovisioning.dll` or `loradeviceprovisioning.exe`).

```json
{
  "NetId": "000001"
}
```

The `NetId` value can also be overridden with a command line argument (using
`--netid` option).

To learn more about what each of the settings in the LoRa device twin does,
refer to the [Quick Start Guide](../quickstart.md#optional-device-properties).

## Supported commands

The following verbs/commands are supported:

|verb|description|
|-|-|
|list|Lits devices in IoT Hub.|
|query|Query a device twin.|
|verify|Verify a single device in IoT Hub.|
|bulkverify|Bulk verify all devices in IoT Hub.|
|add|Add a new device to IoT Hub.|
|update|Update an existing device in IoT Hub.|
|remove|Remove an existing device from IoT Hub.|
|upgrade-firmware|Trigger a firmware upgrade of a Basics Station.|
|help|Display more information on a specific command.|
|version|Display version information.|

### list

List the devices in IoT Hub and show their device twin.

Example:

```powershell
dotnet run -- list --iothub-connection-string <connection_string>
```

The list verb supports the following parameters:

|parameter|required|description|
|-|-|-|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--page|no|Devices per page. Default is 10.|
|--total|no|Maximum number of devices to list. Default is all.|
|--help|no|Display this help screen.|
|--version|no|Display version information.|

### query

Show the device twin for an existing device in IoT Hub by it's DevEUI / Device
Id.

Example:

```powershell
dotnet run -- query --deveui 33CCC86800430010 --iothub-connection-string <connection_string>
```

The query verb supports the following parameters:

|parameter|required|description|
|-|-|-|
|--deveui|yes|DevEUI / Device Id.|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--help|no|Display this help screen.|
|--version|no|Display version information.|

### verify

Verify an existing device in IoT Hub by it's DevEUI / Device Id.

Example:

```powershell
dotnet run -- verify --deveui 33CCC86800430010 --iothub-connection-string <connection_string>
```

The verify verb supports the following parameters:

|parameter|required|description|
|-|-|-|
|--deveui|yes|DevEUI / Device Id.|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--netid|no|Network ID (Only for ABP devices): A 3 bit hex string. Will default to 000001 or NetId set in settings file if left blank.|
|--help|no|Display this help screen.|
|--version|no|Display version information.|

### bulkverify

Bulk verify all devices in IoT Hub. Only shows the devices that have
configuration errors and displays a summary in the end how many devcies are
properly  configured and how many contain errors.

Example:

```powershell
dotnet run -- bulkverify --page 10 --iothub-connection-string <connection_string>
```

The bulkverify verb supports the following parameters:

|parameter|required|description|
|-|-|-|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--page|no|Errors per page. Default is all.|
|--help|no|Display this help screen.|
|--version|no|Display version information.|

### add

Add a new device to IoT Hub. The data entered will be verified and only created
in IoT Hub if it's valid. All required fields that are not provided will be
automatically populated with valid, randomly generated by the tool. The only
mandatory field is `type` which has to be set to either `ABP` or `OTAA` or
`concentrator`.

To learn more about what each of the settings in the LoRa device twin does,
refer to the [Quick Start Guide](../quickstart.md#optional-device-properties).

Example:

```powershell
dotnet run -- add --type abp --deveui 33CCC86800430010 --decoder http://decodermodule/api/customdecoder --iothub-connection-string <connection_string>
```

The add verb supports the following parameters:

|parameter|required|description|
|-|-|-|
|--type|yes|Device type: Must be ABP, OTAA or Concentrator.|
|--region|yes for 'Concentrator' type devices| Must be the name of one of the regions in the DefaultRouterConfig folder.|
|--stationeui|yes for 'Concentrator' type devices| A 16 bit hex string ('AABBCCDDEEFFGGHH').|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--no-cups|no|No CUPS: Only applicable to 'Concentrator' type devices. If set to true indicates that the concentrator does not use CUPS.|
|--certificate-bundle-location|no|Certificate bundle location: Required if --no-cups set to false. Points to the location of the (UTF-8-encoded) certificate bundle file.|
|--client-certificate-thumbprint|no|Client certificate thumbprint: Required if --no-cups set to false. A list of client certificate thumbprints (separated by a space) that should be accepted by the CUPS/LNS endpoints.|
|--tc-uri|no|LoRaWAN Network Server URI: Required if --no-cups set to false. The URI of the LNS endpoint. Protocol must be set to wss://|
|--cups-uri|no|LoRaWAN Network Server URI: Required if --no-cups set to false. The URI of the CUPS endpoint. Protocol must be set to https://|
|--deveui|no|DevEUI / Device Id: A 16 bit hex string. Will be randomly generated if left blank.|
|--appskey|no|AppSKey (Only for ABP devices): A 16 bit hex string. Will be randomly generated if left blank.|
|--nwkskey|no|NwkSKey (Only for ABP devices): A 16 bit hex string. Will be randomly generated if left blank.|
|--devaddr|no|DevAddr (Only for ABP devices): A 4 bit hex string. Will be randomly generated if left blank.|
|--netid|no|Network ID (Only for ABP devices): A 3 bit hex string. Will default to 000001 or NetId set in settings file if left blank.|
|--abprelaxmode|no|ABPRelaxMode (ABP relaxed framecounter, only for ABP devices): True or false. |
|--appeui|no|AppEUI (only for OTAA devices): A 16 bit hex string. Will be randomly generated if left blank.|
|--appkey|no|AppKey (only for OTAA devices): A 16 bit hex string. Will be randomly generated if left blank.|
|--gatewayid|no|GatewayID: A hostname. |
|--decoder|no|SensorDecoder: The name of an integrated decoder function or the URI to a decoder in a custom decoder module in the format: <http://modulename/api/decodername.> |
|--classtype|no|ClassType: "A" (default) or "C". |
|--downlinkenabled|no|DownlinkEnabled: True or false. |
|--preferredwindow|no|PreferredWindow (Preferred receive window): 1 or 2. |
|--deduplication|no|Deduplication: None (default), Drop or Mark. |
|--rx2datarate|no|Rx2DataRate (Receive window 2 data rate, currently only supported for OTAA devices): Any of the allowed data rates. EU: SF12BW125, SF11BW125, SF10BW125, SF8BW125, SF7BW125, SF7BW250 or 50. US: SF10BW125, SF9BW125, SF8BW125, SF7BW125, SF8BW500, SF12BW500, SF11BW500, SF10BW500, SF9BW500, SF8BW500, SF8BW500.|
|--rx1droffset|no|Rx1DrOffset (Receive window 1 data rate offset, currently only supported for OTAA devices): 0 through 15.|
|--rxdelay|no|RxDelay (Delay in seconds for sending downstream messages, currently only supported for OTAA devices): 0 through 15.|
|--keepalivetimeout|no|KeepAliveTimeout (KeepAliveTimeout (Timeout in seconds before device client connection is closed): 0 or 60 and above.|
|--supports32bitfcnt|no|Supports32BitFCnt (Support for 32bit frame counter): True or false.|
|--fcntupstart|no|FCntUpStart (Frame counter up start value): 0 through 4294967295.|
|--fcntdownstart|no|FCntDownStart (Frame counter down start value): 0 through 4294967295.|
|--fcntresetcounter|no|FCntResetCounter (Frame counter reset counter value): 0 through 4294967295.|
|--help|no|Display this help screen.|
|--version|no|Display version information.|

### update

Update the twin information for an existing device.

To remove an existing value that is currently set on the twin, pass it the value
`null`.

To learn more about what each of the settings in the LoRa device twin does,
refer to the [Quick Start Guide](../quickstart.md#optional-device-properties).

Example:

```powershell
dotnet run -- update --deveui 33CCC86800430010 --decoder null --iothub-connection-string <connection_string>
```

The update verb supports the following parameters:

|parameter|required|description|
|-|-|-|
|--deveui|yes|DevEUI / Device Id: A 16 bit hex string.|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--appskey|no|AppSKey (Only for ABP devices): A 16 bit hex string.|
|--nwkskey|no|NwkSKey (Only for ABP devices): A 16 bit hex string.|
|--devaddr|no|DevAddr (Only for ABP devices): A 4 bit hex string.|
|--netid|no|Network ID (Only for ABP devices): A 3 bit hex string. Will default to 000001 or NetId set in settings file if left blank.|
|--abprelaxmode|no|ABPRelaxMode (ABP relaxed framecounter, only for ABP devices): True or false. |
|--appeui|no|AppEUI (only for OTAA devices): A 16 bit hex string.|
|--appkey|no|AppKey (only for OTAA devices): A 16 bit hex string.|
|--gatewayid|no|GatewayID: A hostname. |
|--decoder|no|SensorDecoder: The name of an integrated decoder function or the URI to a decoder in a custom decoder module in the format: <http://modulename/api/decodername>. |
|--classtype|no|ClassType: "A" (default) or "C". |
|--downlinkenabled|no|DownlinkEnabled: True or false. |
|--preferredwindow|no|PreferredWindow (Preferred receive window): 1 or 2. |
|--deduplication|no|Deduplication: None (default), Drop or Mark. |
|--rx2datarate|no|Rx2DataRate (Receive window 2 data rate, currently only supported for OTAA devices): Any of the allowed data rates. EU: SF12BW125, SF11BW125, SF10BW125, SF8BW125, SF7BW125, SF7BW250 or 50. US: SF10BW125, SF9BW125, SF8BW125, SF7BW125, SF8BW500, SF12BW500, SF11BW500, SF10BW500, SF9BW500, SF8BW500, SF8BW500.|
|--rx1droffset|no|Rx1DrOffset (Receive window 1 data rate offset, currently only supported for OTAA devices): 0 through 15.|
|--rxdelay|no|RxDelay (Delay in seconds for sending downstream messages, currently only supported for OTAA devices): 0 through 15.|
|--keepalivetimeout|no|KeepAliveTimeout (KeepAliveTimeout (Timeout in seconds before device client connection is closed): 0 or 60 and above.|
|--supports32bitfcnt|no|Supports32BitFCnt (Support for 32bit frame counter): True or false. |
|--fcntupstart|no|FCntUpStart (Frame counter up start value): 0 through 4294967295.|
|--fcntdownstart|no|FCntDownStart (Frame counter down start value): 0 through 4294967295.|
|--fcntresetcounter|no|FCntResetCounter (Frame counter reset counter value): 0 through 4294967295.|
|--help|no|Display this help screen.|
|--version|no|Display version information.|

### remove

Remove an existing device from IoT Hub by it's DevEUI / Device Id.

Example:

```powershell
dotnet run -- remove --deveui 33CCC86800430010 --iothub-connection-string <connection_string>
```

The query verb supports the following parameters:

|parameter|required|description|
|-|-|-|
|--deveui|yes|DevEUI / Device Id.|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--help|no|Display this help screen.|
|--version|no|Display version information.|

### upgrade-firmware

Triggers a firmware upgrade of a Basics Station.

To learn more about executing firmware upgrades, please refer to the [Firmware
upgrade](../user-guide/station-firmware-upgrade.md) user guide.

Example:

 ```powershell
   dotnet run -- upgrade-firmware 
    --stationeui <station_eui> 
    --package <package_version> 
    --firmware-location <firmware_file_path> 
    --digest-location <digest_file_path> 
    --checksum-location <checksum_file_path> 
    --iothub-connection-string <iothub_connection_string> 
    --storage-connection-string <storage_connection_string>
```

The upgrade-firmware verb accepts the following parameters:

|parameter|required|description|
|-|-|-|
|--stationeui|yes|Station EUI|
|--package|yes|New package version (e.g. `1.0.1`)|
|--firmware-location|yes|Local file path of the firmware upgrade executable|
|--digest-location|yes|Local file path of the file containing a digest of the firmware upgrade|
|--checksum-location|yes|Local file path of the file containing a CRC32 checksum of the key used to generate the digest|
|--iothub-connection-string|yes|Connection string of the IoT Hub.|
|--storage-connection-string|yes|Connection string of the Storage account.|
|--help|no|Display this help screen|
|--version|no|Display version information|
