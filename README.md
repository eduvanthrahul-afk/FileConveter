# OrbitConvert

## Download the Windows app

[Download the complete Windows x64 package](OrbitConvert-Windows-x64.zip). Extract the ZIP, then run `OrbitConvert.exe`. Keep the included DLL files in the same folder as the EXE.

This build is unsigned, so Windows Smart App Control may block it.

OrbitConvert is a Windows tray utility. Launch it and the translucent circle opens in the center of the screen. Hold a file in Explorer, press **Shift**, and move the file onto the circle. Its format wedges appear; release on a wedge to convert, or release in the center and click a format. The app writes a new file beside the original with `.converted` in its name.

## Offline behavior and supported formats

The app does not send files over the network. Image conversion uses Windows Imaging Component through WPF and supports PNG, JPG/JPEG, BMP, GIF, and TIFF. Animated images are currently reduced to their first frame.

Audio/video options appear when FFmpeg is available locally. Document options appear when LibreOffice is available locally. Put `ffmpeg.exe` or `soffice.exe` in an `engines` folder beside the app, or install the tool and make it available on `PATH`. Those tools and their codecs are not bundled in this source project.

No program can convert every file format to every other format: formats can be proprietary, encrypted, damaged, or semantically incompatible. OrbitConvert only offers formats its local conversion engine can handle.

## Build on Windows

Install the .NET 8 SDK, open PowerShell in this folder, and run:

```powershell
dotnet publish .\OrbitConvert.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

The executable will be in `bin\Release\net8.0-windows\win-x64\publish`. Right-click its tray icon for **Open OrbitConvert** or **Exit**. Press Escape or the × button to dismiss the circle.

## Notes

- The Shift trigger opens while the left mouse button is held; Windows only provides the file to the app when the drag enters the circle.
- One file is handled at a time.
- Conversions are run locally by Windows or the selected local engine. There is no cloud service or upload path.
- FFmpeg and LibreOffice availability controls which format choices are shown.