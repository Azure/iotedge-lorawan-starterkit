# Scalability

Our findings and recommendations around scaling the starter kit are based on a set of load tests that we ran. If you want to learn about how to run these load tests, refer to the [Load Tests](./testing/load_tests.md) documentation.

## Single-gateway scenario

For the case of using a single LNS [as an IoT Edge gateway](https://docs.microsoft.com/en-us/azure/iot-edge/iot-edge-as-gateway?view=iotedge-2020-11), we were able to test our solution with 900 OTAA devices connected to a single gateway. After an initial join and cache warm-up phase, each device was sending a message every three minutes, broadcasting each message to four concentrators connected to the same gateway. Keep in mind that in such a scenario you need to adapt the IoT Edge `MaxConnectedClients` environment variable. For more details on the exact configuration we used for these scalability tests, refer to the [Load Tests](./testing/load_tests.md) documentation.

## Multi-gateway scenario

If you plan to use multiple gateways as an IoT Edge gateway, you will not achieve the same scalability as with a single gateway. We were able to successfully scale test 80 OTAA devices connected to multiple gateways, broadcasting each message to each gateway through two concentrators. We will address this limitation in a future release.

## IoT Edge in direct mode

!!! Warning
    Direct mode is not recommended for production use, as it has limitations with respect to message delivery guarantees. If you decide to use this, you may lose messages.

We were able to support 1600 OTAA devices, sending progressively more messages, with a maximum of 9 messages per second to two gateways. During our scalability tests, D2C message delivery latency was on average 450ms after the warm-up phase, making sure that all receive windows were hit for 10'000 messages sent. 
