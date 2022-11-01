# Eflow on Windows Server VM 

## Install Eflow 

1. Create new Windows Server VM.
1. Connect to the Windows Server VM.
1. Install hyper-V
   ```
   .\install-hyperv.ps1
   ```
1. Restart the VM and reconnect
1. Provision Eflow
   ```
   .\provision-eflow -iotEdgeDeviceConnectionString "HostName=****"
   ```

## Verify Eflow installation

1. Connect to the eflow VM
   ```
   Connect-EflowVm
   ```

1. Check the configuration and connection of the VM. 
   ```
   sudo iotedge check
   ```

1. Check that the deployment modules are running on the VM.
   ```
   sudo iotedge list
   ```


## Reference
1. [Create and provision an IoT Edge for Linux on Windows](https://learn.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-on-windows-symmetric?view=iotedge-1.4&tabs=azure-portal%2Cpowershell)