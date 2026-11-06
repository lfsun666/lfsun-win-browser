using System.Drawing.Imaging;

namespace Lfsun.Win.Browser.UI;

/// <summary>
/// 中文注释：零依赖纯 C# 离线二维码生成器（遵循 ISO/IEC 18004 规范）。
/// 支持 Version 1~12，容错级别 M (约 15% 纠错)，可轻松容纳常规及超长网页 URL。
/// </summary>
public static class QrCodeGenerator
{
    private static readonly int[] ExpTable = new int[512];
    private static readonly int[] LogTable = new int[256];

    static QrCodeGenerator()
    {
        var x = 1;
        for (var i = 0; i < 255; i++)
        {
            ExpTable[i] = x;
            LogTable[x] = i;
            x <<= 1;
            if ((x & 256) != 0)
            {
                x ^= 0x11D;
            }
        }

        for (var i = 255; i < 512; i++)
        {
            ExpTable[i] = ExpTable[i - 255];
        }
    }

    private sealed record QrVersionInfo(
        int Version,
        int TotalCodewords,
        int DataCodewordsM,
        int EcCodewordsPerBlockM,
        int BlockGroup1Count,
        int BlockGroup1DataCount,
        int BlockGroup2Count,
        int BlockGroup2DataCount,
        int[] AlignmentCoords);

    // 标准 ISO 18004 Version 1~12 容错级别 M 参数表
    private static readonly QrVersionInfo[] VersionCatalog =
    [
        new(1,  26,  16,  10, 1, 16, 0, 0, []),
        new(2,  44,  28,  16, 1, 28, 0, 0, [6, 18]),
        new(3,  70,  44,  26, 1, 44, 0, 0, [6, 22]),
        new(4,  100, 64,  18, 2, 32, 0, 0, [6, 26]),
        new(5,  134, 86,  24, 2, 43, 0, 0, [6, 30]),
        new(6,  172, 108, 16, 4, 27, 0, 0, [6, 34]),
        new(7,  196, 124, 18, 4, 31, 0, 0, [6, 22, 38]),
        new(8,  242, 154, 22, 2, 38, 2, 39, [6, 24, 42]),
        new(9,  292, 182, 22, 3, 36, 2, 37, [6, 26, 46]),
        new(10, 346, 216, 26, 4, 43, 1, 44, [6, 28, 50]),
        new(11, 404, 254, 30, 1, 50, 4, 51, [6, 30, 54]),
        new(12, 466, 290, 22, 6, 36, 2, 37, [6, 32, 58])
    ];

    /// <summary>
    /// 中文注释：将指定文本（如 URL）编码并生成高清晰度二维码 Bitmap。
    /// </summary>
    public static Bitmap Generate(string text, int pixelSize = 6, int margin = 4, Color? fgColor = null, Color? bgColor = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            text = " ";
        }

        var matrix = EncodeMatrix(text);
        var matrixSize = matrix.GetLength(0);
        var totalSize = (matrixSize + margin * 2) * pixelSize;

        var foreground = fgColor ?? Color.FromArgb(20, 24, 35);
        var background = bgColor ?? Color.White;

        var bitmap = new Bitmap(totalSize, totalSize, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var bgBrush = new SolidBrush(background);
        using var fgBrush = new SolidBrush(foreground);

        graphics.FillRectangle(bgBrush, 0, 0, totalSize, totalSize);

        for (var row = 0; row < matrixSize; row++)
        {
            for (var col = 0; col < matrixSize; col++)
            {
                if (matrix[row, col])
                {
                    graphics.FillRectangle(
                        fgBrush,
                        (col + margin) * pixelSize,
                        (row + margin) * pixelSize,
                        pixelSize,
                        pixelSize);
                }
            }
        }

        return bitmap;
    }

