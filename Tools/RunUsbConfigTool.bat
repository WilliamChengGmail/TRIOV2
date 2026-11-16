@echo off
chcp 65001 >nul
echo Starting TRIO2026 USB Config Tool...
"%~dp0UsbConfigTool\UsbConfigTool.exe"
pause
