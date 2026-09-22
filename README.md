# Jhoinrch CAN UPdate Tool

STM32 USB DFU 固件升级工具（Windows 桌面版），面向 Jhoinrch / CANable 等基于 STM32 的 USB-CAN 适配器。

## 功能

- **设备枚举**：自动列出 DFU 模式设备（支持 STM32 多 alternate setting 合并显示）
- **驱动安装**：一键打开 Zadig，为 DFU 设备绑定 WinUSB 驱动
- **固件解析**：支持 `.dfu`（DfuSe）与 `.bin`，解析地址 / 大小 / VID:PID / CRC 校验
- **固件烧录**：通过 `dfu-util` 下载固件，完成后自动 `:leave` 启动新固件
- **固件读取**：按地址与长度导出设备内固件为 `.bin`
- **进度与日志**：实时进度条、完整 `dfu-util` 输出日志、可取消操作
- **单文件分发**：嵌入 `dfu-util` / `libusb` / Zadig，无需安装 .NET 即可运行

## 技术栈

| 项 | 说明 |
|---|---|
| 框架 | .NET 9（`net9.0-windows`）+ WPF |
| 固件协议 | USB DFU / ST DfuSe |
| 底层工具 | dfu-util 0.11、libusb-1.0、Zadig |
| 发布 | Single-file、自包含、win-x64 |

## 项目结构

```
DFU/
├── MainWindow.xaml / .cs   # 主界面与交互
├── DfuUtilRunner.cs        # dfu-util 定位 / 释放 / 调用
├── DfuFile.cs              # .dfu / .bin 固件解析
├── Crc32.cs                # DfuSe 后缀 CRC-32 校验
├── native/                 # 嵌入的 dfu-util、libusb、Zadig 等
└── DfuTool.csproj
```

## 使用说明

1. 设备进入 DFU 模式（通常按住 BOOT0 上电）
2. 若设备未识别，点击 **ZADIG**，选择 `DFU in FS Mode` → WinUSB → Replace Driver
3. **Refresh** 枚举设备，选择目标设备
4. **Browse** 选择 `.dfu` 或 `.bin` 固件，**Parse** 查看信息
5. （`.bin` 需手动填写烧录地址，默认 `0x08000000`）
6. **Flash** 开始烧录，或 **Read** 导出固件

## 构建

```bash
# 需要 .NET 9 SDK
dotnet build DFU/DfuTool.csproj -c Release

# 发布单文件 exe
dotnet publish DFU/DfuTool.csproj -c Release -r win-x64 --self-contained true
```

产物路径：

```
DFU/bin/Release/net9.0-windows/win-x64/publish/Jhoinrch CAN Tool.exe
```

## 相关链接

- [Get Firmware](https://canable.io/builds/)
- [Jhoinrch.net](https://Jhoinrch.net)

## License

仅供配套硬件固件升级使用。
