# KinectWindowsWebCam

Aplikacja WinForms dla Windows 10/11 zamienia Kinect Xbox 360 (v1) w kamerę wirtualną widoczną w Chrome, Discord, OBS i innych aplikacjach obsługujących DirectShow.

## Wymagania

- Windows 10 lub 11
- Kinect for Windows SDK 1.8
- Kinect Xbox 360 (v1) z adapterem zasilająco-USB
- UnityCapture

## Instalacja

1. Zainstaluj Kinect for Windows SDK 1.8: https://www.microsoft.com/en-us/download/details.aspx?id=40278
2. Pobierz UnityCapture: https://github.com/schellingb/UnityCapture
3. Uruchom `Install\Install.bat` z UnityCapture jako administrator.
4. Sklonuj repozytorium i otwórz projekt w Visual Studio 2022.
5. Zbuduj i uruchom aplikację.

## Działanie

- Aplikacja uruchamia się w system tray bez głównego okna.
- Kliknięcie ikonki tray otwiera popup z:
  - suwakiem jakości renderowania źródła (wyjście zawsze 1280x960)
  - suwakiem fizycznego tilt silnika Kinecta z debounce
  - statusem Kinecta
  - przyciskiem Exit
- Strumień do UnityCapture utrzymuje stały format 1280x960 przez cały czas działania.

## Struktura

- `Program.cs` - uruchomienie aplikacji tray i spinanie usług
- `TrayPopupForm.cs` - popup bez obramowania
- `KinectCaptureService.cs` - obsługa Kinect SDK 1.8 i strumienia RGB
- `KinectMotorService.cs` - sterowanie tilt z debounce
- `VirtualCameraWriter.cs` - zapis do UnityCapture przez shared memory
- `FrameScaler.cs` - skalowanie klatek BGRA
- `QualityPreset.cs` - profile jakości renderowania źródła
