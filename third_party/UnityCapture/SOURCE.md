# UnityCapture (DirectShow filter)

Pliki `UnityCaptureFilter32.dll` i `UnityCaptureFilter64.dll` pochodzą bez zmian z
https://github.com/schellingb/UnityCapture, katalog `Install/`, commit
`3ed54c325e0ad71afcf4f246c07e5e17b3d7f2d2`. Licencja: MIT (`LICENSE.txt`).

Są osadzane w `KinectWindowsWebCam.exe` jako zasoby i przy pierwszym uruchomieniu
kopiowane do `%ProgramFiles%\KinectWindowsWebCam` oraz rejestrowane przez `regsvr32`.

| Plik | SHA-256 |
| --- | --- |
| UnityCaptureFilter32.dll | `aa3ebdf03dea7f3aab3dd7b724751f49ed71672256b57c6a19aa6809cabf30ba` |
| UnityCaptureFilter64.dll | `72812f5363d8ecb45632253f8c8c888844b1b62e27616f3c8cc21064ccde25e5` |

Protokół wymiany klatek (zgodny z `Source/shared.inl` z tego samego commitu):

- obiekty: `UnityCapture_Mutx`, `UnityCapture_Want`, `UnityCapture_Sent`, `UnityCapture_Data`
- nadawca otwiera mutex, **tworzy** event `Want`, otwiera event `Sent` i mapowanie `Data`
- nagłówek 32 B: `maxSize, width, height, stride (w pikselach), format, resizemode, mirrormode, timeout`
- dane: RGBA 8 bit, wiersze od dołu obrazu (bottom-up, jak DIB)
- CLSID filtra: x64 `{5C2CD55C-92AD-4999-8666-912BD3E70010}`, x86 `{5C2CD55C-92AD-4999-8666-912BD3E70020}`
