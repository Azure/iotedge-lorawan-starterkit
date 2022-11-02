using namespace System.Collections.Concurrent

param(
  [array]$array
)

param(
  [string]$vaultName
)

$map = [ConcurrentDictionary[string, object]]::new()

$array | ForEach-Object -Parallel {
  $key = $_
  $val = az keyvault secret show --name $key --vault-name $vaultName --query value -o tsv 
  $collector = $using:map
  $res = $collector.TryAdd($key, $val)
} -ThrottleLimit 10

foreach($key in $map.Keys)
{
  $val = $map[$key]
  $modified_key = $key.replace('-','_')
  "$modified_key=$val" >> $env:GITHUB_ENV
  Write-Host("::add-mask::$val")
}