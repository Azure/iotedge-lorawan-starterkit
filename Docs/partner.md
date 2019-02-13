# Device Manufacturer Guidance

The LoRaWAN starter kit currently is tested on many popular gateways, we run some of those as part of daily CI/CD pipeline to test integrity and performance of our codebase. However, we cannot test each and every gateway out there and hence we have created a process for device manufacturer to support their gateways and get them highlighted in this repo.

## Step by Step Instructions

If you would like to make your gateway highlighted on our GitHub page, we expect that you contribute to our repo in terms of a sample on how to make the gateway work. This approach provides us with validation that things work on the gateway and also allows others using the same Gateway to leverage the code in the sample. To create the sample, follow these steps:

- Go through the [Developer Guidance](/Docs/devguide.md) to clone the repo and make sure everything works in your local dev environment.
- You may have to create a specific encoder / decoder, we provide a framework for adding your custom encoder. Refer to [Developer Guidance](/Docs/devguide.md) for more details.
- Make sure everything works with an Azure subscription with Standard Pricing SKU's, for example we do not support the Free Azure IoT Hub SKU.
- Once you have the code working, we expect that you create a sample for the custom decoder for your gateway that can be used by any other developer to onboard the starter kit for that device. The sample should be created in the [Samples](/Samples) folder and follow the below structure:
  
``` 
.. Samples
|-------- .. <Device Manufacturer Name>  (Create this folder for company)
|------------ .. <Device Gateway Model> (Create this folder for every gateway)
|--------------- .. Sample source code (add you source code here)
```

Example:

![Decoder](/Docs/Pictures/samples.png)

- We have provided a [sample reference implementation](/Samples/DecoderSample) of a decoder, please refer to this as a template and leverage the [instructions](/Samples/DecoderSample/ReadMe.md) to create implementation of your customer decoder. 
- The Sample code can also contain device model specific tests that when run allows for testing of the gateway.
- Since we are .NET Core and C# based, the sample is based on the .NET technology stack, however you can create your decoders in your preferred languages by implementing similar interfaces. If you have a specific language or platform to be supported, submit an issue to let us know.
  
