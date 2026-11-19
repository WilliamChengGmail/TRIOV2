@echo off
chcp 65001 >nul
title TRIOV2 EventLog Viewer
cd /d "%~dp0QueryEvents"
if exist "QueryEvents.exe" (
    "QueryEvents.exe" %*
) else (
    dotnet run --project "%~dp0..\src\Tools\QueryEvents" -- %*
)
pause
