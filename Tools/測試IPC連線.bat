@echo off
chcp 65001 >nul
echo ==================================================
echo   TRIO2026 PrivilegedService IPC Test Tool
echo   No admin rights needed (simulates App side)
echo ==================================================
echo.
"%~dp0ServiceTestTool\ServiceTestTool.exe"
pause
