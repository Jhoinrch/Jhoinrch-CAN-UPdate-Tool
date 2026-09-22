# Jhoinrch CAN UPdate Tool

STM32 USB DFU firmware upgrade tool for Windows. Designed for Jhoinrch / CANable-style USB-CAN adapters based on STM32.

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

1. Put the device into DFU mode (usually hold BOOT0 while powering on)
2. If the device is not detected, click **ZADIG**, pick `DFU in FS Mode` → WinUSB → Replace Driver
3. Click **Refresh** to enumerate devices and select the target
4. **Browse** to choose a `.dfu` or `.bin` file, then **Parse** to inspect it
5. For `.bin`, fill in the flash address (default `0x08000000`)
6. Click **Flash** to download, or **Read** to export firmware

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
