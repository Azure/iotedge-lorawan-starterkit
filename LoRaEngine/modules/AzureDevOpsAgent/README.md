# Azure DevOps Agent (VSTS agent) for arm32 containers

Part of our [Azure IoT Hub LoRaWan starter kit](https://github.com/Azure/iotedge-lorawan-starterkit) project, we had to automate some hardware tests. The project is about building a gateway to support LoRa devices, join them, manage them thru Azure IoT Hub. The gateway is running on a container and can support Linux arm32 or amd64 bit architectures. The project is using on the [Semtech UDP packet forwarder](https://github.com/Lora-net/packet_forwarder) project. Then we have a full gateway, written in .NET Core running in an Azure IoT Edge module.

Software tests are not the only one that we want to run as we want to be able to validate specific region settings and make sure any modification in the code will work great as well on a real  device. To run and automate test in a proper CI/CD chain, we're using Azure DevOps, formally known as VSTS (Visual Studio Team Services). Azure DevOps allow to run remote tests and gather results thru an agent running remotely. The pipeline is specifically build to run our hardware tests. In short, we are sending serial commands to an Arduino (a Seeeduino LoRaWan device) connected thru a serial port on a Raspberry Pi3. The Raspberry Pi has a LoRaWan shield and the needed Azure IoT Edge modules to manage the Lora device(s). The test compare what is sent thru serial port, what is received from the device with what arrives to the LoRaEngine gateway and what is finally published in Azure IoT Hub.

So in order to run the test, deploy and manage them the best way, we've decided to build an Azure DevOps agent in a container. It will execute the tests written in C# in .NET Core. 

## Building the container

This solution is build on top of a VS Code Azure IoT Edge module. It is the fastest way to start. The main structure of the project is created. All non necessary elements like Windows platforms has been removed and only the Linux arm32v7 and amd64 has been kept. The cs files and projects has been removed as well. So to make this working we need the following in the container:

* the VSTS Agent for Linux arm
* the full .NET Core SDK for Linux arm
* nodejs

The docker file is the following:

```docker
FROM microsoft/dotnet:2.1-sdk-stretch-arm32v7
 
# Install curl, wget and git
RUN apt-get update && apt-get install -y \
curl \
wget \
git
 
# Download compiles vsts-agent
RUN curl https://vstsagentpackage.azureedge.net/agent/2.141.0/vsts-agent-linux-arm-2.141.0.tar.gz -o vsts-agent-linux-arm-2.141.0.tar.gz
RUN mkdir vsts-agent
RUN tar xzf vsts-agent-linux-arm-2.141.0.tar.gz -C ./vsts-agent
 
# install node
RUN curl -sL https://deb.nodesource.com/setup_8.x
RUN apt-get install -y nodejs

COPY vsts.sh .

ENTRYPOINT [ "/bin/bash", "./vsts.sh" ]

```

We are starting from official Microsoft image, Debian based containing the full .NET Core SDK 2.1. We are then adding the VSTS Agent build for Linux arm. then we add nodejs and finally run a script to configure the VSTS agent.

```bash
#! /bin/bash
./vsts-agent/bin/Agent.Listener configure --unattended --url $VSTS_SERVER_URL --auth PAT --token $VSTS_TOKEN --pool default --agent $AGENT_NAME --replace --acceptTeeEula
./vsts-agent/bin/Agent.Listener run
```

The script configure the VSTS agent with the server Azure DevOps URL as well a security token and an agent name. The entry point is logically the script.

## Specific container settings for IoT Edge

As we are accessing the hardware with our tests, we need to run into privilege mode in the container. This has to be added in the deployment.template.json file:

```json
"createOptions": "{  \"HostConfig\": { \"Privileged\": true } }"
```

## Creating your .env file

The project require multiple environment variables. You can copy the ```example.env``` file to a ```.env``` file and replace the various environment variable.
They are all mandatory:

* ```CONTAINER_REGISTRY_ADDRESS=yourregistry.azurecr.io``` where yourregistry is the name of your Azure IoT Container Registry
* ```CONTAINER_REGISTRY_USERNAME=yourlogin``` where yourlogin is the user from your registry
* ```CONTAINER_REGISTRY_PASSWORD=registrypassword``` where your registrypassword is the password for your Azure CR registry
* ```AGENT_VERSION=0.0.2``` you can change versions as you like. This is useful to force redeployment of the module on the end device
* ```VSTS_SERVER_URL=https://yourproject.visualstudio.com``` whenre yourproject is the Azure DevOps server you are using
* ```VSTS_TOKEN=yourtoken``` where yourtoken is the token you must generate to access Azure DevOps. Go to your Azure DevOps project, click on your profile then security then generate key and you'll be able to generate a key. Please note that keys has expiration and rights, please make sure you'll give enough rights to it to access the agent and report the tests results.
* ```AGENT_NAME=agent_name``` where agent_name is the name of the agent that will show up in the agent list.

## Building and depoying the only this solution

To build and deploy the solution, we strongly encourage you to use VS Code and the Azure IoT Edge extension. It will make your life easier.

![deployment](/Docs/Pictures/iotedgebuildcontainer.png)

Select ```deployment.template.json```, right click on it and then select ```Build and Push IoT Edge solution```. This will automatically create a ```deployment.json``` file in the ```config``` folder. Right click on it and select ```Create deployment for single device``` and select the Azure IoT Edge device you want to deploy the solution on. Of course, you can as well deploy it on any docker enable Linux arm32 device (or amd64 if you selected this configuration). 

## Building and deploying the solution part of other containers

If you are deploying the solution part of other containers on an IoT Edge solution, make sure you have this module part of the same ```modules``` folder and that you merge the specific Azure DevOps modules option into the main file. You can find an example [here](https://github.com/Azure/iotedge-lorawan-starterkit/blob/oneweek-pipeline/LoRaEngine/deployment.test.template.json).

