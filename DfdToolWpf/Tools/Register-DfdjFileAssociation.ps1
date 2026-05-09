param(
    [string]$ExePath
)

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $ExePath = Join-Path (Split-Path -Parent $PSScriptRoot) "bin\Release\net8.0-windows\DfdToolWpf.exe"
}

$ExePath = [System.IO.Path]::GetFullPath($ExePath)

if (-not (Test-Path $ExePath)) {
    Write-Error "EXE が見つかりません: $ExePath"
    exit 1
}

$extensionKey = "HKCU:\Software\Classes\.dfdj"
$fileTypeKey = "HKCU:\Software\Classes\DfdToolWpf.dfdj"
$commandKey = "$fileTypeKey\shell\open\command"

New-Item -Path $extensionKey -Force | Out-Null
Set-ItemProperty -Path $extensionKey -Name "(default)" -Value "DfdToolWpf.dfdj"

New-Item -Path $fileTypeKey -Force | Out-Null
Set-ItemProperty -Path $fileTypeKey -Name "(default)" -Value "DFD Tool 図ファイル"

New-Item -Path $commandKey -Force | Out-Null
Set-ItemProperty -Path $commandKey -Name "(default)" -Value "`"$ExePath`" `"%1`""

Write-Host ".dfdj を DfdToolWpf に関連付けました。"
Write-Host "EXE: $ExePath"
