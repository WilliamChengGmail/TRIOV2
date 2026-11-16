@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo Starting TRIO2026 USB Config Tool...
dotnet run -c Release --project "%~dp0..\src\Tools\UsbConfigTool\UsbConfigTool.csproj"
pause