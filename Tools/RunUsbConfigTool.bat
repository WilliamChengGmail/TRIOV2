@echo off
chcp 65001 >nul
title TRIOV2 USB Config Tool
cd /d "%~dp0UsbConfigTool"
if exist "UsbConfigTool.exe" (
    start "" "UsbConfigTool.exe" %*
) else (
    echo Starting TRIOV2 USB Config Tool via dotnet run...
    dotnet run -c Release --project "%~dp0..\src\Tools\UsbConfigTool\UsbConfigTool.csproj"
)
