@echo off
setlocal enabledelayedexpansion

set "APP_DIR=%~dp0"
set "APP_EXE=%APP_DIR%KinectWindowsWebCam.exe"
set "UNITYCAPTURE_URL=https://github.com/schellingb/UnityCapture/archive/refs/heads/master.zip"
set "UNITYCAPTURE_ZIP=%TEMP%\UnityCapture-master.zip"
set "UNITYCAPTURE_DIR=%TEMP%\UnityCapture-master"

if not exist "%APP_EXE%" (
  echo Brak pliku KinectWindowsWebCam.exe w folderze release.
  pause
  exit /b 1
)

if "%KINECTSDK10_DIR%"=="" (
  echo Kinect SDK 1.8 nie jest zainstalowany.
  echo Pobierz i zainstaluj SDK: https://www.microsoft.com/en-us/download/details.aspx?id=40278
  start "" "https://www.microsoft.com/en-us/download/details.aspx?id=40278"
  pause
  exit /b 1
)

reg query "HKCR\CLSID\{5C2CD55C-92AD-4999-8666-912BD3E7004F}" >nul 2>nul
if errorlevel 1 (
  echo UnityCapture nie jest wykryty. Trwa pobieranie i instalacja...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -Uri '%UNITYCAPTURE_URL%' -OutFile '%UNITYCAPTURE_ZIP%'"
  if errorlevel 1 (
    echo Nie udalo sie pobrac UnityCapture.
    pause
    exit /b 1
  )
  powershell -NoProfile -ExecutionPolicy Bypass -Command "if (Test-Path '%UNITYCAPTURE_DIR%') { Remove-Item -Recurse -Force '%UNITYCAPTURE_DIR%' }; Expand-Archive -LiteralPath '%UNITYCAPTURE_ZIP%' -DestinationPath '%TEMP%' -Force"
  if errorlevel 1 (
    echo Nie udalo sie rozpakowac UnityCapture.
    pause
    exit /b 1
  )
  set "INSTALL_BAT=%UNITYCAPTURE_DIR%\Install\Install.bat"
  if not exist "%INSTALL_BAT%" (
    echo Nie znaleziono Install.bat UnityCapture.
    pause
    exit /b 1
  )
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%INSTALL_BAT%' -Verb RunAs -Wait"
)

start "" "%APP_EXE%"
exit /b 0