    private static bool[,] EncodeMatrix(string text)
    {
        var rawBytes = System.Text.Encoding.UTF8.GetBytes(text);
        var versionInfo = SelectVersion(rawBytes.Length);
        var dataBits = BuildDataBits(rawBytes, versionInfo);
        var allCodewords = BuildAllCodewords(dataBits, versionInfo);

        var size = 17 + versionInfo.Version * 4;
        var matrix = new bool?[size, size];
        var isFunctionModule = new bool[size, size];

        // 1. 放置寻象图形 (Finder Patterns)
        PlaceFinderPattern(matrix, isFunctionModule, 0, 0);
        PlaceFinderPattern(matrix, isFunctionModule, size - 7, 0);
        PlaceFinderPattern(matrix, isFunctionModule, 0, size - 7);

        // 2. 放置分隔符 (Separators)
        PlaceSeparators(matrix, isFunctionModule, size);

        // 3. 放置对准图形 (Alignment Patterns)
        if (versionInfo.AlignmentCoords.Length > 0)
        {
            PlaceAlignmentPatterns(matrix, isFunctionModule, versionInfo.AlignmentCoords, size);
        }

        // 4. 放置时序图形 (Timing Patterns)
        for (var i = 8; i < size - 8; i++)
        {
            var black = i % 2 == 0;
            if (matrix[6, i] is null)
            {
                matrix[6, i] = black;
                isFunctionModule[6, i] = true;
            }
            if (matrix[i, 6] is null)
            {
                matrix[i, 6] = black;
                isFunctionModule[i, 6] = true;
            }
        }

        // 5. 放置暗模块 (Dark module)
        matrix[4 * versionInfo.Version + 9, 8] = true;
        isFunctionModule[4 * versionInfo.Version + 9, 8] = true;

        // 6. 保留格式信息区域 (Format Bits placeholder)
        ReserveFormatInfo(isFunctionModule, size);

        // 7. 写入数据码字并应用掩模 Pattern 0 ((row + col) % 2 == 0)
        var bitIndex = 0;
        var totalDataBits = allCodewords.Length * 8;
        var upwards = true;

        for (var right = size - 1; right > 0; right -= 2)
        {
            if (right == 6)
            {
                right--; // 跳过垂直时序图形列
            }

            for (var vertical = 0; vertical < size; vertical++)
            {
                var row = upwards ? (size - 1 - vertical) : vertical;
                for (var step = 0; step < 2; step++)
                {
                    var col = right - step;
                    if (isFunctionModule[row, col])
                    {
                        continue;
                    }

                    var bit = false;
                    if (bitIndex < totalDataBits)
                    {
                        var byteIdx = bitIndex / 8;
                        var bitOffset = 7 - (bitIndex % 8);
                        bit = ((allCodewords[byteIdx] >> bitOffset) & 1) == 1;
                        bitIndex++;
                    }

                    // Mask 0: (row + col) % 2 == 0
                    if ((row + col) % 2 == 0)
                    {
                        bit = !bit;
                    }

                    matrix[row, col] = bit;
                }
            }

            upwards = !upwards;
        }

        // 8. 写入格式信息 (Format Information): 容错级别 M (00) + Mask 0 (000) -> 格式串为 0x5412
        WriteFormatInformation(matrix, size);

        var result = new bool[size, size];
        for (var r = 0; r < size; r++)
        {
            for (var c = 0; c < size; c++)
            {
                result[r, c] = matrix[r, c] ?? false;
            }
        }

        return result;
    }

    private static QrVersionInfo SelectVersion(int dataLength)
    {
        foreach (var v in VersionCatalog)
        {
            // Byte 模式开销：4 bit 模式指示器 + (Version < 10 ? 8 : 16) bit 字符计数
            var charCountBits = v.Version < 10 ? 8 : 16;
            var headerBits = 4 + charCountBits;
            var totalBitsNeeded = headerBits + dataLength * 8;
            if (totalBitsNeeded <= v.DataCodewordsM * 8)
            {
                return v;
            }
        }

        return VersionCatalog[^1];
    }

    private static List<bool> BuildDataBits(byte[] data, QrVersionInfo versionInfo)
    {
        var bits = new List<bool>();

        // Mode: 0100 (Byte mode)
        bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);

        // Character count indicator
        var countBits = versionInfo.Version < 10 ? 8 : 16;
        for (var i = countBits - 1; i >= 0; i--)
        {
            bits.Add(((data.Length >> i) & 1) == 1);
        }

        // Data bytes
        foreach (var b in data)
        {
            for (var i = 7; i >= 0; i--)
            {
                bits.Add(((b >> i) & 1) == 1);
            }
        }

