<# .SYNOPSIS
    EFLOW Windows Server provisioning Script
.DESCRIPTION
    This script automatically installs Edge For Linux On Windows (EFLOW) on a new Windows Server VM.
    This script assumes that the Windows VM has Hyper-V already installed.
    The script accepts the following parameters:
    - iotEdgeDeviceConnectionString: (required) the iotedge device connection string.
    - switchName: (optional) the switch name.
    - startEflowIpRange: (optional) start of the IP range of eflow.
    - internalPort: (optional) the internal port of the Net Nat Static Mapping.
    - externalPort: (optional) the external port of the Net Nat Static Mapping.
.NOTES
     Author     :
        Mikhail Chatillon       - chmikhai@microsoft.com
        Daniele Antonio Maggio  - daniele.maggio@microsoft.com
        Nora Abi Akar           - noraabiakar@microsoft.com
#>

param
(
    [string]$iotEdgeDeviceConnectionString,
    [string]$switchName,
    [string]$startEflowIpRange=100,
    [int]$internalPort=5000,
    [int]$externalPort=5000
)

# Verify dependencies
if (!$iotEdgeDeviceConnectionString)
{
    throw "IoT Edge Device Connection String not provided."
}

if (!$switchName)
{
    throw "Switch Name not provided."
}

if ((Get-WindowsFeature -Name "Hyper-V").Installed -eq $false)
{
    throw "Hyper-V not correctly installed."
}

if(!(Get-VMSwitch -Name $switchName -ErrorAction SilentlyContinue))
{
    throw "Switch $switchName not found";
}

# Create Nat
Write-Host "Creating Nat"

$netAdapterIfIndex=(Get-NetAdapter -Name "*$switchName*").ifIndex
$netAdapterIpAddress=Get-NetIPAddress -AddressFamily IPv4  -InterfaceIndex $netAdapterIfIndex
$netAdapterIp=$netAdapterIpAddress.IPAddress
$ipAddressFamily=$netAdapterIp.Substring(0, $netAdapterIp.lastIndexOf('.')+1)
$gwIp=$ipAddressFamily+1
$natIp=$ipAddressFamily+0
$startIp=$ipAddressFamily+$startEflowIpRange

New-NetNat -Name "$switchName" -InternalIPInterfaceAddressPrefix "$natIp/24"

# Install Eflow
Write-Host "Installing Eflow"

Set-ExecutionPolicy -ExecutionPolicy AllSigned -Force
$msiPath = $([io.Path]::Combine($env:TEMP, 'AzureIoTEdge.msi'))
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://aka.ms/AzEflowMSI" -OutFile $msiPath

Start-Process -Wait msiexec -ArgumentList "/i","$([io.Path]::Combine($env:TEMP, 'AzureIoTEdge.msi'))","/qn"
Deploy-Eflow  -acceptEula 'yes' -acceptOptionalTelemetry 'yes' -vswitchName $switchName -ip4Address $startIp -ip4GatewayAddress $gwIp -vswitchType 'Internal' -ip4PrefixLength 24
Provision-EflowVm -provisioningType ManualConnectionString -devConnString "$iotEdgeDeviceConnectionString"

# Add static mapping
Write-Host "Creating static mapping"

if(!(Get-NetNatStaticMapping -NatName "$switchName" -ErrorAction SilentlyContinue))
{
    Add-NetNatStaticMapping -ExternalIPAddress "0.0.0.0/0" -ExternalPort $externalPort -Protocol TCP -InternalIPAddress "$startip" -InternalPort $internalPort -NatName $switchName
}

# Set DNS server
Write-Host "Set DNS server"

Set-EflowVmDNSServers -dnsServers 168.63.129.16
