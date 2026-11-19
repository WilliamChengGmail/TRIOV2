@echo off
chcp 65001 >nul
title TRIOV2 Account Tool
cd /d "%~dp0AccountTool"
if exist "AccountTool.exe" (
    "AccountTool.exe" %*
) else (
    dotnet run --project "%~dp0..\src\Tools\AccountTool" -- %*
)
if %ERRORLEVEL% neq 0 pause
