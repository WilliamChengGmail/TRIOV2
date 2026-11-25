@echo off
chcp 65001 >nul
title TRIOV2 Deploy to USB Tool

:: ====================================================
:: TRIOV2 USB Deployment Packager
:: Package App + PrivilegedService + DevLauncher + Simulator + Database to USB
:: Author: Office of William
:: ====================================================

echo ==================================================
echo   TRIOV2 Deploy to USB Tool
echo ==================================================
echo.

set TARGET_ARG=%~1
set USB_DRIVE=
set DEPLOY_DIR=
set PROJECT_ROOT=%~dp0..

if not "%TARGET_ARG%"=="" goto :USE_ARG
goto :CHECK_USB

:USE_ARG
set DEPLOY_DIR=%TARGET_ARG%
if "%DEPLOY_DIR:~-1%"=="\" set DEPLOY_DIR=%DEPLOY_DIR:~0,-1%
if "%DEPLOY_DIR:~1,1%"==":" if "%DEPLOY_DIR:~2%"=="" set DEPLOY_DIR=%DEPLOY_DIR%\TRIOV2_Deploy
goto :START_DEPLOY

:CHECK_USB
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

:START_DEPLOY
echo [OK] Target Directory: %DEPLOY_DIR%
echo.

:: Step 1: Clean old deploy directory
if exist "%DEPLOY_DIR%" (
    echo [1/7] Cleaning old deployment directory...
    rmdir /s /q "%DEPLOY_DIR%"
)

:: Step 2: Publish TRIOV2.App (self-contained)
echo [2/7] Publishing TRIOV2.App (Release, win-x64, self-contained)...
dotnet publish "%PROJECT_ROOT%\src\TRIOV2.App\TRIOV2.App.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\App"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to publish TRIOV2.App!
    pause
    exit /b 1
)
echo       Done.

:: Step 3: Publish TRIOV2.PrivilegedService (self-contained)
echo [3/7] Publishing TRIOV2.PrivilegedService (Release, win-x64, self-contained)...
dotnet publish "%PROJECT_ROOT%\src\TRIOV2.PrivilegedService\TRIOV2.PrivilegedService.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\Service"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to publish TRIOV2.PrivilegedService!
    pause
    exit /b 1
)
echo       Done.

:: Step 4: Publish TRIOV2.DevLauncher (self-contained)
echo [4/7] Publishing TRIOV2.DevLauncher (Release, win-x64, self-contained)...
dotnet publish "%PROJECT_ROOT%\src\Tools\DevLauncher\DevLauncher.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\DevLauncher_bin"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to publish TRIOV2.DevLauncher!
    pause
    exit /b 1
)
echo       Done.

:: Step 5: Publish TRIOV2.Simulator (self-contained)
echo [5/7] Publishing TRIOV2.Simulator (Release, win-x64, self-contained)...
dotnet publish "%PROJECT_ROOT%\src\Tools\TRIOV2.Simulator\TRIOV2.Simulator.csproj" -c Release -r win-x64 --self-contained true -o "%DEPLOY_DIR%\Simulator_bin"
if errorlevel 1 (
    echo.
    echo [ERROR] Failed to publish TRIOV2.Simulator!
    pause
    exit /b 1
)
echo       Done.

:: Step 6: Copy default databases
echo [6/7] Copying system databases to App\Database...
mkdir "%DEPLOY_DIR%\App\Database" 2>nul
copy /y "%PROJECT_ROOT%\Database\*.db" "%DEPLOY_DIR%\App\Database\" >nul
echo       Done.

:: Step 7: Clean non-Windows runtimes and pdb files
echo [7/7] Cleaning non-Windows runtimes and symbol files...
for %%t in (App Service DevLauncher_bin Simulator_bin) do (
    if exist "%DEPLOY_DIR%\%%t\runtimes" (
        for /d %%d in ("%DEPLOY_DIR%\%%t\runtimes\*") do (
            echo %%~nxd | findstr /i "^win" >nul || rmdir /s /q "%%d" 2>nul
        )
    )
    del /q "%DEPLOY_DIR%\%%t\*.pdb" 2>nul
)
echo       Done.

:: Create Screenshots folder
mkdir "%DEPLOY_DIR%\Screenshots" 2>nul

:: Create launchers on USB drive
echo Creating launcher scripts on deployment directory...

:: 1. TRIOV2 App Launchers
(
    echo @echo off
    echo chcp 65001 ^>nul
    echo title TRIOV2 App Launcher
    echo cd /d "%%~dp0App"
    echo start "" "TRIOV2.App.exe"
) > "%DEPLOY_DIR%\StartTRIOV2.bat"
copy /y "%DEPLOY_DIR%\StartTRIOV2.bat" "%DEPLOY_DIR%\啟動App.bat" >nul

:: 2. PrivilegedService Launchers
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
copy /y "%DEPLOY_DIR%\StartPrivilegedService.bat" "%DEPLOY_DIR%\啟動特權服務.bat" >nul

:: 3. DevLauncher (Simulator) Launchers
(
    echo @echo off
    echo chcp 65001 ^>nul
    echo title TRIOV2 DevLauncher ^(UI ^& Touch Simulator^)
    echo cd /d "%%~dp0DevLauncher_bin"
    echo start "" "TRIOV2.DevLauncher.exe"
) > "%DEPLOY_DIR%\StartSimulator.bat"
copy /y "%DEPLOY_DIR%\StartSimulator.bat" "%DEPLOY_DIR%\啟動模擬器.bat" >nul

:: 4. Hardware Simulator Launchers
(
    echo @echo off
    echo chcp 65001 ^>nul
    echo title TRIOV2 Hardware Simulator
    echo cd /d "%%~dp0Simulator_bin"
    echo start "" "TRIOV2.Simulator.exe"
) > "%DEPLOY_DIR%\StartHardwareSimulator.bat"
copy /y "%DEPLOY_DIR%\StartHardwareSimulator.bat" "%DEPLOY_DIR%\啟動硬體模擬器.bat" >nul

echo       Done.

echo.
echo ==================================================
echo   Deployment Successful!
echo   Location: %DEPLOY_DIR%
echo.
echo   How to run on target machine:
echo   1. Run %DEPLOY_DIR%\啟動特權服務.bat (As Admin)
echo   2. Run %DEPLOY_DIR%\啟動模擬器.bat (DevLauncher UI Simulator)
echo      OR %DEPLOY_DIR%\啟動App.bat (Direct App)
echo      OR %DEPLOY_DIR%\啟動硬體模擬器.bat (Hardware Simulator)
echo ==================================================
echo.
if "%CI_TEST%"=="" pause