# Azure IoT Edge LoRaWAN Starter Kit
## DecoderSample

This sample allows you to create and run your own LoRa message decoder in an independent container running on your LoRa gateway without having to edit the main LoRa Engine. This description shows you how to get started.

### Customizing

To add a new decoder, simply copy or reuse  the sample ```DecoderValueSensor``` method from the ```LoraDecoders``` class in [LoraDecoder.cs](/Samples/DecoderSample/Classes/LoraDecoder.cs). You can name the method whatever you like and can create as many decoders as you need by adding new, individual methods to the ```LoraDecoders``` class.

The payload sent to the decoder is passed as byte[] ```payload``` and uint ```fport```.

After writing the code that decodes your message, your method should return a **string containing valid JSON** containing the response to be sent upstream.

```cs
internal static class LoraDecoders
{   
    private static string DecoderValueSensor(byte[] payload, uint fport)
    {
        var result = Encoding.ASCII.GetString(payload);            
        return JsonConvert.SerializeObject(new { value = result });
    }
}
```

You can test the decoder locally by debugging the SensorDecoderModule project in Visual Studio and calling the decoder at:

```
http://machine:port/api/<decodername>?fport=<1>&payload=<ABCDE12345>
```

The built-in sample decoder can be debugged at:

```
http://localhost:8881/api/DecoderValueSensor?fport=1&payload=ABCDE12345
`````

### Deploying

Create a docker image from your finished solution based on the target architecture and host it in an [Azure Container Registry](https://azure.microsoft.com/en-us/services/container-registry/), on DockerHub or in any other container registry of your choice.

We provide the following Dockerfiles:

- [Dockerfile.amd64](/Samples/DecoderSample/Dockerfile.amd64)
- [Dockerfile.arm32v7](/Samples/DecoderSample/Dockerfile.arm32v7)

To test the container running you decoder using a webbrowser or Postman, you can manually start it in Docker and bind it's port 80 to a free port of your host machine.

```bash
docker run --rm -it -p 8881:80 --name decodersample <container registry>/<image>:<tag>
````

You can then use a browser to navigate to:

```
http://localhost:8881/api/DecoderValueSensor?fport=1&payload=ABCDE12345
```

If required, add credentials to access your container registry to the IoT Edge device by adding them to IoT Hub -> IoT Edge -> Your Device -> Set Modules -> Container Registry settings.

![Decoder Sample - Edge Module Container Registry Permission](/pictures/decodersample-edgepermission.png)

Configure your IoT Edge gateway device to include the custom container. IoT Hub -> IoT Edge -> Your Device -> Set Modules -> Deployment Modules -> Add -> IoT Edge Module. Set the module Name and Image URI, pointing to your image created above.

**Make sure to choose all lowercase letters for the Module Name as the container will be unreachable otherwise!**

![Decoder Sample - Edge Module](/pictures/decodersample-edgemodule.png)

To activate the decoder for a LoRa device, navigate to your IoT Hub -> IoT Devices -> Device Details -> Device Twin and set the ```SensorDecoder``` value in the desired properties to: 

```
http://<decoder module name>/api/<DecoderName>
```

**Again make sure to chosse all lowercase letters for the module name to make sure it is reachable.**

![Decoder Sample - LoRa Device Twin](/pictures/decodersample-devicetwin.png)

In case the custom decoder is unreachable, throws an error or return invalid JSON, the error message will be shown in your device's messages in IoT Hub.