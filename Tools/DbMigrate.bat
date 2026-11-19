@echo off
chcp 65001 >nul
title TRIOV2 Database Migration Tool
cd /d "%~dp0DbMigrate"
if exist "DbMigrate.exe" (
    "DbMigrate.exe" %*
) else (
    dotnet run --project "%~dp0..\src\Tools\DbMigrate\DbMigrate.csproj" -- %*
)
pause

