# Builds the cli and prepares it to be updated to a release
$DotNetVersion="net6.0"

Write-Host "📦 Build and package Linux x64 version..." -ForegroundColor DarkYellow
$LinuxDestinationRelativePath="./bin/Release/$DotNetVersion/linux-x64/lora-cli.linux-x64.tar.gz"
dotnet publish -r linux-x64 /p:PublishSingleFile=true --self-contained -c Release --verbosity quiet
tar -czf $LinuxDestinationRelativePath -C "./bin/Release/$DotNetVersion/linux-x64/publish" .

Write-Host "📦 Build and package Linux musl x64 version..." -ForegroundColor DarkYellow
$LinuxMuslDestinationRelativePath="./bin/Release/$DotNetVersion/linux-musl-x64/lora-cli.linux-musl-x64.tar.gz"
dotnet publish -r linux-musl-x64 /p:PublishSingleFile=true --self-contained -c Release --verbosity quiet
tar -czf $LinuxMuslDestinationRelativePath -C "./bin/Release/$DotNetVersion/linux-musl-x64/publish" .

Write-Host "📦 Build and package Win x64 version..." -ForegroundColor DarkYellow
$WindowsDestinationRelativePath="./bin/Release/$DotNetVersion/win-x64/lora-cli.win-x64.zip"
dotnet publish -r win-x64 /p:PublishSingleFile=true --self-contained -c Release --verbosity quiet
Compress-Archive -Force -Path ".\bin\Release\$DotNetVersion\win-x64\publish\" -DestinationPath $WindowsDestinationRelativePath

Write-Host "📦 Build and package OSX x64 version..." -ForegroundColor DarkYellow
$OsxDestinationRelativePath=".\bin\Release\$DotNetVersion\osx-x64\lora-cli.osx-x64.zip"
dotnet publish -r osx-x64 /p:PublishSingleFile=true --self-contained -c Release --verbosity quiet
Compress-Archive -Force -Path ".\bin\Release\$DotNetVersion\osx-x64\publish\" -DestinationPath $OsxDestinationRelativePath

Write-Host "🥳 Build complete!" -ForegroundColor Green
Write-Host "Linux x64      -> " -ForegroundColor DarkYellow -NoNewline
Write-Host "$((Get-Item $LinuxDestinationRelativePath).FullName)"
Write-Host "Linux musl x64 -> " -ForegroundColor DarkYellow -NoNewline
Write-Host "$((Get-Item $LinuxMuslDestinationRelativePath).FullName)"
Write-Host "Windows x64    -> " -ForegroundColor DarkYellow -NoNewline
Write-Host "$((Get-Item $WindowsDestinationRelativePath).FullName)"
Write-Host "OSX x64        -> " -ForegroundColor DarkYellow -NoNewline
Write-Host "$((Get-Item $OsxDestinationRelativePath).FullName)"

