
# At the moment, this script is not used. It will be used in the future if the CI moved away from Edge toolkit
param
(
    [string]$DEPLOYMENT_FILE_PATH,
    [string]$NEW_DEPLOYMENT_NAME,
    [string]$TARGET_DEVICE_ID,
    [string]$PRIORITY = 0
)

az extension add --name azure-iot

.github/scripts/tokenizeFiles.ps1 `
     -TokenPrefix '"$' `
     -TokenSuffix '"' `
     -Path LoRaEngine/. `
     -FileFilter 'deployment.*.json'

Write-Output $NEW_DEPLOYMENT_NAME

Get-Content -Path 'LoRaEngine/deployment.lbs.template.json' | Write-Output

az iot edge deployment create `
        --content $DEPLOYMENT_FILE_PATH `
        --deployment-id "$NEW_DEPLOYMENT_NAME" `
        --login "$Env:IOTHUB_CONNECTION_STRING" `
        --target-condition "deviceId='$TARGET_DEVICE_ID'" `
        --priority $PRIORITY

if (!$?) {
    throw "Creating deployment $($DEPLOYMENT_NAME) failed."
}

# removing unused deployments
.github/scripts/remove-edge-deployment.ps1 -NEW_DEPLOYMENT_NAME $NEW_DEPLOYMENT_NAME
