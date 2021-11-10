# At the moment, this script is not used. It will be used in the future if the CI moved away from Edge toolkit

param
(
    [string]$NEW_DEPLOYMENT_NAME
)


Write-Output "*** Cleaning iot edge deployments ***"
$items=az iot edge deployment list --login "$Env:IOTHUB_CONNECTION_STRING"  | ConvertFrom-Json

if (!$items) {
    throw "Unable to list deployments during cleaning"
}

$items | ForEach-Object  {
    if (-not ($_.id.Contains($NEW_DEPLOYMENT_NAME))){
        if($_.id -notmatch "opcplc"){
            Write-Output "Deleting deployment $($_.id)`n"

            az iot edge deployment delete -d "$($_.id)" --login "$Env:IOTHUB_CONNECTION_STRING" | ConvertFrom-Json

            if (!$?) {
                throw "Deleting deployment $($_.id) failed."
            }
        }
    }
}
