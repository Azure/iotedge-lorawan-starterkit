# What is Azure Sphere

[Azure Sphere](https://azure.microsoft.com/en-us/services/azure-sphere/) is a security service
combining hardware, an OS, and cloud services to secure IoT applications. Together these
three components ensure [The Seven Properties of Highly Secure
Devices](https://www.microsoft.com/en-us/research/uploads/prod/2020/11/Seven-Properties-of-Highly-Secured-Devices-2nd-Edition-R1.pdf).

[Pricing](https://azure.microsoft.com/en-us/pricing/details/azure-sphere/): one time purchase of
hardware only (no recurring fees)

## Advantages for the starter kit

- Automated management of the underlying OS and the images for the concentrators.
- Although some features are already supported e.g. password-less authentication or error reporting,
  we could benefit from additional built-in features, e.g:
  - hardware security
  - automated security updates to address newly discovered vulnerabilities
- Cost savings: considering the maintenance required for the concentrators, the additional or more
  expensive hardware could be justified or even be cheaper in the long term.

## Disadvantages

- Requires specific hardware with [limited choices so
  far](https://azure.microsoft.com/en-us/services/azure-sphere/#ecosystem) (some also out of stock)
  - There is a [LoRaWAN concentrator built from
    Miromico](https://www.avnet.com/wps/portal/silica/solutions/technologies/wireless-connectivity/lora-gateways/)
    - However it only works with the Packet Forwarder for now. Miromico are aware of this limitation but we
    are not sure when they plan to support LBS.
- Introduces another layer that needs to be considered.
- In order for the concentrators to receive over-the-air updates, they need Internet connectivity
  which could be a potential attack vector.
  - Currently concentrators only need to connect to an LNS.

## Integration with other Azure services

Azure Sphere can be used [together with IoT Hub or IoT
Central](https://docs.microsoft.com/en-us/azure-sphere/app-development/use-azure-iot): you need to
register the Sphere tenant certificate to the IoT Hub

It can also be used [together with IoT
Edge](https://docs.microsoft.com/en-us/azure-sphere/app-development/setup-iot-edge?tabs=cliv2beta):
an Azure IoT Edge device would act as a transparent gateway.

[On Azure Sphere Vs
RTOS](https://docs.microsoft.com/en-us/answers/questions/27002/when-should-i-use-azure-sphere-versus-azure-rtos.html):
Sphere focuses on security while RTOS is more generic/requires more manual maintenance/setting-up.
[The two can be combined
though](https://techcommunity.microsoft.com/t5/internet-of-things-blog/combining-azure-sphere-iot-security-with-azure-rtos-real-time/ba-p/1992869)

## Process for the concentrators

A brief recap of the steps as they are listed in the
[quickstart](https://docs.microsoft.com/en-us/azure-sphere/install/overview) with some additional
notes:

- Get the hardware (either a new device or "guardian module" that is "attached" to existing hardware)
- Install SDK/CLI + [VS
  extension](https://marketplace.visualstudio.com/items?itemName=AzureSphereTeam.AzureSphereSDKforVisualStudio2019)
  and connect the Sphere device to the PC
- [Create a
  "tenant"](https://docs.microsoft.com/en-us/azure-sphere/deployment/manage-tenants?tabs=cliv2beta)
  - Note this is different than a Active Directory tenant
  - Register the [tenant's certificate to IoT
    Hub](https://docs.microsoft.com/en-us/azure-sphere/app-development/setup-iot-hub?tabs=cliv2beta)
- [Claim the
  device](https://docs.microsoft.com/en-us/azure-sphere/install/claim-device?tabs=cliv2beta):
  one-time operation that can not be undone
- [Configure networking](https://docs.microsoft.com/en-us/azure-sphere/install/configure-wifi)

## Using existing devices

> A guardian module is add-on hardware that incorporates an Azure Sphere chip and physically
> attaches to a port on a "brownfield" device—that is, an existing device that may already be in
> use. [Docs](https://docs.microsoft.com/en-us/azure-sphere/hardware/guardian-modules)

[List of hardware includes only 2
devices (see guardian devices)](https://azure.microsoft.com/en-gb/services/azure-sphere/#ecosystem).

[Connectivity](https://docs.microsoft.com/en-us/azure-sphere/hardware/guardian-modules#connectivity)

- upstream to the cloud can be Ethernet, wifi or cellular
- downstream can be serial, ethernet, wireless

## Resources - activity

- [IoT show episode discussing Azure Sphere on the Miromico LoRaWAN gateway](https://www.youtube.com/watch?v=AqLOS3dnksE)
- Releases happen on a monthly cadence - last release on Jan 26 [was
cancelled](https://techcommunity.microsoft.com/t5/internet-of-things-blog/general-availability-release-of-azure-sphere-version-22-01-is/ba-p/3073222)
but otherwise seems actively developed.
- Community
  - [Stack
Overflow questions](https://stackoverflow.com/search?q=azuresphere&s=23550afd-6ece-47d5-a84f-2d0dc6fc9cf5&s=3b14b5b6-0179-4a03-ac51-bc826cc46fb1)
  - [Azure
Q&A](https://docs.microsoft.com/en-us/answers/search.html?c=&includeChildren=&f=&type=question+OR+idea+OR+kbentry+OR+answer+OR+topic+OR+user&redirect=search%2Fsearch&sort=relevance&q=azure%20sphere%20OS)
