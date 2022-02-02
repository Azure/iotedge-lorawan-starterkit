# What is Azure Sphere

[Azure Sphere](https://azure.microsoft.com/en-us/services/azure-sphere/) delivers device security by
combining hardware, operating system software, and cloud services that has been purpose-built for
secure IoT applications.

Together these components implement [The Seven Properties of Highly Secure
Devices](https://www.microsoft.com/en-us/research/uploads/prod/2020/11/Seven-Properties-of-Highly-Secured-Devices-2nd-Edition-R1.pdf).

[Pricing](https://azure.microsoft.com/en-us/pricing/details/azure-sphere/): one time purchase of
devices only (no recurring fees)

## Advantages for the starter kit

- Automated management of the underlying OS and the concentrator logic.
- Out of the box security for critical applications with features:
  - hardware security
  - error reporting (i.e. A software error, such as a buffer overrun induced by an attacker probing
security, is reported to cloud-based failure analysis system)
  - renewable security (revoking compromised assets for known vulnerabilities)
- Considering the maintenance required for the concentrators, the additional or more expensive
  hardware could still be justified or even be cheaper in the long term.

## Disadvantages

- Requires specific hardware with [limited choices so
  far](https://azure.microsoft.com/en-us/services/azure-sphere/#ecosystem) (some also out of stock)
  - There is a [concentrator built from
    Miromico](https://www.avnet.com/wps/portal/silica/solutions/technologies/wireless-connectivity/lora-gateways/)
    that works with the Packet Forwarder and we are in touch with the company to learn if it can be
    potentially used with the LoRa Basics Station.
- Cups implementation already supports some of the features e.g. password-less authentication

# Process for the concentrators

[Quickstart](https://docs.microsoft.com/en-us/azure-sphere/install/overview)
- Get the hardware (either a new device or "guardian module")
- install SDK/CLI + [VS
  extension](https://marketplace.visualstudio.com/items?itemName=AzureSphereTeam.AzureSphereSDKforVisualStudio2019)
  and connect the device to the PC
- [create a
  tenant](https://docs.microsoft.com/en-us/azure-sphere/deployment/manage-tenants?tabs=cliv2beta)
- [claim the
  device](https://docs.microsoft.com/en-us/azure-sphere/install/claim-device?tabs=cliv2beta):
  one-time operation that can not be undone
- [configure networking](https://docs.microsoft.com/en-us/azure-sphere/install/configure-wifi)
- [subscribe to
  notifications/updates](https://docs.microsoft.com/en-us/azure-sphere/install/get-notifications)

## Integration with other services

Can be used [together with IoT Hub or
Central](https://docs.microsoft.com/en-us/azure-sphere/app-development/use-azure-iot): you need to
register the Sphere tenant to the hub

Can be used [together with IoT
Edge](https://docs.microsoft.com/en-us/azure-sphere/app-development/setup-iot-edge?tabs=cliv2beta)
> After you have completed the tasks in this topic, your Azure Sphere device will be configured to
> send data to an Azure IoT Hub through an Azure IoT Edge device that acts as a transparent gateway.

[Sphere Vs Azure
RTOS](https://docs.microsoft.com/en-us/answers/questions/27002/when-should-i-use-azure-sphere-versus-azure-rtos.html):
Sphere focuses on security while RTOS is more generic/requires more manual maintenance/setting-up.

[The two can be combined
though](https://techcommunity.microsoft.com/t5/internet-of-things-blog/combining-azure-sphere-iot-security-with-azure-rtos-real-time/ba-p/1992869)

## Using existing devices

> A guardian module is add-on hardware that incorporates an Azure Sphere chip and physically
> attaches to a port on a "brownfield" device—that is, an existing device that may already be in
> use. [Docs](https://docs.microsoft.com/en-us/azure-sphere/hardware/guardian-modules)

[List of hardware includes only 2
devices](https://azure.microsoft.com/en-gb/services/azure-sphere/#ecosystem).

[Connectivity](https://docs.microsoft.com/en-us/azure-sphere/hardware/guardian-modules#connectivity)
- upstream to the cloud can be Ethernet, wifi or cellular
- downstream can be serial, ethernet, wireless

# Resources - activity 

- Short [video demo](https://www.youtube.com/watch?v=AqLOS3dnksE)
- Releases happen on a monthly cadence - release on Jan 26 [was
cancelled](https://techcommunity.microsoft.com/t5/internet-of-things-blog/general-availability-release-of-azure-sphere-version-22-01-is/ba-p/3073222)
but otherwise seems actively developed.
- [Stack
overflow](https://stackoverflow.com/search?q=azuresphere&s=23550afd-6ece-47d5-a84f-2d0dc6fc9cf5&s=3b14b5b6-0179-4a03-ac51-bc826cc46fb1)
- [Azure
Q&A](https://docs.microsoft.com/en-us/answers/search.html?c=&includeChildren=&f=&type=question+OR+idea+OR+kbentry+OR+answer+OR+topic+OR+user&redirect=search%2Fsearch&sort=relevance&q=azure%20sphere%20OS)