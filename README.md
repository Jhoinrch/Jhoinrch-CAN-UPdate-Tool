# Jhoinrch CAN UPdate Tool

STM32 USB DFU firmware upgrade tool for Windows. Designed for Jhoinrch / CANable-style USB-CAN adapters based on STM32.

> [!IMPORTANT]
> ## [📘 How to use — step-by-step Usage Guide](docs/USAGE.md)
> Click the link above for the full tutorial with screenshots: get firmware, DFU mode, driver install, flash / read.

# [**➡️ Open the Usage Guide**](docs/USAGE.md)

## Features

- **Device enumeration** — list DFU-mode devices (STM32 multi alternate settings are merged into one row)
- **Driver install** — open Zadig to bind WinUSB for the DFU device
- **Firmware parse** — support `.dfu` (DfuSe) and `.bin`; show address / size / VID:PID / CRC check
- **Firmware flash** — download firmware with `dfu-util`, then `:leave` to boot the new image
- **Firmware read** — export on-device firmware to `.bin` by address and length
- **Progress & log** — live progress bar, full `dfu-util` output, cancellable operations
- **Single-file distribution** — embeds `dfu-util` / `libusb` / Zadig; no .NET install required

## Tech Stack

| Item | Description |
|---|---|
| Framework | .NET 9 (`net9.0-windows`) + WPF |
| Protocol | USB DFU / ST DfuSe |
| Native tools | dfu-util 0.11, libusb-1.0, Zadig |
| Publish | Single-file, self-contained, win-x64 |

## Project Layout

```
DFU/
├── MainWindow.xaml / .cs   # Main UI and interaction
├── DfuUtilRunner.cs        # Locate / extract / run dfu-util
├── DfuFile.cs              # .dfu / .bin firmware parser
├── Crc32.cs                # DfuSe suffix CRC-32 check
├── native/                 # Embedded dfu-util, libusb, Zadig, runtimes
└── DfuTool.csproj
```

## Usage

Full step-by-step tutorial with screenshots:

### [📘 How to use — Usage Guide](docs/USAGE.md)

In the app, click the green **How to use** button to open the same guide in your browser.

## Build

Requires the .NET 9 SDK.

```bash
# Build
dotnet build DFU/DfuTool.csproj -c Release

# Publish single-file exe
dotnet publish DFU/DfuTool.csproj -c Release -r win-x64 --self-contained true
```

Output:

```
DFU/bin/Release/net9.0-windows/win-x64/publish/Jhoinrch CAN Tool.exe
```

## Links

- [Get Firmware](https://canable.io/builds/)
- [Jhoinrch.net](https://Jhoinrch.net)

## License

Intended for firmware upgrades of companion hardware only.
