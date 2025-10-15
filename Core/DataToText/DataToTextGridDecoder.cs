using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Core.DataToText;

/// <summary>
/// DataToText 65×65网格解码器
/// 负责从屏幕截图中定位、提取和解码游戏数据
/// </summary>
public sealed class DataToTextGridDecoder
{
    // 网格常量
    private const int GRID_SIZE = 65;
    private const int CORNER_MARKER_SIZE = 3;
    private const int TOTAL_CELLS = GRID_SIZE * GRID_SIZE;
    private const int MARKER_CELLS = 4 * CORNER_MARKER_SIZE * CORNER_MARKER_SIZE;
    private const int DATA_CELLS = TOTAL_CELLS - MARKER_CELLS;

    // 数据格式常量
    private const int METADATA_BYTES = 4;
    private const int FIELD_COUNT = 108;
    private const int BITS_PER_FIELD = 24;
    private const int CRC32_BYTES = 4;
    private const int DATA_BYTES = FIELD_COUNT * 3; // 108 fields × 3 bytes

    // CRC32多项式 (IEEE 802.3)
    private const uint CRC32_POLYNOMIAL = 0xEDB88320;
    private static readonly uint[] crc32Table = InitCRC32Table();

    // 缓存上一次找到的位置，加速后续定位
    private GridLocation? cachedLocation;
    private int cacheHitCount;
    private const int CACHE_VALIDATION_THRESHOLD = 10; // 每10帧验证一次缓存

    /// <summary>
    /// 解码后的字段数据 (108个24位整数)
    /// </summary>
    public int[] Fields { get; } = new int[FIELD_COUNT];

    /// <summary>
    /// 上一次解码的元数据
    /// </summary>
    public GridMetadata LastMetadata { get; private set; }

    /// <summary>
    /// 上一次解码是否成功
    /// </summary>
    public bool LastDecodeSuccess { get; private set; }

