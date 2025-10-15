using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
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
    private const int CORNER_MARKER_SIZE = 7;  // QR码 Finder Pattern 尺寸
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
    /// 使用QR码 Finder Pattern 检测算法
    /// </summary>
    private static GridLocation? FindGridInScreen(Image<Bgra32> screenImage)
    {
        // 1. 检测所有 Finder Pattern
        List<FinderPattern> patterns = FindFinderPatterns(screenImage);

        System.Diagnostics.Debug.WriteLine($"[FindGridInScreen] 找到 {patterns.Count} 个 Finder Patterns");
        foreach (var p in patterns)
        {
            System.Diagnostics.Debug.WriteLine($"  Pattern @ ({p.CenterX:F1}, {p.CenterY:F1}), ModuleSize={p.EstimatedModuleSize:F2}");
        }

        if (patterns.Count < 3)
        {
            System.Diagnostics.Debug.WriteLine($"[FindGridInScreen] Finder Pattern 数量不足 (需要>=3, 实际={patterns.Count})");
            return null; // 至少需要3个角
        }

        // 2. 从检测到的 Finder Patterns 中选择最可能的3个角
        //    (通常是距离最近且形成矩形的3个点)
        var bestTriangle = FindBestTriangle(patterns);
        if (bestTriangle == null)
        {
            System.Diagnostics.Debug.WriteLine($"[FindGridInScreen] FindBestTriangle 返回 null");
            return null;
        }

        // 3. 从3个 Finder Pattern 计算网格位置
        var location = CalculateGridLocation(bestTriangle.Value);
        System.Diagnostics.Debug.WriteLine($"[FindGridInScreen] 计算的网格位置: {location}");
        return location;
    }

    /// <summary>
    /// 检测图像中的所有 Finder Pattern
    /// 使用QR码行扫描 + 比例检测算法
    /// </summary>
    private static List<FinderPattern> FindFinderPatterns(Image<Bgra32> image)
    {
        List<FinderPattern> patterns = new();
        int width = image.Width;
        int height = image.Height;
        
        System.Diagnostics.Debug.WriteLine($"[FindFinderPatterns] 开始扫描图像 {width}×{height}");

        // 逐行扫描
        for (int y = 0; y < height; y++)
        {
            int[] stateCounts = new int[5]; // 黑-白-黑-白-黑 的run-length
            int currentState = 0;
            bool lastPixelBlack = false;

            for (int x = 0; x < width; x++)
            {
                bool isBlack = IsPixelBlack(image[x, y]);

                if (isBlack == lastPixelBlack)
                {
                    stateCounts[currentState]++;
                }
                else
                {
                    if (currentState == 4)
                    {
                        // 检查是否符合 1:1:3:1:1 比例
                        if (IsFinderPatternRatio(stateCounts))
                        {
                            // 垂直验证
                            if (VerifyVerticalPattern(image, x, y, stateCounts))
                            {
                                FinderPattern? pattern = CalculateFinderPatternCenter(image, x, y, stateCounts);
                                if (pattern != null)
                                    patterns.Add(pattern);
                            }
                        }

                        // 滑动窗口: 丢弃第一个状态,其他前移
                        stateCounts[0] = stateCounts[2];
                        stateCounts[1] = stateCounts[3];
                        stateCounts[2] = stateCounts[4];
                        stateCounts[3] = 1;
                        stateCounts[4] = 0;
                        currentState = 3;
                    }
                    else
                    {
                        currentState++;
                        stateCounts[currentState] = 1;
                    }
                    lastPixelBlack = isBlack;
                }
            }

            // 检查行末
            if (currentState == 4 && IsFinderPatternRatio(stateCounts))
            {
                if (VerifyVerticalPattern(image, width - 1, y, stateCounts))
                {
                    FinderPattern? pattern = CalculateFinderPatternCenter(image, width - 1, y, stateCounts);
                    if (pattern != null)
                        patterns.Add(pattern);
                }
            }
        }

        // 合并相近的 Finder Patterns (去重)
        return MergeNearbyPatterns(patterns);
    }

    /// <summary>
    /// 检查run-length是否符合 Finder Pattern 的 1:1:3:1:1 比例
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFinderPatternRatio(int[] stateCounts)
    {
        int totalModuleSize = stateCounts[0] + stateCounts[1] + stateCounts[2] + stateCounts[3] + stateCounts[4];
        if (totalModuleSize < 7) return false; // 太小

        float moduleSize = totalModuleSize / 7.0f;
        float maxVariance = moduleSize / 2.0f; // 50% 容差

        return Math.Abs(stateCounts[0] - moduleSize) < maxVariance &&
               Math.Abs(stateCounts[1] - moduleSize) < maxVariance &&
               Math.Abs(stateCounts[2] - moduleSize * 3) < maxVariance &&
               Math.Abs(stateCounts[3] - moduleSize) < maxVariance &&
               Math.Abs(stateCounts[4] - moduleSize) < maxVariance;
    }

    /// <summary>
    /// 垂直方向验证 Finder Pattern
    /// </summary>
    private static bool VerifyVerticalPattern(Image<Bgra32> image, int centerX, int centerY, int[] horizontalCounts)
    {
        int maxCount = horizontalCounts[0] + horizontalCounts[1] + horizontalCounts[2] + horizontalCounts[3] + horizontalCounts[4];
        int[] verticalCounts = new int[5];
        int currentState = 2; // 从中心黑块开始

        // 向上扫描
        for (int y = centerY; y >= 0 && currentState >= 0; y--)
        {
            if (IsPixelBlack(image[centerX, y]))
            {
                if (currentState % 2 == 0) // 应该是黑色
                    verticalCounts[currentState]++;
                else
                    break;
            }
            else
            {
                if (currentState % 2 == 1) // 应该是白色
                    verticalCounts[currentState]++;
                else
                {
                    currentState--;
                    if (currentState >= 0)
                        verticalCounts[currentState]++;
                }
            }

            if (verticalCounts[currentState] > maxCount)
                return false;
        }

        // 向下扫描
        currentState = 2;
        for (int y = centerY + 1; y < image.Height && currentState <= 4; y++)
        {
            if (IsPixelBlack(image[centerX, y]))
            {
                if (currentState % 2 == 0) // 应该是黑色
                    verticalCounts[currentState]++;
                else
                    break;
            }
            else
            {
                if (currentState % 2 == 1) // 应该是白色
                    verticalCounts[currentState]++;
                else
                {
                    currentState++;
                    if (currentState <= 4)
                        verticalCounts[currentState]++;
                }
            }

            if (verticalCounts[currentState] > maxCount)
                return false;
        }

        return IsFinderPatternRatio(verticalCounts);
    }

    /// <summary>
    /// 计算 Finder Pattern 的中心点
    /// </summary>
    private static FinderPattern? CalculateFinderPatternCenter(Image<Bgra32> image, int endX, int endY, int[] stateCounts)
    {
        float centerX = endX - stateCounts[4] - stateCounts[3] - stateCounts[2] / 2.0f;
        float centerY = endY;
        float moduleSize = (stateCounts[0] + stateCounts[1] + stateCounts[2] + stateCounts[3] + stateCounts[4]) / 7.0f;

        return new FinderPattern(centerX, centerY, moduleSize);
    }

    /// <summary>
    /// 合并相近的 Finder Patterns (去重)
    /// </summary>
    private static List<FinderPattern> MergeNearbyPatterns(List<FinderPattern> patterns)
    {
        if (patterns.Count <= 1)
            return patterns;

        List<FinderPattern> merged = new();
        patterns = patterns.OrderBy(p => p.CenterY).ThenBy(p => p.CenterX).ToList();

        foreach (var pattern in patterns)
        {
            bool foundSimilar = false;
            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].DistanceTo(pattern) < pattern.EstimatedModuleSize * 2)
                {
                    // 合并: 取平均值
                    float newX = (merged[i].CenterX + pattern.CenterX) / 2;
                    float newY = (merged[i].CenterY + pattern.CenterY) / 2;
                    float newSize = (merged[i].EstimatedModuleSize + pattern.EstimatedModuleSize) / 2;
                    merged[i] = new FinderPattern(newX, newY, newSize);
                    foundSimilar = true;
                    break;
                }
            }

            if (!foundSimilar)
                merged.Add(pattern);
        }

        return merged;
    }

    /// <summary>
    /// 从 Finder Patterns 中找到最佳的3个角(形成矩形)
    /// </summary>
    private static (FinderPattern topLeft, FinderPattern topRight, FinderPattern bottomLeft)? FindBestTriangle(List<FinderPattern> patterns)
    {
        if (patterns.Count < 3)
            return null;

        // 简化版: 假设网格在屏幕左上角,选择最上最左的3个点
        // 更复杂的实现会计算所有组合并选择最接近直角的
        var sorted = patterns.OrderBy(p => p.CenterY).ThenBy(p => p.CenterX).ToList();

        if (sorted.Count >= 4)
        {
            // 有4个角,尝试找出形成矩形的3个
            // 这里简化处理:取前3个
            return (sorted[0], sorted[1], sorted[2]);
        }

        return (sorted[0], sorted[1], sorted[2]);
    }

    /// <summary>
    /// 从3个 Finder Pattern 计算网格位置
    /// </summary>
    private static GridLocation? CalculateGridLocation((FinderPattern topLeft, FinderPattern topRight, FinderPattern bottomLeft) triangle)
    {
        // 计算 cellSize (从 Finder Pattern 的模块大小)
        float avgModuleSize = (triangle.topLeft.EstimatedModuleSize +
                              triangle.topRight.EstimatedModuleSize +
                              triangle.bottomLeft.EstimatedModuleSize) / 3.0f;

        int cellSize = (int)Math.Round(avgModuleSize);

        // 网格左上角 = topLeft Finder Pattern 的左上角
        // Finder Pattern 中心在 (3.5, 3.5) 个模块位置
        int gridX = (int)Math.Round(triangle.topLeft.CenterX - 3.5f * cellSize);
        int gridY = (int)Math.Round(triangle.topLeft.CenterY - 3.5f * cellSize);

        return new GridLocation
        {
            X = gridX,
            Y = gridY,
            CellSize = cellSize
        };
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
                    grid[row, col] = 0; // 默认白色(Lua值0)
                    continue;
                }

                // Lua渲染: grid=1→黑色, grid=0→白色
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
    /// 简单阈值判断,考虑JPEG压缩误差
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsPixelBlack(Bgra32 pixel)
    {
        // 纯黑色(0,0,0)经过JPEG压缩可能变成(0~20, 0~20, 0~20)
        // 使用简单阈值: 所有通道都<40 = 黑色
        return pixel.R < 40 && pixel.G < 40 && pixel.B < 40;
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

/// <summary>
/// QR码 Finder Pattern 信息
/// </summary>
internal sealed class FinderPattern
{
    public float CenterX { get; init; }
    public float CenterY { get; init; }
    public float EstimatedModuleSize { get; init; }

    public FinderPattern(float centerX, float centerY, float moduleSize)
    {
        CenterX = centerX;
        CenterY = centerY;
        EstimatedModuleSize = moduleSize;
    }

    /// <summary>
    /// 计算与另一个 Finder Pattern 的距离
    /// </summary>
    public float DistanceTo(FinderPattern other)
    {
        float dx = CenterX - other.CenterX;
        float dy = CenterY - other.CenterY;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
