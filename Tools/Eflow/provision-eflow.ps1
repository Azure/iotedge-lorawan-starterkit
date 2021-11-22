<# .SYNOPSIS
    EFLOW Windows Server provisioning Script
.DESCRIPTION
    This script automatically install Edge For Linux On Windows (EFLOW) on a new Windows Server VM
    The script might trigger a restart and might need to be rerun after a restart
.NOTES
     Author     :
        Mikhail Chatillon       - chmikhai@microft.com
        Daniele Antonio Maggio  - daniele.maggio@microsoft.com
#>

param
(
    [string]$switchName="EFLOW Switch",
    [string]$startEflowIpRange=100,
    [string]$endEflowIpRange=200,
    [string]$iotEdgeDeviceConnectionString,
    [int]$internalPort=5000,
    [int]$externalPort=5000
)

# Enable Windows Features
if ((Get-WindowsFeature -Name "DHCP").Installed -eq $false)
{
    Install-WindowsFeature -Name DHCP -IncludeManagementTools
}
if ((Get-WindowsFeature -Name "Hyper-V").Installed -eq $false)
{
    Install-WindowsFeature -Name Hyper-V
    Install-WindowsFeature -Name Hyper-V-PowerShell
    Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -All -NoRestart
    Write-Host "Restarting the computer in 30 seconds to finish Hyper-V installation"
    Start-Sleep -Seconds 30
    Restart-Computer
}
if ((Get-WindowsFeature -Name "DHCP").Installed -eq $false)
{
    throw "DHCP not correctly installed."
}
if ((Get-WindowsFeature -Name "Hyper-V").Installed -eq $false)
{
    throw "Hyper-V not correctly installed."
}

# Create the networking
if(Get-VMSwitch -Name $switchName -ErrorAction SilentlyContinue)
{
    throw "Switch already existing";
}

# TODO: We should ideally add more checks before continuing.
New-VMSwitch -Name $switchName -SwitchType internal
Write-Host "Sleeping for 30sec before continuing for propagating the VMSwitch"
Start-Sleep -Seconds 30
Write-Host "Finished waiting for propagating the VMSwitch"
$netAdapterIfIndex=(Get-NetAdapter -Name "*$switchName*").ifIndex
$netAdapterIpAddress=Get-NetIPAddress -AddressFamily IPv4  -InterfaceIndex $netAdapterIfIndex
$netAdapterIp=$netAdapterIpAddress.IPAddress
$ipAddressFamily=$netAdapterIp.Substring(0, $netAdapterIp.lastIndexOf('.')+1)
$gwIpCounter=1
$gwIp=$ipAddressFamily+$gwIpCounter

while(!(get-NetIPAddress -IpAddress $gwIp -ErrorAction SilentlyContinue))
{
    $gwIpCounter++
    if($gwIpCounter>9)
    {
        throw "All the IPs in the subnet range 1-9 on $ipAddressFamily were already taken";
    }

    $gwIp=$ipAddressFamily+$gwIpCounter
}

$natIp=$ipAddressFamily+0

New-NetIPAddress -IPAddress $gwIp -PrefixLength 24 -InterfaceIndex $netAdapterIfIndex

if(Get-NetNat -Name "$switchName" -ErrorAction SilentlyContinue)
{
    throw "Net Nat with name $switchName already existing";
}

New-NetNat -Name "$switchName" -InternalIPInterfaceAddressPrefix "$natIp/24"
Add-NetNatStaticMapping -ExternalIPAddress "0.0.0.0/0" -ExternalPort $externalPort -Protocol TCP -InternalIPAddress "$startip" -InternalPort $internalPort -NatName $switchName

#Install DHCP
netsh dhcp add securitygroups
Restart-Service dhcpserver
$startIp=$ipAddressFamily+$startEflowIpRange
$endIp=$ipAddressFamily+$endEflowIpRange
Add-DhcpServerV4Scope -Name "AzureIoTEdgeScope" -StartRange $startIp -EndRange $endIp -SubnetMask 255.255.255.0 -State Active
Set-DhcpServerV4OptionValue -ScopeID $natIp -Router $gwIp
Restart-Service dhcpserver

# install Eflow
Set-ExecutionPolicy -ExecutionPolicy AllSigned -Force
$msiPath = $([io.Path]::Combine($env:TEMP, 'AzureIoTEdge.msi'))
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest "https://aka.ms/AzEflowMSI" -OutFile $msiPath
Start-Process -Wait msiexec -ArgumentList "/i","$([io.Path]::Combine($env:TEMP, 'AzureIoTEdge.msi'))","/qn"
Deploy-Eflow  -acceptEula 'yes' -acceptOptionalTelemetry 'yes' -vswitchName $switchName -ip4Address $startIp -ip4GatewayAddress $gwIp -vswitchType 'Internal'
if($iotEdgeDeviceConnectionString){
    Provision-EflowVm -provisioningType ManualConnectionString -devConnString "$iotEdgeDeviceConnectionString"
}
