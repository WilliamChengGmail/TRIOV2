@echo off
chcp 65001 >nul
title TRIOV2 Database Initializer
cd /d "%~dp0DbInitializer"
if exist "DbInitializer.exe" (
    "DbInitializer.exe" %*
) else (
    dotnet run --project "%~dp0..\src\Tools\DbInitializer\DbInitializer.csproj" -- %*
)
pause

