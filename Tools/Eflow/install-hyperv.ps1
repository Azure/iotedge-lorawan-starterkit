<# .SYNOPSIS
    Hyper-V installation Script
.DESCRIPTION
    This script automatically installs Hyper-V on a Windows Server VM to allow eflow provisioning.
    The VM requires a restart after this script is run before eflow can be provisioned.
.NOTES
     Author     :
        Mikhail Chatillon       - chmikhai@microsoft.com
        Daniele Antonio Maggio  - daniele.maggio@microsoft.com
        Nora Abi Akar           - noraabiakar@microsoft.com
#>

# Install Hyper-V
Install-WindowsFeature -Name Hyper-V
Install-WindowsFeature -Name Hyper-V-PowerShell
Enable-WindowsOptionalFeature -Online -FeatureName VirtualMachinePlatform -All -NoRestart

Write-Host "Please reboot to finalize installation."