    /// <summary>
    /// 上一次解码错误消息
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// 从屏幕图像中定位并解码网格数据
    /// </summary>
    /// <param name="screenImage">完整屏幕截图</param>
    /// <returns>是否成功解码</returns>
    public bool DecodeFromScreen(Image<Bgra32> screenImage)
    {
        LastDecodeSuccess = false;
        LastError = null;

        try
        {
            // 1. 尝试使用缓存位置
            if (cachedLocation != null && cacheHitCount < CACHE_VALIDATION_THRESHOLD)
            {
                if (TryDecodeAtLocation(screenImage, cachedLocation))
                {
                    cacheHitCount++;
                    LastDecodeSuccess = true;
                    return true;
                }
                // 缓存失效，清除并重新搜索
                cachedLocation = null;
                cacheHitCount = 0;
            }

            // 2. 全屏搜索网格位置
            GridLocation? location = FindGridInScreen(screenImage);
            if (location == null)
            {
                LastError = "未能在屏幕中找到DataToText网格";
                return false;
            }

            // 3. 尝试解码
            if (TryDecodeAtLocation(screenImage, location))
            {
                cachedLocation = location;
                cacheHitCount = 0;
                LastDecodeSuccess = true;
                return true;
            }

            LastError = "网格定位成功但解码失败";
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"解码异常: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 在屏幕中搜索DataToText网格
    /// 通过识别四个3×3角标记来定位
    /// </summary>
    private static GridLocation? FindGridInScreen(Image<Bgra32> screenImage)
    {
        int width = screenImage.Width;
        int height = screenImage.Height;

        // 计算自适应的单元格大小范围
        // 最小：4px (适用于低分辨率/小窗口)
        // 最大：屏幕较短边 / GRID_SIZE (确保网格能容纳在屏幕内)
        int minCellSize = 4;
        int maxCellSize = Math.Min(width, height) / GRID_SIZE;

        // 防止maxCellSize过小或过大
        maxCellSize = Math.Max(maxCellSize, minCellSize);
        maxCellSize = Math.Min(maxCellSize, 100); // 上限100px (适用于4K+超高DPI)

        for (int estimatedCellSize = minCellSize; estimatedCellSize <= maxCellSize; estimatedCellSize++)
        {
            int gridPixelSize = GRID_SIZE * estimatedCellSize;

            // 如果网格大于屏幕，跳过这个单元格大小
            if (gridPixelSize > width || gridPixelSize > height)
                continue;

            // 在屏幕中滑动窗口搜索
            // 为了性能，采用步进搜索而非逐像素
            int step = Math.Max(1, estimatedCellSize / 2);

            for (int y = 0; y <= height - gridPixelSize; y += step)
            {
                for (int x = 0; x <= width - gridPixelSize; x += step)
                {
                    // 快速检查：验证左上角标记
                    if (IsCornerMarkerPresent(screenImage, x, y, estimatedCellSize))
                    {
                        // 验证其他三个角标记
                        if (VerifyAllCornerMarkers(screenImage, x, y, estimatedCellSize))
                        {
                            return new GridLocation
                            {
                                X = x,
                                Y = y,
                                CellSize = estimatedCellSize
                            };
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 检查指定位置是否存在3×3角标记
    /// 图案: █ █ █
    ///      █ ░ █
    ///      █ █ █
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCornerMarkerPresent(Image<Bgra32> image, int startX, int startY, int cellSize)
    {
        // 采样9个位置的中心点
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int x = startX + col * cellSize + cellSize / 2;
                int y = startY + row * cellSize + cellSize / 2;

                if (x >= image.Width || y >= image.Height)
                    return false;

                bool shouldBeBlack = !(row == 1 && col == 1); // 中心是白色
                bool isBlack = IsPixelBlack(image[x, y]);

                if (shouldBeBlack != isBlack)
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 验证所有四个角标记
    /// </summary>
    private static bool VerifyAllCornerMarkers(Image<Bgra32> image, int gridX, int gridY, int cellSize)
    {
        int gridPixelSize = GRID_SIZE * cellSize;
        int markerPixelSize = CORNER_MARKER_SIZE * cellSize;

        // 左上角 (已经验证过，但为了完整性再次检查)
        if (!IsCornerMarkerPresent(image, gridX, gridY, cellSize))
            return false;

        // 右上角
        if (!IsCornerMarkerPresent(image, gridX + gridPixelSize - markerPixelSize, gridY, cellSize))
            return false;

        // 左下角
        if (!IsCornerMarkerPresent(image, gridX, gridY + gridPixelSize - markerPixelSize, cellSize))
            return false;

        // 右下角
        if (!IsCornerMarkerPresent(image,
            gridX + gridPixelSize - markerPixelSize,
            gridY + gridPixelSize - markerPixelSize,
            cellSize))
            return false;

        return true;
    }

    /// <summary>
    /// 在指定位置尝试解码网格数据
    /// </summary>
    private bool TryDecodeAtLocation(Image<Bgra32> image, GridLocation location)
    {
        // 1. 采样网格数据 (65×65)
        byte[,] grid = SampleGrid(image, location);

        // 2. 提取位流 (跳过角标记)
        byte[] bits = ExtractBits(grid);

        // 3. 位转字节
        byte[] bytes = BitsToBytes(bits);

        // 4. 验证数据完整性
        if (bytes.Length < METADATA_BYTES + DATA_BYTES + CRC32_BYTES)
        {
            LastError = "数据长度不足";
            return false;
        }

        // 5. 解析元数据
        LastMetadata = new GridMetadata
        {
            Version = bytes[0],
            FieldCount = bytes[1],
            Reserved1 = bytes[2],
            Reserved2 = bytes[3]
        };

        // 6. 验证版本和字段数
        if (LastMetadata.FieldCount != FIELD_COUNT)
        {
            LastError = $"字段数不匹配: 期望{FIELD_COUNT}, 实际{LastMetadata.FieldCount}";
            return false;
        }

        // 7. 验证CRC32
        ReadOnlySpan<byte> dataForCrc = bytes.AsSpan(0, METADATA_BYTES + DATA_BYTES);
        uint calculatedCrc = CalculateCRC32(dataForCrc);
        uint storedCrc = BinaryPrimitives.ReadUInt32BigEndian(
            bytes.AsSpan(METADATA_BYTES + DATA_BYTES, CRC32_BYTES));

        if (calculatedCrc != storedCrc)
        {
            LastError = $"CRC32校验失败: 计算={calculatedCrc:X8}, 存储={storedCrc:X8}";
            return false;
        }

        // 8. 解析108个字段 (每个3字节 big-endian)
        int byteIndex = METADATA_BYTES;
        for (int i = 0; i < FIELD_COUNT; i++)
        {
            Fields[i] = (bytes[byteIndex] << 16) |
                       (bytes[byteIndex + 1] << 8) |
                       bytes[byteIndex + 2];
            byteIndex += 3;
        }

        return true;
    }

    /// <summary>
    /// 从图像中采样65×65网格
    /// </summary>
    private static byte[,] SampleGrid(Image<Bgra32> image, GridLocation location)
    {
        byte[,] grid = new byte[GRID_SIZE, GRID_SIZE];

        for (int row = 0; row < GRID_SIZE; row++)
        {
            for (int col = 0; col < GRID_SIZE; col++)
            {
                // 采样单元格中心点
                int x = location.X + col * location.CellSize + location.CellSize / 2;
                int y = location.Y + row * location.CellSize + location.CellSize / 2;

                // 边界检查
                if (x >= image.Width || y >= image.Height)
                {
                    grid[row, col] = 0; // 默认白色
                    continue;
                }

                grid[row, col] = IsPixelBlack(image[x, y]) ? (byte)1 : (byte)0;
            }
        }

        return grid;
    }

    /// <summary>
    /// 从65×65网格中提取位流 (跳过角标记)
    /// </summary>
    private static byte[] ExtractBits(byte[,] grid)
    {
        byte[] bits = new byte[DATA_CELLS];
        int bitIndex = 0;

        for (int row = 0; row < GRID_SIZE; row++)
        {
            for (int col = 0; col < GRID_SIZE; col++)
            {
                if (!IsInCorner(row, col))
                {
                    bits[bitIndex++] = grid[row, col];
                }
            }
        }

        return bits;
    }

    /// <summary>
    /// 检查单元格是否在角标记区域
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInCorner(int row, int col)
    {
        // 上半部分（前3行）
        if (row < CORNER_MARKER_SIZE)
        {
            return col < CORNER_MARKER_SIZE || col >= GRID_SIZE - CORNER_MARKER_SIZE;
        }
        // 下半部分（后3行）
        if (row >= GRID_SIZE - CORNER_MARKER_SIZE)
        {
            return col < CORNER_MARKER_SIZE || col >= GRID_SIZE - CORNER_MARKER_SIZE;
        }
        return false;
    }

    /// <summary>
    /// 位数组转字节数组
    /// </summary>
    private static byte[] BitsToBytes(byte[] bits)
    {
        int byteCount = (bits.Length + 7) / 8;
        byte[] bytes = new byte[byteCount];

        for (int i = 0; i < bits.Length; i++)
        {
            if (bits[i] == 1)
            {
                int byteIndex = i / 8;
                int bitIndex = 7 - (i % 8); // MSB first
                bytes[byteIndex] |= (byte)(1 << bitIndex);
            }
        }

        return bytes;
    }

    /// <summary>
    /// 判断像素是否为黑色
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPixelBlack(Bgra32 pixel)
    {
        // 简单阈值: 平均亮度 < 128
        int brightness = (pixel.R + pixel.G + pixel.B) / 3;
        return brightness < 128;
    }

    /// <summary>
    /// 计算CRC32校验码
    /// </summary>
    private static uint CalculateCRC32(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;

        for (int i = 0; i < data.Length; i++)
        {
            byte index = (byte)((crc ^ data[i]) & 0xFF);
            crc = (crc >> 8) ^ crc32Table[index];
        }

        return crc ^ 0xFFFFFFFF;
    }

    /// <summary>
    /// 初始化CRC32查找表
    /// </summary>
    private static uint[] InitCRC32Table()
    {
        uint[] table = new uint[256];

        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                    crc = (crc >> 1) ^ CRC32_POLYNOMIAL;
                else
                    crc >>= 1;
            }
            table[i] = crc;
        }

        return table;
    }

    /// <summary>
    /// 重置缓存 (当窗口移动或大小改变时调用)
    /// </summary>
    public void ResetCache()
    {
        cachedLocation = null;
        cacheHitCount = 0;
    }

    /// <summary>
    /// 获取当前屏幕的搜索范围信息 (用于诊断)
    /// </summary>
    public static (int minCellSize, int maxCellSize, int searchCount) GetSearchRangeInfo(Image<Bgra32> screenImage)
    {
        int width = screenImage.Width;
        int height = screenImage.Height;

        int minCellSize = 4;
        int maxCellSize = Math.Min(width, height) / GRID_SIZE;
        maxCellSize = Math.Max(maxCellSize, minCellSize);
        maxCellSize = Math.Min(maxCellSize, 100);

        int searchCount = maxCellSize - minCellSize + 1;

        return (minCellSize, maxCellSize, searchCount);
    }
}

/// <summary>
/// 网格位置信息
/// </summary>
public sealed class GridLocation
{
    public int X { get; init; }
    public int Y { get; init; }
    public int CellSize { get; init; }

    public override string ToString() => $"({X}, {Y}) CellSize={CellSize}px";
}

/// <summary>
/// 网格元数据
/// </summary>
public readonly struct GridMetadata
{
    public byte Version { get; init; }
    public byte FieldCount { get; init; }
    public byte Reserved1 { get; init; }
    public byte Reserved2 { get; init; }

    public override string ToString() =>
        $"Version={Version}, Fields={FieldCount}";
}
