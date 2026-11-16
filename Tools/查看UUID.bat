@echo off
cd /d "%~dp0"
dotnet run --project "%~dp0..\src\Tools\UuidTool\UuidTool.csproj" -c Debug
pause