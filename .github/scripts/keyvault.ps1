param(
  [array]$array
)

$array | ForEach-Object -Parallel {
  $key = $_
  $val = az keyvault secret show --name $key --vault-name "nabki-keyvault" --query value -o tsv 
  "$key = $val" >> $env:GITHUB_ENV
  Write-Host("::add-mask::$val")
} -ThrottleLimit 10