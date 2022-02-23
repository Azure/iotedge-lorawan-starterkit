# LNS discovery

The LNS protocol specifies that the first request an LBS makes is to the `/router-info` discovery endpoint. The LNS from the starter kit implements the `/router-info` endpoint and redirects any requests to that endpoint forward to the `/router-data` endpoint on the same LNS.

In addition to the discovery endpoint on the LNS itself, we also support a standalone discovery service, which you can use to distribute LBS connections to different LNS. The discovery service is a .NET application, and its implementation is part of the `LoRaWan.NetworkServerDiscovery` project.

The discovery service relies on configuration from IoT Hub module twins to associate each LBS with a set of LNS to which it may connect to. For this the configuration needs to take into account possible network boundaries - by defining these networks you can control which LBS should connect to which LNS. For this you need to configure your LBS twin in IoT Hub as follows:

```json
{
  ...,
  "tags": {
    "network": "<network-id>"
  },
  ...
}
```

The network ID may only consist of alphanumeric characters. After you configured the LBS, you need to also configure your LNS with the same network. The LNS twin furthermore needs to contain the IP/DNS of the host, hence the twin of the LNS *module* should look like:

```json
{
  ...,
  "tags": {
    "network": "<network-id>"
  },
  ...,
  "properties": {
    "desired": {
      "hostAddress": "wss://mylns:5001",
      ...
    }
  }
}
```

Keep in mind that the `hostAddress` value must include the scheme and the host name. In case you omit the port, the defaults of the protocol will be used.

!!! note
    The discovery endpoint distributes LBS to LNS using a round-robin distribution mechanism. It will always try the same LNS per LBS first. Hence, it can be used for an active/passive scenario, but not to distribute load more evenly across different LNS.

!!! note
    The configuration values of the network name of a station, respectively of the set of LNS in a given network, are cached by the discovery endpoint for six hours.
    If you updated the configuration, make sure to restart the discovery service to ensure that the cache is refreshed.

You can choose to deploy the discovery service either on-prem or in the cloud. For both deployment strategies, you can configure the following behavior:

- The log level can be configured by setting the `Logging__LogLevel__Default` environment variable. For more fine-grained configuration of the console log level, refer to [Logging in .NET Core and ASP.NET Core](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-6.0#set-log-level-by-command-line-environment-variables-and-other-configuration).
- You can use Application Insights by setting the `APPINSIGHTS_INSTRUMENTATIONKEY` environment variable. The log levels for Application Insights can be configured similarly to the default log levels, but by using the environment variables `Logging__ApplicationInsights__LogLevel__<Default|...>`.
- You can configure on which URLs the service is listening by setting the environment variable `ASPNETCORE_URLS`.

## Deployment in Azure

We recommend that you use an Azure App Service to run the discovery service in the cloud. You can follow the [App Service Quickstart](https://docs.microsoft.com/en-us/azure/app-service/quickstart-dotnetcore?tabs=net60&pivots=development-environment-cli) for instructions how to deploy an ASP.NET Core app in an Azure App Service. When using an App Service, make sure that you:

- [Enable the system assigned managed identity](https://docs.microsoft.com/en-us/azure/app-service/overview-managed-identity?tabs=portal%2Chttp#add-a-system-assigned-identity)
- Assign a role to the system assigned identity which allows it to query the IoT Hub twins, e.g. IoT Hub Twin Contributor.
- Specify the `IotHubHostName` environment variable, such that it points to your IoT Hub.

## On-premises deployment

When deploying the discovery service on-premises, please read how to [Host ASP.NET Core on Windows with IIS](https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-6.0) (or any other hosting strategy you may want to use).

- You must specify the IoT Hub connection string by setting the `ConnectionStrings__IotHub` environment variable, since you cannot use managed identities with an on-premises deployment. The discovery endpoint exposes metrics in Prometheus format.
- You can configure the certificates that should be used by the discovery endpoint as described in the [Minimal APIs overview](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-6.0#specify-https-using-a-custom-certificate). Instead of using the `appsettings.json`, you can use environment variables of the same structure `Kestrel__Certificates__Default__<Path|KeyPath|...>`.
