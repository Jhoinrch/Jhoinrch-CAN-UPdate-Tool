using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DfuTool;

/// <summary>解析 .dfu（DfuSe）和 .bin 固件文件，提取地址 / 大小 / 校验信息。</summary>
public static class DfuFileParser
{
    public static DfuFileInfo Parse(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var info = new DfuFileInfo { FilePath = path };

        if (IsDfuSe(bytes))
            ParseDfuSe(bytes, info);
        else
        {
            info.IsDfuFile = false;
            info.Images.Add(new DfuImage { Address = 0x08000000u, Size = (uint)bytes.Length });
        }
        return info;
    }

    /// <summary>判断是否为带 "DFU" 签名后缀的 DfuSe 文件。</summary>
    private static bool IsDfuSe(byte[] b) =>
        b.Length >= 16 && b[^12] == (byte)'D' && b[^11] == (byte)'F' && b[^10] == (byte)'U';

    private static void ParseDfuSe(byte[] b, DfuFileInfo info)
    {
        // 文件前缀: "DfuSe"(5) + 版本(2) + bcdDFU(2) + idVendor(2) + bTargets(1)
        if (b.Length < 12 || Encoding.ASCII.GetString(b, 0, 5) != "DfuSe" || b[5] != 0x01 || b[6] != 0x00)
            return;

        int p = 7;
        info.BcdDfu = ReadU16(b, p); p += 2;
        info.VendorId = ReadU16(b, p); p += 2;
        int targets = b[p]; p += 1;

        for (int t = 0; t < targets && p + 263 <= b.Length; t++)
        {
            p += 255;                                     // bfTargetName
            p += 4;                                       // dwTargetSize
            int nbAlt = (int)ReadU32(b, p); p += 4;

            for (int a = 0; a < nbAlt && p < b.Length; a++)
            {
                byte altId = b[p]; p += 1;
                string altName = Encoding.ASCII.GetString(b, p, 255).TrimEnd('\0').Trim(); p += 255;
                uint size = ReadU32(b, p); p += 4;

                // ST 的 alt 名形如 "Internal Flash  /0x08000000/04*016Kg,..."，从中提取地址
                uint addr = 0x08000000u;
                var m = Regex.Match(altName, @"/0[xX]([0-9a-fA-F]{1,8})");
                if (m.Success)
                    uint.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, null, out addr);

                info.Images.Add(new DfuImage { AltSetting = altId, Name = altName, Size = size, Address = addr });
                p += (int)size;                           // 跳过固件数据
            }
        }

        // 后缀: bcdDevice(2) + idProduct(2) + idVendor(2) + bcdDFU(2) + "DFU"(3) + bLength(1) + CRC32(4)
        int suf = b.Length - 16;
        if (suf >= 0)
        {
            info.DeviceVersion = ReadU16(b, suf);
            info.ProductId = ReadU16(b, suf + 2);
            info.VendorId = ReadU16(b, suf + 4);
            info.BcdDfu = ReadU16(b, suf + 6);
            byte bLen = b[suf + 11];
            uint crcInFile = ReadU32(b, suf + 12);
            uint crcCalc = Crc32.Compute(b, 0, b.Length - 4);
            info.CrcValid = bLen == 16 && crcCalc == crcInFile;
        }
    }

    private static uint ReadU16(byte[] b, int p) => (uint)(b[p] | (b[p + 1] << 8));
    private static uint ReadU32(byte[] b, int p) => (uint)(b[p] | (b[p + 1] << 8) | (b[p + 2] << 16) | (b[p + 3] << 24));
}

/// <summary>固件文件信息。</summary>
public class DfuFileInfo
{
    public string FilePath { get; set; } = "";
    public bool IsDfuFile { get; set; }
    public uint? VendorId { get; set; }
    public uint? ProductId { get; set; }
    public uint? DeviceVersion { get; set; }
    public uint? BcdDfu { get; set; }
    public bool? CrcValid { get; set; }
    public List<DfuImage> Images { get; set; } = new();

    public string FormatName => IsDfuFile ? "DfuSe (.dfu)" : "Raw Binary (.bin)";
}

/// <summary>DFU 目标镜像（对应一个 alternate setting）。</summary>
public class DfuImage
{
    public byte AltSetting { get; set; }
    public string? Name { get; set; }
    public uint Address { get; set; }
    public uint Size { get; set; }
}
