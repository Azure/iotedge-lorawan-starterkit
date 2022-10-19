using namespace System.Collections.Concurrent

param(
  [array]$array
)

$map = [ConcurrentDictionary[string, object]]::new()

$array | ForEach-Object -Parallel {
  $key = $_
  $val = az keyvault secret show --name $key --vault-name "lorae2etestkeyvault" --query value -o tsv 
  $collector = $using:map
  $res = $collector.TryAdd($key, $val)
} -ThrottleLimit 10

foreach($key in $map.Keys)
{
  $val = $map[$key]
  $key.Replace('-','_')
  "$key= $val" >> $env:GITHUB_ENV
  Write-Host("::add-mask::$val")
}
