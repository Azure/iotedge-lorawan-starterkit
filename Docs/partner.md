# Device Manufacturer Guidance
We have developed the LoRaWAN starter kit agnostic of a device manufacturer implementation and focussed on the specifics on underlying architectures (arm, x86). However, we understand that device manufacturers can have specific requirements to ensure the LoRa packets are processed and decoded correctly. In this section, we provide guidance for the following:
- **Gateway Manufacturers**: Allow a gateway to be compatible with our kit. Gateway manufacturers will test / make our Edge Hub modules work on their gateway.

- **LoRA Sensor Manufacturers**: These manufacturers will develop a custom decoder using our decoding framework for their sensors.
  
Please choose from a section that relates to your requirements.
## Device Gateway Manufacturer Guidance

The LoRaWAN starter kit currently is tested on many popular gateways, we run some of those as part of daily CI/CD pipeline to test integrity and performance of our codebase. However, we cannot test each and every gateway out there and hence we have created a process for device manufacturer to support their gateways and get them highlighted in this repo.

### Instructions
If you would like to test gateway compatibility with out starter kit and also get it highlighted on our GitHub page. To test gateway compatibility, please follow these steps:
- Go through the [Developer Guidance](/Docs/devguide.md) to clone the repo and make sure everything works in your local dev environment.
- Make sure everything works with an Azure subscription with Standard Pricing SKU's, for example we do not support the Free Azure IoT Hub SKU.
- Ensure that the gateway specification meet the minimal hardware configuration required for Azure IoT Edge and a container framework like Docker, Moby to run. We recommend at the minimum of 1 GB RAM, rPi based boards and similar configuration devices will be a good candidate for our starter kit.
- If the gateway requires a specific packet forwarder not provided by our kit (we leverage an implementation of the LoRa packet forwarder). Create the appropriate code for the packet forwarded and link to our repo.
- Run the tests on the Gateway (must be a real device) to connect to a LoRa end node and receive and send packets. 
- Once you have tested the framework and have all things running, open an issue on the repo and we will invite you to add a page for your gateway on our repo. The page can include details about your gateway and any specific instructions to make your gateway running with LoRaWAN starter kit. 

This approach provides us with validation that things work on the gateway and also allows others using the same Gateway to leverage the learnings.

## Device Node (sensor) Manufacturer Guidance
If you are a LoRa Node/Sensor manufacturer that leverages specific decoding scheme for the LoRa packets, we have provision for you to run those devices using our decoding framework.
### Instructions
Follow these steps to onboard your device with a custom decoder:
- Go through the [Developer Guidance](/Docs/devguide.md) to clone the repo and make sure everything works in your local dev environment.
- Make sure everything works with an Azure subscription with Standard Pricing SKU's, for example we do not support the Free Azure IoT Hub SKU.
- We have provided a [sample reference implementation](/Samples/DecoderSample) of a decoder, please refer to this as a template and leverage the [instructions](/Samples/DecoderSample/ReadMe.md) to create implementation of your customer decoder. 
- The Sample code can also contain device model specific tests that when run allows for testing of the gateway.
- Since we are .NET Core and C# based, the sample is based on the .NET technology stack, however you can create your decoders in your preferred languages by implementing similar interfaces. If you have a specific language or platform to be supported, submit an issue to let us know.
  
