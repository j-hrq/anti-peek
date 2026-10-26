@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "SOURCE_FILE=%SCRIPT_DIR%AntiPeek.cs"
set "OUTPUT_EXE=%SCRIPT_DIR%AntiPeek.exe"

set "CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC_PATH%" (
    set "CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)

if not exist "%CSC_PATH%" (
    echo [ERRO] Compilador csc.exe nativo nao encontrado em C:\Windows\Microsoft.NET.
    exit /b 1
)

echo Compilando AntiPeek.exe usando %CSC_PATH% ...
"%CSC_PATH%" /target:winexe /optimize+ /nologo /out:"%OUTPUT_EXE%" /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll "%SOURCE_FILE%"

if %ERRORLEVEL% equ 0 (
    echo Sucesso! Gerado: %OUTPUT_EXE%
) else (
    echo [ERRO] Falha ao compilar AntiPeek.exe
    exit /b 1
)
