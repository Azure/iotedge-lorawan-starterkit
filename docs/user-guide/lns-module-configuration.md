---
hide:
  - toc
---

# Network Server IoT Edge Module configuration

The following table is providing a list of configuration options, to be provided as environment variables for manual configuration of the `LoRaWanNetworkSrvModule`:

| Environment variable name   | Description                                                                            | Mandatory                           |
| --------------------------- | -------------------------------------------------------------------------------------- | ----------------------------------- |
| ENABLE_GATEWAY              | Indicates whether the edgeHub gateway should be enabled or not                         | No (defaults to `true`)             |
| IOTEDGE_DEVICEID            | The gateway deviceId                                                                   | Yes                                 |
| HTTPS_PROXY                 | HTTPS proxy url                                                                        | No                                  |
| RX2_DATR                    | RX2 data rate; useful to override the default regional RX2 Data Rate at a global level | No (defaults to null, regional default value is used) |
| RX2_FREQ                    | RX2 frequency                                                                          | No                                  |
| IOTEDGE_TIMEOUT             | IoT Edge timeout in milliseconds                                                       | No (defaults to 0)                  |
| FACADE_SERVER_URL           | Azure Facade function url                                                              | Yes                                 |
| FACADE_AUTH_CODE            | Azure Facade function auth code                                                        | Yes                                 |
| LOG_LEVEL                   | Logging level                                                                          | No (defaults to level 4 (Error)     |
| LOG_TO_CONSOLE              | Indicates whether logging to console is enabled or not                                 | No (default to `true`)              |
| LOG_TO_TCP                  | Indicates whether logging to TCP is enabled or not (used mainly for integration tests) | No (defaults to `false`)            |
| LOG_TO_HUB                  | Indicates whether logging to IoT Hub is enabled or not                                 | No (defaults to `false`)            |
| LOG_TO_TCP_ADDRESS          | IP address for TCP logs                                                                | Yes, only if TCP logging is enabled |
| LOG_TO_TCP_PORT             | TCP port to send logs to                                                               | Yes, only if TCP logging is enabled |
| NETID                       | Gateway network ID                                                                     | No (defaults to network id 1)       |
| AllowedDevAddresses         | Allowed Dev addresses                                                                  | No                                  |
| LNS_SERVER_PFX_PATH         | Path of the .pfx certificate to be used for LNS Server endpoint                        | No                                  |
| LNS_SERVER_PFX_PASSWORD     | Password of the .pfx certificate to be used for LNS Server endpoint                    | No                                  |
| CLIENT_CERTIFICATE_MODE     | Specifies the client certificate mode with which the server should be run              | No (defaults to `NoCertificate`)    |
| LNS_VERSION                 | Version of the LNS                                                                     | No                                  |
| IOTHUB_CONNECTION_POOL_SIZE | IoT Hub connection pool size                                                           | No (defaults to 1)                  |

The following settings can be configured via desired properties of the Network Server module twin in IoT Hub:

| Property name                 | Description                                                                                                                                   | Mandatory               |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------- |
| FacadeServerUrl               | Azure Facade function url                                                                                                                     | Yes                     |
| FacadeServerAuthCode          | Azure Facade function auth code                                                                                                               | Yes                     |
| ProcessingDelayInMilliseconds | Processing delay (in milliseconds) which should be used for LNS that does not own connection for a device (applicable to multi-gateway setup) | No (defaults to 400 ms) |
