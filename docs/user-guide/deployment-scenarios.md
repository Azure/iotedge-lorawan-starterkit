# Deployment Scenarios

The Starter Kit support a wide variety of deployments, and this article will
highlight some of them.

- [Keep data on-premise](#keep-data-on-premise)
- [Redundancy with multiple concentrators and gateways](#redundancy-with-multiple-concentrators-and-gateways)
- [Deployment on Windows Server](#deployment-on-windows-server)
- [Deployment of IoT Edge in the cloud](#deployment-of-iotedge-in-the-cloud)

Other scenarios are supported, and combinations of the below scenarios are possible.

## Keep data on-premise

It is possible to deploy Azure IoT Edge on your own hardware, and keep all data
in your local network or infrastructure. For example, by using a custom local
forwarding module, it is possible to route the data to a local queue or message
bus. In this case, the sensor data will never leave the on-premise network, and
the connection to Azure will be used for managing the devices, handling the
[deduplication](../adr/007_message_deduplication.md) or managing [concentrator
updates](station-firmware-upgrade.md) for example.

![Keep data on-premise](../images/scenarios/scenario-local-data.png)

## Scalability & Availability

### Single conentrator with single LNS

This is the most basic deployment you can chose. It does only deploy 1 concentrator
and one network server. Each sensor can at most reach a single concentrator.

#### Advantages

1. Scalability: this model scales very well. There is no additional overhead to deduplicate
messages, nor do we need to determine who is winning the race to potentially send messages
back to the device. Also, no additional edge hub keeps a connection for the leaf device, which helps with the scale requirement on the IoT Hub itself.
1. Simple: the deployment is simple to start with and maintain.
1. Low cost: No redundant hardware is used, which helps keeping the operational cost down.

#### Disadvantages

1. Availability: this solution has 2 points of failure: the concentrator and the
network server. Either of those can go down. The result in the worst case is lost
of messages.
1. Limited reach: Since there is only a single concentrator, you have to put all your sensor
within reach of that concentrator.
1. Reliability: message delivery might be impacted.

#### Recommended settings

| Target        | Setting               |Value|
|---------------|:----------------------|:--------|
| Device Twin   |KeepAliveTimeout       |undefined or 0 |
| Device Twin   |Deduplication          |None           |
| Device Twin   |GatewayId              |The Id of the Network Server |

![Single concentrator - Single gateway](../images/scenarios/1_scale_and_availability.jpg)

### Multi concentrator with single LNS

Multiple concentrators are deployed that reach a single network server. Each sensor can reach one or multiple concentrators. Depending on the goal of the deployment (availability and/or reach).

#### Advantages

1. Scalability: this model scales very well. It's a bit more overhead than the single concentrator / single gateway
model as it will have to deduplicate messages coming in from the additional
concentrators.
1. Relatively simple
1. Low cost
1. Partial redundancy: if the deployment is designed to ensure that each sensor can reach
at least 2 concentrators, you get higher availability than with single/single.
1. Reach: if you deploy for reach, you can get a larger support a larger geographical
deployment. To combine reach with availability, the cost will raise.

#### Disadvantages

1. Availability: Depending on the deployment, this solution has at least one single point of failure: the network server.
The result in the worst case is lost of messages.
1. Reliability: message delivery might be impacted.

#### Recommended settings

| Target        | Setting               |Value|
|---------------|:----------------------|:--------|
| Device Twin   |KeepAliveTimeout       |undefined or 0 |
| Device Twin   |Deduplication          |None           |
| Device Twin   |GatewayId              |The Id of the Network Server |

![Multi Single](../images/scenarios/2_scale_and_availability.jpg)

### Multiple concentrator with Multiple LNS

Multiple concentrators are deployed that reach multiple LNS (*note* one concentrator
can only be connected to a single LNS). Each sensor can reach one or multiple concentrators. Depending on the goal of the deployment (availability and/or reach).

To get high availability concentrator deployment and assignment to LNS should be done
so that a single sensor is in range of at least 2 concentrators, both connecting to a
**different** LNS.

#### Advantages

1. Availability: If the deployment is done right, the single point of failure can
be eliminated.
1. Reliability: message delivery likelyhood is increased.
1. Reach: The deployment model can be expanded

#### Disadvantages

1. Cost: all components need to be deployed and managed multiple times.
1. Complexity
1. Scalability: The scalability is impacted as the LNS now needs to manage leader election
for message handling, as well as multiple connetion are opened for the same client on
multiple edge hubs, resulting in connection ping pong.

#### Recommended settings

| Target        | Setting               |Value|
|---------------|:----------------------|:--------|
| Device Twin   |KeepAliveTimeout       | > 0 - depending on the sensor, this can be tuned to be the time you expect a new message to come in. Ideally not > 300 |
| Device Twin   |Deduplication          |Drop           |
| Device Twin   |GatewayId              |empty|

![Redundancy](../images/scenarios/scenario-redundancy.png)

## Deployment on Windows Server

Some organizations only support Windows Server operating systems
on their infrastructure. In this case, it is still possible to use IoT Edge and
the Starter Kit by using [*EFLOW*](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-for-linux-on-windows?view=iotedge-2018-06),
short for "Azure IoT Edge for Linux on Windows". This allows you to run containerized Linux
workloads on Windows machines.

![Redundancy](../images/scenarios/scenario-eflow.png)

## Deployment of IoT Edge in the cloud

It is also possible to run the IoT Edge modules directly in the cloud. Either a
Linux VM to run IoT Edge directly, or on a Windows VM with EFLOW.
In fact, the full end-to-end Continuous Integration pipeline for the Starter Kit
also tests this scenario with an installation of EFLOW in a Windows Server VM in Azure.

![Redundancy](../images/scenarios/scenario-edge-in-cloud.png)

## Appendix

- [More info on deduplication strategies](../adr/007_message_deduplication.md)
