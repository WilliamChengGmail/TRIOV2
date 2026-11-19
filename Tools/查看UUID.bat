@echo off
chcp 65001 >nul
title TRIOV2 UUID Tool
cd /d "%~dp0UuidTool"
if exist "UuidTool.exe" (
    "UuidTool.exe" %*
) else (
    dotnet run --project "%~dp0..\src\Tools\UuidTool\UuidTool.csproj" -c Debug
)
pause
