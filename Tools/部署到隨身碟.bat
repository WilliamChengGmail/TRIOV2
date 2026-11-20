@echo off
chcp 65001 >nul
title TRIOV2 Deploy to USB Tool

:: ====================================================
:: TRIOV2 USB Deployment Packager
:: Package App + PrivilegedService + Database to USB
:: Author: Office of William
:: ====================================================

echo ==================================================
echo   TRIOV2 Deploy to USB Tool
echo ==================================================
echo.

:: Detect USB drive (DriveType=2: Removable)
set USB_DRIVE=
for /f "tokens=1" %%d in ('wmic logicaldisk where "DriveType=2" get DeviceID /value 2^>nul ^| findstr "="') do (
    for /f "tokens=2 delims==" %%v in ("%%d") do set USB_DRIVE=%%v
)

if "%USB_DRIVE%"=="" (
    echo [INFO] No removable drive detected automatically.
    set /p USB_DRIVE="Please enter USB drive letter (e.g. E: or Q to quit): "
)

if /i "%USB_DRIVE%"=="Q" exit /b 0

if "%USB_DRIVE%"=="" (
    echo [ERROR] USB drive not specified. Exiting...
    pause
    exit /b 1
)

if "%USB_DRIVE:~-1%"=="\" set USB_DRIVE=%USB_DRIVE:~0,-1%
if not exist "%USB_DRIVE%\" (
    echo [ERROR] Cannot find drive %USB_DRIVE%\. Please make sure USB drive is plugged in.
    pause
    exit /b 1
)

echo [OK] Target USB Drive: %USB_DRIVE%\
set DEPLOY_DIR=%USB_DRIVE%\TRIOV2_Deploy
set PROJECT_ROOT=%~dp0..

echo [OK] Target Directory: %DEPLOY_DIR%
echo.

:: Step 1: Clean old deploy directory
if exist "%DEPLOY_DIR%" (
    echo [1/5] Cleaning old deployment directory...
    rmdir /s /q "%DEPLOY_DIR%"
)

:: Step 2: Publish TRIOV2.App (self-contained)
echo [2/5] Publishing TRIOV2.App (Release, win-x64, self-contained)...
dotnet publish "%PROJECT_ROOT%\src\TRIOV2.App\TRIOV2.App.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\App"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to publish TRIOV2.App!
    pause
    exit /b 1
)
echo       Done.

:: Step 3: Publish TRIOV2.PrivilegedService (self-contained)
echo [3/5] Publishing TRIOV2.PrivilegedService (Release, win-x64, self-contained)...
dotnet publish "%PROJECT_ROOT%\src\TRIOV2.PrivilegedService\TRIOV2.PrivilegedService.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\Service"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to publish TRIOV2.PrivilegedService!
    pause
    exit /b 1
)
echo       Done.

:: Step 4: Copy default databases
echo [4/5] Copying system databases to App\Database...
mkdir "%DEPLOY_DIR%\App\Database" 2>nul
copy /y "%PROJECT_ROOT%\Database\*.db" "%DEPLOY_DIR%\App\Database\" >nul
echo       Done.

:: Step 5: Clean non-Windows runtimes
echo [5/5] Cleaning non-Windows runtimes...
for /d %%d in ("%DEPLOY_DIR%\App\runtimes\*") do (
    echo %%~nxd | findstr /i "^win" >nul || rmdir /s /q "%%d" 2>nul
)
for /d %%d in ("%DEPLOY_DIR%\Service\runtimes\*") do (
    echo %%~nxd | findstr /i "^win" >nul || rmdir /s /q "%%d" 2>nul
)
echo       Done.

:: Create launchers on USB drive
echo Creating launcher scripts on USB root...
(
    echo @echo off
    echo chcp 65001 ^>nul
    echo title TRIOV2 App Launcher
    echo cd /d "%%~dp0App"
    echo start "" "TRIOV2.App.exe"
) > "%DEPLOY_DIR%\StartTRIOV2.bat"

(
    echo @echo off
    echo chcp 65001 ^>nul
    echo title TRIOV2 PrivilegedService ^(Console Mode^)
    echo net session ^>nul 2^>^&1
    echo if %%ERRORLEVEL%% neq 0 ^(
    echo     echo [ERROR] Please run as Administrator!
    echo     pause
    echo     exit /b 1
    echo ^)
    echo cd /d "%%~dp0Service"
    echo "TRIOV2.PrivilegedService.exe"
    echo pause
) > "%DEPLOY_DIR%\StartPrivilegedService.bat"

echo       Done.

echo.
echo ==================================================
echo   Deployment Successful!
echo   Location: %DEPLOY_DIR%
echo.
echo   How to run on target machine:
echo   1. Run %DEPLOY_DIR%\StartPrivilegedService.bat (As Admin)
echo   2. Run %DEPLOY_DIR%\StartTRIOV2.bat
echo ==================================================
echo.
pause
