# Observability

## Integrating with Azure Monitor

We support Azure Monitor for observability of the LoRaWAN starter kit. If you decide to use Azure Monitor, you will need to create an Application Insights instance and a Log Analytics workspace in your subscription. To enable observability using Azure Monitor, modify the following settings in your `.env` file:

```{bash}
APPINSIGHTS_INSTRUMENTATIONKEY={appinsight_key}
IOT_HUB_RESOURCE_ID=/subscriptions/{subscription_id}/resourceGroups/{resource_group}/providers/Microsoft.Devices/IotHubs/{iot_hub_name}
LOG_ANALYTICS_WORKSPACE_ID={log_analytics_workspace_id}
LOG_ANALYTICS_SHARED_KEY={log_analytics_shared_key}
```

Generate a deployment manifest from `deployment_observability.layered.template.json` and deploy it to the edge devices for which you want to apply the observability. The template will set up the [metrics collector module](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-collect-and-transport-metrics?view=iotedge-2020-11&tabs=iothub#metrics-collector-module) on the edge and connect it with your Log Analytics instance. The gateway will connect to your Application Insights instance. Make sure to set the `APPINSIGHTS_INSTRUMENTATIONKEY` before deploying the `deployment.template.lbs.json` solution, if you want to make sure that the gateway can connect to Application Insights. The Application Insights log level will always be the same as the console log level.

The Network Server will always expose metrics in Prometheus format at the path `/metrics`. You can scrape these metrics using the tool of you choice, as we will show in the next example.

## Integrating with the Elastic stack

In this section we describe an example setup that may get you started if you decide to use the Elastic tool stack for observability (i.e. [Elasticsearch](https://www.elastic.co/elasticsearch/) and potentially Logstash/Kibana for this example).

For this example, we will assume that you have set up the Elastic stack already, and that you want to integrate the starter kit into your ELK observability setup. If you do not have the Elastic stack set up yet, refer to [Elastic's extensive documentation](https://www.elastic.co/guide/index.html) and set it up before you begin with this example.

To integrate the starter kit into your ELK stack, we will rely on [Metricbeat](https://www.elastic.co/beats/metricbeat) to scrape metrics from the Prometheus endpoints of the  IoT Edge modules (Network Server, Edge Hub and Edge Agent). Since Metricbeat requires access to these IoT Edge endpoints, we will deploy it as an IoT Edge module, too. 

>  Note: To [run Metricbeat on Docker](https://www.elastic.co/guide/en/beats/metricbeat/7.16/running-on-docker.html) you will need to make sure that you run IoT Edge on a supported OS/Architecture. At the time of writing, the Metricbeat Docker image does for example not work on ARM devices.

To ensure that the Metricbeat is aware of all metrics endpoints it needs to scrape and the ELK backend for metric export, we need to configure it correctly. Typically, to [configure Metricbeat on Docker](https://www.elastic.co/guide/en/beats/metricbeat/7.16/running-on-docker.html#_configure_metricbeat_on_docker) we rely on a configuration file, which we will call `metricbeat.yml` in the following, that needs to be present inside the Docker container. Before we discuss how to make this file available inside the Docker container, we discuss its contents and how we need to modify it.

To enhance Metricbeat with the required functionality to scrape the Prometheus endpoints, we rely on the [Prometheus module](https://www.elastic.co/guide/en/beats/metricbeat/current/metricbeat-module-prometheus.html). Make sure that you add the module configuration to your `metricbeat.yml`. A basic configuration snippet might look like this:

```yaml
...
metricbeat.modules:
- module: prometheus
  period: 10s
  metricsets: ["collector"]
  hosts: ["LoRaWanNetworkSrvModule:5000", "edgeHub:9600", "edgeAgent:9600"]
  metrics_path: /metrics
...
```

> Note: If you configure the Network Server to use HTTPS, it will listen on port 5001 instead of 5000. If you use self-signed certificates for this, make sure that you configure the Prometheus module with your chain of trust.

The last thing left to do is to make sure that the `metricbeat.yml` is available to the Metricbeat module. You can either [link module storage to device storage](https://docs.microsoft.com/en-us/azure/iot-edge/how-to-access-host-storage-from-module?view=iotedge-2020-11) in IoT Edge to achieve this, in which case you have to decide how you provide `metricbeat.yml` on the system where you host IoT Edge. Alternatively, you can deploy a custom image with a `metricbeat.yml` *template* and [use environment variables in the configuration](https://www.elastic.co/guide/en/beats/metricbeat/current/using-environ-vars.html) to set values that need to be configurable during deployment. If you use this approach, you will then need to amend your IoT Edge deployment template with something similar to:

```yaml
{
  "modulesContent": {
    "$edgeAgent": {
      "properties.desired": {
        "schemaVersion": "1.0",
        ...,
        "modules": {
          ...,
          "metricbeat": {
            "settings": {
              "image": "{IMAGE_PATH}",
              "createOptions": "{\"HostConfig\":{\"Privileged\":true},\"Binds\":[\"/var/run/docker.sock:/var/run/docker.sock:ro\",\"sys/fs/cgroup:/hostfs/sys/fs/cgroup:ro\",...]}"
            },
            "env": {
              "foo": {
                "value": "bar"
              }
            },
            "type": "docker",
            "version": "1.0",
            "imagePullPolicy": "on-create",
            "status": "running",
            "restartPolicy": "always"
          }
        }
      }
    },
    ...
  }
}

```

You can use environment variables, such as the `foo=bar` environment variable example, to replace environment variable references in your `metricbeat.yml`.

To ship logs to ELK, you can follow the same strategy using [Filebeat](https://www.elastic.co/beats/filebeat) instead of Metricbeat.