        // Terminator (0000)
        var maxDataBits = versionInfo.DataCodewordsM * 8;
        var terminatorLength = Math.Min(4, maxDataBits - bits.Count);
        for (var i = 0; i < terminatorLength; i++)
        {
            bits.Add(false);
        }

        // Pad to byte boundary
        while (bits.Count % 8 != 0)
        {
            bits.Add(false);
        }

        // Pad codewords: 0xEC (11101100), 0x11 (00010001)
        var padToggle = false;
        while (bits.Count < maxDataBits)
        {
            byte padByte = padToggle ? (byte)0x11 : (byte)0xEC;
            for (var i = 7; i >= 0; i--)
            {
                bits.Add(((padByte >> i) & 1) == 1);
            }
            padToggle = !padToggle;
        }

        return bits;
    }

    private static byte[] BuildAllCodewords(List<bool> dataBits, QrVersionInfo v)
    {
        var dataCodewords = new byte[v.DataCodewordsM];
        for (var i = 0; i < v.DataCodewordsM; i++)
        {
            byte val = 0;
            for (var b = 0; b < 8; b++)
            {
                if (dataBits[i * 8 + b])
                {
                    val |= (byte)(1 << (7 - b));
                }
            }
            dataCodewords[i] = val;
        }

        var totalBlocks = v.BlockGroup1Count + v.BlockGroup2Count;
        var dataBlocks = new byte[totalBlocks][];
        var ecBlocks = new byte[totalBlocks][];

        var offset = 0;
        var blockIdx = 0;
        for (var i = 0; i < v.BlockGroup1Count; i++)
        {
            dataBlocks[blockIdx] = new byte[v.BlockGroup1DataCount];
            Array.Copy(dataCodewords, offset, dataBlocks[blockIdx], 0, v.BlockGroup1DataCount);
            ecBlocks[blockIdx] = CalculateReedSolomon(dataBlocks[blockIdx], v.EcCodewordsPerBlockM);
            offset += v.BlockGroup1DataCount;
            blockIdx++;
        }

        for (var i = 0; i < v.BlockGroup2Count; i++)
        {
            dataBlocks[blockIdx] = new byte[v.BlockGroup2DataCount];
            Array.Copy(dataCodewords, offset, dataBlocks[blockIdx], 0, v.BlockGroup2DataCount);
            ecBlocks[blockIdx] = CalculateReedSolomon(dataBlocks[blockIdx], v.EcCodewordsPerBlockM);
            offset += v.BlockGroup2DataCount;
            blockIdx++;
        }

        // 交错数据码字 (Interleaving data codewords)
        var result = new List<byte>(v.TotalCodewords);
        var maxDataBlockLen = Math.Max(v.BlockGroup1DataCount, v.BlockGroup2DataCount);
        for (var i = 0; i < maxDataBlockLen; i++)
        {
            for (var b = 0; b < totalBlocks; b++)
            {
                if (i < dataBlocks[b].Length)
                {
                    result.Add(dataBlocks[b][i]);
                }
            }
        }

        // 交错纠错码字 (Interleaving EC codewords)
        for (var i = 0; i < v.EcCodewordsPerBlockM; i++)
        {
            for (var b = 0; b < totalBlocks; b++)
            {
                result.Add(ecBlocks[b][i]);
            }
        }

        return result.ToArray();
    }

    private static byte[] CalculateReedSolomon(byte[] data, int ecCount)
    {
        var generator = BuildGeneratorPolynomial(ecCount);
        var info = new int[data.Length + ecCount];
        for (var i = 0; i < data.Length; i++)
        {
            info[i] = data[i];
        }

        for (var i = 0; i < data.Length; i++)
        {
            var lead = info[i];
            if (lead == 0)
            {
                continue;
            }

            for (var j = 0; j < generator.Length; j++)
            {
                info[i + j] ^= GfMultiply(generator[j], lead);
            }
        }

        var ec = new byte[ecCount];
        for (var i = 0; i < ecCount; i++)
        {
            ec[i] = (byte)info[data.Length + i];
        }

        return ec;
    }

    private static int[] BuildGeneratorPolynomial(int degree)
    {
        var poly = new int[] { 1 };
        for (var i = 0; i < degree; i++)
        {
            var factor = new int[] { ExpTable[i], 1 };
            var next = new int[poly.Length + 1];
            for (var j = 0; j < poly.Length; j++)
            {
                next[j] ^= GfMultiply(poly[j], factor[0]);
                next[j + 1] ^= GfMultiply(poly[j], factor[1]);
            }
            poly = next;
        }
        Array.Reverse(poly);
        return poly;
    }

    private static int GfMultiply(int a, int b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        return ExpTable[LogTable[a] + LogTable[b]];
    }

    private static void PlaceFinderPattern(bool?[,] matrix, bool[,] isFunc, int startCol, int startRow)
    {
        for (var r = 0; r < 7; r++)
        {
            for (var c = 0; c < 7; c++)
            {
                var isBlack = r == 0 || r == 6 || c == 0 || c == 6 || (r >= 2 && r <= 4 && c >= 2 && c <= 4);
                matrix[startRow + r, startCol + c] = isBlack;
                isFunc[startRow + r, startCol + c] = true;
            }
        }
    }

    private static void PlaceSeparators(bool?[,] matrix, bool[,] isFunc, int size)
    {
        // Top-left separator
        for (var i = 0; i < 8; i++)
        {
            SetSeparatorModule(matrix, isFunc, 7, i);
            SetSeparatorModule(matrix, isFunc, i, 7);
        }

        // Top-right separator
        for (var i = 0; i < 8; i++)
        {
            SetSeparatorModule(matrix, isFunc, 7, size - 1 - i);
            SetSeparatorModule(matrix, isFunc, i, size - 8);
        }

        // Bottom-left separator
        for (var i = 0; i < 8; i++)
        {
            SetSeparatorModule(matrix, isFunc, size - 8, i);
            SetSeparatorModule(matrix, isFunc, size - 1 - i, 7);
        }
    }

    private static void SetSeparatorModule(bool?[,] matrix, bool[,] isFunc, int r, int c)
    {
        matrix[r, c] = false;
        isFunc[r, c] = true;
    }

    private static void PlaceAlignmentPatterns(bool?[,] matrix, bool[,] isFunc, int[] coords, int size)
    {
        foreach (var r in coords)
        {
            foreach (var c in coords)
            {
                // 不能覆盖左上、右上、左下寻象图形
                if ((r <= 8 && c <= 8) || (r <= 8 && c >= size - 8) || (r >= size - 8 && c <= 8))
                {
                    continue;
                }

                for (var dr = -2; dr <= 2; dr++)
                {
                    for (var dc = -2; dc <= 2; dc++)
                    {
                        var isBlack = Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0);
                        matrix[r + dr, c + dc] = isBlack;
                        isFunc[r + dr, c + dc] = true;
                    }
                }
            }
        }
    }

    private static void ReserveFormatInfo(bool[,] isFunc, int size)
    {
        for (var i = 0; i <= 8; i++)
        {
            isFunc[8, i] = true;
            isFunc[i, 8] = true;
        }

        for (var i = 0; i < 8; i++)
        {
            isFunc[8, size - 1 - i] = true;
            isFunc[size - 1 - i, 8] = true;
        }
    }

    private static void WriteFormatInformation(bool?[,] matrix, int size)
    {
        // 容错等级 M (00) + 掩模 0 (000) -> 格式信息码为 0x5412 (101010000010010)
        const int formatBits = 0x5412;
        var bits = new bool[15];
        for (var i = 0; i < 15; i++)
        {
            bits[i] = ((formatBits >> (14 - i)) & 1) == 1;
        }

        // Top-left & Top-right
        var formatCoords1 = new (int r, int c)[]
        {
            (8, 0), (8, 1), (8, 2), (8, 3), (8, 4), (8, 5), (8, 7), (8, 8),
            (7, 8), (5, 8), (4, 8), (3, 8), (2, 8), (1, 8), (0, 8)
        };

        for (var i = 0; i < 15; i++)
        {
            matrix[formatCoords1[i].r, formatCoords1[i].c] = bits[i];
        }

        // Bottom-left & Top-right duplicate
        for (var i = 0; i < 7; i++)
        {
            matrix[size - 1 - i, 8] = bits[i];
        }

        for (var i = 7; i < 15; i++)
        {
            matrix[8, size - 15 + i] = bits[i];
        }
    }
}
