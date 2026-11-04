# KinectWindowsWebCam

Kinect Xbox 360 (v1) jako kamera internetowa w Windows 10/11 — widoczna w Discordzie,
Chrome, OBS, Zoomie i innych aplikacjach DirectShow pod nazwą **Kinect WebCam**.

## Uruchomienie

1. Pobierz `KinectWindowsWebCam.exe` z zakładki **Releases** (jeden plik, ok. 400 KB).
2. Uruchom go. Przy pierwszym starcie Windows raz poprosi o uprawnienia administratora —
   aplikacja rejestruje wtedy wbudowany sterownik kamery wirtualnej. Kolejne uruchomienia
   już o nic nie pytają.
3. Ikona pojawi się w zasobniku obok zegara. W Discordzie/Chrome/OBS wybierz kamerę
   **Kinect WebCam**.

Nie trzeba instalować .NET ani UnityCapture — .NET Framework 4.8 jest częścią
Windows 10 (1903+) i 11, a filtr kamery jest wbudowany w plik `.exe`.

Jedyne, czego nie da się wbudować, to **sterownik Kinecta**: Kinect for Xbox 360 działa
na Windows tylko z zainstalowanym
[Kinect for Windows SDK 1.8](https://www.microsoft.com/en-us/download/details.aspx?id=40278)
(sam „Runtime” nie obsługuje wersji Xbox 360). Jeśli go brakuje, aplikacja pokaże to
w statusie i da przycisk do pobrania.

Kinect wymaga zasilacza z adapterem USB — sam kabel z konsoli nie zadziała na PC.

## Okno po kliknięciu ikony

- **Jakość źródła** — rozdzielczość, w jakiej obraz jest przekazywany do kamery
  wirtualnej (320/480/640 px szerokości); aplikacja docelowa i tak dostaje taką
  rozdzielczość, jaką sama wybierze (skalowanie liniowe po stronie filtra).
- **Nachylenie** — fizyczny silnik Kinecta (−27°…+27°). Zmiany są wysyłane z opóźnieniem
  i nie częściej niż co 1,4 s, bo Kinect blokuje silnik przy zbyt częstych ruchach.
- **Kadr 16:9** — przycina obraz 4:3 do 16:9, żeby nie był rozciągnięty w aplikacjach,
  które proszą o 1280×720 (Discord, Meet, Teams, Zoom). Wyłącz dla aplikacji 4:3.
- **Odbicie lustrzane** — odwraca obraz w poziomie.
- Status Kinecta i kamery wirtualnej oraz przycisk zamknięcia aplikacji.

## Odinstalowanie

```
KinectWindowsWebCam.exe --uninstall
```

Wyrejestrowuje filtr kamery i usuwa `%ProgramFiles%\KinectWindowsWebCam`.

## Budowanie

```
dotnet build KinectWindowsWebCam.csproj -c Release
```

Wynik: `bin/Release/net48/KinectWindowsWebCam.exe` (wystarczy sam ten plik).
Workflow `.github/workflows/release.yml` buduje go na `windows-latest`; push taga `v*`
tworzy GitHub Release z plikiem `.exe`, ręczne uruchomienie workflow daje go jako artefakt.

## Jak to działa

- `KinectCaptureService.cs` — wątek przechwytywania; Kinect SDK 1.8 przez natywny
  `Kinect10.dll` (`INuiSensor`), strumień kolorowy 640×480 @ 30 fps, automatyczne
  ponowne łączenie i obsługa silnika.
- `KinectNative.cs`, `NativeMethods.cs` — P/Invoke i wywołania COM przez vtable.
- `FrameConverter.cs` — BGRX → RGBA, skalowanie, kadrowanie 16:9, kolejność wierszy
  wymagana przez filtr.
- `VirtualCameraWriter.cs` — wysyłanie klatek do filtra UnityCapture przez pamięć
  współdzieloną; klatka jest liczona tylko, gdy jakaś aplikacja faktycznie używa kamery.
- `VirtualCameraInstaller.cs` — wypakowanie i rejestracja filtra (x64 i x86) z jednorazowym
  podniesieniem uprawnień.
- `TrayApplicationContext.cs`, `TrayPopupForm.cs` — ikona w zasobniku i okno ustawień.
- `TiltScheduler.cs` — debounce i limit częstotliwości silnika.
- `third_party/UnityCapture` — filtr DirectShow
  [UnityCapture](https://github.com/schellingb/UnityCapture) (MIT), szczegóły w `SOURCE.md`.
