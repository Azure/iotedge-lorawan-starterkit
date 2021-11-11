# At the moment, this script is not used. It will be used in the future if the CI moved away from Edge toolkit

param
(
    [Parameter(Mandatory = $True)]
    [string]$TokenPrefix,
    [Parameter(Mandatory = $True)]
    [string]$TokenSuffix,
    [Parameter(Mandatory = $True)]
    [string]$Path,
    [Parameter(Mandatory = $True)]
    [string]$FileFilter
)

Write-Verbose "Prefix = $TokenPrefix" -Verbose
Write-Verbose "Suffix = $TokenSuffix" -Verbose

# Need to find matching keys in the target files and use them to get variable values
$prefix = [regex]::Escape($TokenPrefix)
$suffix = [regex]::Escape($TokenSuffix)
$regex = [regex] "${prefix}((?:(?!${suffix}).)*)${suffix}"
Write-Verbose "regex: ${regex}" -Verbose

$ErrorActionPreference = "Continue"

$replaceCallback = {
    param(
        [System.Text.RegularExpressions.Match] $Match
    )

    $matchValue = $Match.Groups[1].Value

    if (Test-Path env:$matchValue ) {
        $value = (Get-ChildItem env:$matchValue).Value
        Write-Verbose "Found token '$($matchValue)' in variables" -Verbose
    }
    else {
        $value = $Match.Value
        Write-Error "Variable '$($matchValue)' not found. Kept token."
    }

    Write-Verbose "Replacing '$($Match.Value)'"
    $value
}

Get-ChildItem $Path -Filter $FileFilter |
Foreach-Object {

    $file = Get-Childitem -path $_.FullName

    $desiredFile = New-Object System.IO.StreamReader($file)
    $content = $desiredFile.ReadToEnd()
    $desiredFile.Close()
    $desiredFile.Dispose()

    $content = $regex.Replace($content, $replaceCallback)

    # Re-open the file this time with streamwriter
    $desiredFile = New-Object System.IO.StreamWriter($file)

    # Insert the $text to the file and close
    $desiredFile.Write($content -join "`r`n")
    $desiredFile.Flush()
    $desiredFile.Close()
}
