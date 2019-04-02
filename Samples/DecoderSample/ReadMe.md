# Azure IoT Edge LoRaWAN Starter Kit
## DecoderSample

This sample allows you to create and run your own LoRa message decoder in an independent container running on your LoRa gateway without having to edit the main LoRa Engine. This description shows you how to get started.

### Customizing

To add a new decoder, simply copy or reuse  the sample ```DecoderValueSensor``` method from the ```LoraDecoders``` class in [LoraDecoder.cs](/Samples/DecoderSample/Classes/LoraDecoder.cs). You can name the method whatever you like and can create as many decoders as you need by adding new, individual methods to the ```LoraDecoders``` class.

The payload sent to the decoder is passed as string ```devEui```, byte[] ```payload``` and uint ```fport```.

After writing the code that decodes your message, your method should return a **string containing valid JSON** containing the response to be sent upstream.

```cs
internal static class LoraDecoders
{
    private static string DecoderValueSensor(string devEUI, byte[] payload, byte fport)
    {
        // EITHER: Convert a payload containing a string back to string format for further processing
        var result = Encoding.UTF8.GetString(payload);

        // OR: Convert a payload containing binary data to HEX string for further processing
        var result_binary = ConversionHelper.ByteArrayToString(payload);

        // Write code that decodes the payload here.

        // Return a JSON string containing the decoded data
        return JsonConvert.SerializeObject(new { value = result });

    }
}
```

You can test the decoder on your machine by debugging the SensorDecoderModule project in Visual Studio. 

When creating a debugging configuration in Visual Studio Code or a ```launchSettings.json``` file in Visual Studio, the default address that the webserver will try to use is ```http://localhost:5000``` or ```https://localhost:5001```. You can override this with any port of your choice.

On launching the debugger you will see a webbrowser with a ```404 Not Found``` error as there is no default document to be served in this Web API app.

You will also manually need to [base64-encode](https://www.base64encode.org/) and [URL-encode](https://www.urlencoder.org/) the payload before adding it to the URL parameters.

For example, to test a payload of `ABCDE12345`, you:
- Convert it to a base64 encoded string: `QUJDREUxMjM0NQ==`
- Convert the result to a valid URL parameter: `QUJDREUxMjM0NQ%3D%3D`
- Add this to your test URL.

For the built-in sample decoder ```DecoderValueSensor``` with Visual Studio (Code)'s default settings this would be:

```
http://localhost:5000/api/DecoderValueSensor?devEui=0000000000000000&fport=1&payload=QUJDREUxMjM0NQ%3D%3D
`````
You can call your decoder at:

```
http://localhost:yourPort/api/<decodername>?devEui=0000000000000000&fport=1&payload=<QUJDREUxMjM0NQ%3D%3D>
```

You should see the result as JSON string.

![Decoder Sample - Debugging on localhost](/Docs/Pictures/decodersample-debugging.png)

When running the solution in a container, the [Kestrel webserver](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-2.1) from .NET Core uses the HTTP default port 80 of the container and does not need to bind it to a port on the host machine as Docker allows for container-to-container communication. IoT Edge automatically creates the required [Docker Network Bridge](https://docs.docker.com/network/bridge/).

### Preparing and Testing the Docker Image

Create a docker image from your finished solution based on the target architecture and host it in an [Azure Container Registry](https://azure.microsoft.com/en-us/services/container-registry/), on DockerHub or in any other container registry of your choice.

We are using the [Azure IoT Edge for Visual Studio Code](https://marketplace.visualstudio.com/items?itemName=vsciot-vscode.azure-iot-edge) extension to build and push the Docker image.

Make sure you are logged in to the Azure Container Registry you are using. Run `docker login <mycontainerregistry>.azurecr.io` on your development machine.

Edit the file [module.json](./module.json) to contain your container registry address, image name and version number:

![Decoder Sample - module.json file](/Docs/Pictures/decodersample-module-json.png)

We provide the Dockerfiles for the following architectures:

- [Dockerfile.amd64](/Samples/DecoderSample/Dockerfile.amd64)
- [Dockerfile.arm32v7](/Samples/DecoderSample/Dockerfile.arm32v7)

To build the Docker image, right-click on the [module.json](./module.json) file and select "Build IoT Edge Module Image" or "Build and Push IoT Edge Module Image". Select the architecture you want to build for (ARM32v7 or AMD64) from the drop-down menu.

To **temporarily test** the container running you decoder using a webbrowser or Postman, you can manually start it in Docker and bind the container's port 80 to a free port on your host machine, like for example 8881.

```bash
docker run --rm -it -p 8881:80 --name decodersample <container registry>/<image>:<tag>
````

You can then use a browser to navigate to:

```
http://localhost:8881/api/DecoderValueSensor?devEui=0000000000000000&fport=1&payload=QUJDREUxMjM0NQ%3D%3D
```

### Deploying to IoT Edge

If required, add credentials to access your container registry to the IoT Edge device by adding them to IoT Hub &rarr; IoT Edge &rarr; Your Device &rarr; Set Modules &rarr; Container Registry settings.

![Decoder Sample - Edge Module Container Registry Permission](/Docs/Pictures/decodersample-edgepermission.png)

Configure your IoT Edge gateway device to include the custom container. IoT Hub &rarr; IoT Edge &rarr; Your Device &rarr; Set Modules &rarr; Deployment Modules &rarr; Add &rarr; IoT Edge Module. Set the module Name and Image URI, pointing to your image created above.

**Make sure to choose all lowercase letters for the Module Name as the container will be unreachable otherwise!**

![Decoder Sample - Edge Module](/Docs/Pictures/decodersample-edgemodule.png)

To activate the decoder for a LoRa device, navigate to your IoT Hub &rarr; IoT Devices &rarr; Device Details &rarr; Device Twin and set the ```SensorDecoder``` value in the desired properties to: 

```
http://<decoder module name>/api/<DecoderName>
```

**Again make sure to chosse all lowercase letters for the module name to make sure it is reachable.**

![Decoder Sample - LoRa Device Twin](/Docs/Pictures/decodersample-devicetwin.png)

In case the custom decoder is unreachable, throws an error or return invalid JSON, the error message will be shown in your device's messages in IoT Hub.
