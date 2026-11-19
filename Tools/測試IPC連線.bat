@echo off
chcp 65001 >nul
title TRIOV2 IPC Test Tool
echo ==================================================
echo   TRIOV2 PrivilegedService IPC Test Tool
echo   No admin rights needed (simulates App side)
echo ==================================================
echo.
cd /d "%~dp0ServiceTestTool"
if exist "ServiceTestTool.exe" (
    "ServiceTestTool.exe" %*
) else (
    dotnet run --project "%~dp0..\src\Tools\ServiceTestTool\ServiceTestTool.csproj"
)
pause
