# KinectWindowsWebCam

Aplikacja WinForms dla Windows 10/11 zamienia Kinect Xbox 360 (v1) w kamerę wirtualną widoczną w Chrome, Discord, OBS i innych aplikacjach obsługujących DirectShow.

## Gotowy release (.exe)

- W zakładce Releases pobierz `KinectWindowsWebCam-win-x64.zip`.
- Rozpakuj paczkę.
- Przy pierwszym uruchomieniu odpal `Start-KinectWindowsWebCam.bat` jako administrator.
- Skrypt:
  - sprawdza obecność Kinect SDK 1.8,
  - doinstalowuje UnityCapture (jeśli brak),
  - uruchamia `KinectWindowsWebCam.exe`.

## Wymagania

- Windows 10 lub 11
- Kinect for Windows SDK 1.8
- Kinect Xbox 360 (v1) z adapterem zasilająco-USB

## Automatyczne wydanie release

- Workflow `.github/workflows/release.yml` buduje self-contained single-file `KinectWindowsWebCam.exe` dla `win-x64`.
- Push taga `v*` tworzy GitHub Release i dodaje pliki do pobrania.

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
