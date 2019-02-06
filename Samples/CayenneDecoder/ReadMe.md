# Azure IoT Edge LoRaWAN Starter Kit

## Cayenne Decoder

This sample allows you to create and run your own LoRa message decoder in an independent container running on your LoRa gateway without having to edit the main LoRa Engine. It is based on the sample decoder. 

## Supported Cayenne devices

This decode takes a Lora payload and decode it based on the [Cayenne encoding specification](https://github.com/myDevicesIoT/cayenne-docs/blob/master/docs/LORA.md).

The payload is decoded and transformed into a json object.

The payload ```AWcA5gJoMANzJigEZQD9``` will then be transformed as:

```json
{"value":{"IlluminanceSensor":{"Channel":4,"Value":253},"TemperatureSensor":{"Channel":1,"Value":23.0},"HumiditySensor":{"Channel":2,"Value":24.0},"Barometer":{"Channel":3,"Value":976.8}}}
```

All Cayenne devices are supported.