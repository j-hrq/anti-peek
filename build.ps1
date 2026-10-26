$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceFile = Join-Path $scriptDir "AntiPeek.cs"
$outputExe  = Join-Path $scriptDir "AntiPeek.exe"

# Localiza o compilador C# nativo do Windows (.NET Framework 4.x)
$cscPath = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $cscPath)) {
    $cscPath = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}

if (-not (Test-Path $cscPath)) {
    Write-Error "Compilador csc.exe nativo nao encontrado em C:\Windows\Microsoft.NET."
    exit 1
}

Write-Host "Compilando AntiPeek.exe usando $cscPath ..." -ForegroundColor Cyan

& $cscPath `
    /target:winexe `
    /optimize+ `
    /nologo `
    /out:"$outputExe" `
    /r:System.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    "$sourceFile"

if ($LASTEXITCODE -eq 0 -and (Test-Path $outputExe)) {
    $sizeKB = [Math]::Round((Get-Item $outputExe).Length / 1KB, 2)
    Write-Host "Sucesso! Gerado: $outputExe ($sizeKB KB)" -ForegroundColor Green
} else {
    Write-Error "Falha ao compilar AntiPeek.exe"
    exit 1
}
