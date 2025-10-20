using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Core.DataToText;

/// <summary>
/// DataToText 65×65网格解码器
/// 负责从屏幕截图中定位、提取和解码游戏数据
/// </summary>
public sealed class DataToTextGridDecoder
{
    private readonly ILogger<DataToTextGridDecoder>? logger;
    
    // 网格常量
    private const int CORNER_MARKER_SIZE = 7;  // QR码 Finder Pattern 尺寸
    private const int QUIET_ZONE_SIZE = 1;     // Finder Pattern 周围的静区宽度
    private const int CORNER_TOTAL_SIZE = CORNER_MARKER_SIZE + QUIET_ZONE_SIZE;  // 8 (7 + 1)
    
    // Timing Pattern 常量 (C# 0-indexed, Lua中是3和4)
    private const int TIMING_PATTERN_ROW = 2;  // 第3行，Finder内部，避免穿过中心
    private const int TIMING_PATTERN_COL = 3;  // 第4列，Finder中心列，可复用已扫描的runs

    // 数据格式常量
    private const int METADATA_BYTES = 4;
    private const int FIELD_COUNT = 108;
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
    /// 构造函数
    /// </summary>
    /// <param name="logger">可选的日志记录器</param>
    public DataToTextGridDecoder(ILogger<DataToTextGridDecoder>? logger = null)
    {
        this.logger = logger;
    }

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
    /// 上一次解码的网格位置信息（用于调试）
    /// </summary>
    public GridLocation? LastGridLocation { get; private set; }

    /// <summary>
    /// 上一次采样的网格数据（用于调试）
    /// </summary>
    public byte[,]? LastSampledGrid { get; private set; }

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
            GridLocation? location = FindGridInScreen(screenImage, logger);
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
    /// 在屏幕中搜索DataToText网格 - 使用 Timing Pattern 实现精确定位
    /// Timing Pattern 在第3行（Finder内部），可以一次扫描同时找到两个 Finder 和 Timing Pattern
    /// 假设：屏幕中只有一个网格，完整显示
    /// </summary>
    private static GridLocation? FindGridInScreen(Image<Bgra32> screenImage, ILogger<DataToTextGridDecoder>? logger)
    {
        logger?.LogDebug("[FindGridInScreen] 开始搜索 {Width}×{Height} 屏幕", screenImage.Width, screenImage.Height);

        // 步骤 1: 扫描 Timing Pattern 行，同时找到两个 Finder 和 Timing Pattern
        var timingResult = FindFindersAndTimingPattern(screenImage, logger);
        if (timingResult == null)
        {
            logger?.LogDebug("[FindGridInScreen] 未找到 Finder 和 Timing Pattern");
            return null;
        }

        var (leftTopPattern, rightTopPattern, gridSizeH, xBoundaries, timingRow) = timingResult.Value;
        logger?.LogDebug("[FindGridInScreen] 找到水平: 左上Finder @ ({X1:F1}, {Y1:F1}), 右上Finder @ ({X2:F1}, {Y2:F1}), GridSize={Size}", 
            leftTopPattern.CenterX, leftTopPattern.CenterY,
            rightTopPattern.CenterX, rightTopPattern.CenterY,
            gridSizeH);

        // 步骤 2: 提取垂直方向的 runs (左上 Finder 中心列)
        int centerCol = (int)Math.Round(leftTopPattern.CenterX);
        var (verticalRuns, vStartsWithBlack) = ExtractRunLengthsVertical(screenImage, centerCol, 0, screenImage.Height);

        // 步骤 3: 在垂直 runs 中查找左下 Finder Pattern
        var bottomResult = FindThirdFinderInVerticalRuns(verticalRuns, vStartsWithBlack, leftTopPattern, screenImage, centerCol);
        if (bottomResult == null)
        {
            logger?.LogDebug("[FindGridInScreen] 未在同列找到左下 Finder");
            return null;
        }

        var (bottomLeftPattern, topEndRunIndex, bottomStartRunIndex) = bottomResult.Value;
        logger?.LogDebug("[FindGridInScreen] 找到垂直: 左下Finder @ ({X:F1}, {Y:F1})", 
            bottomLeftPattern.CenterX, bottomLeftPattern.CenterY);

        // 步骤 4: 从垂直 Timing Pattern 检测网格大小和边界
        var (gridSizeV, yBoundaries) = DetectGridSizeAndBoundariesFromTimingPattern(
            verticalRuns, vStartsWithBlack,
            leftTopPattern, bottomLeftPattern,
            topEndRunIndex, bottomStartRunIndex,
            false, logger);
        
        // 步骤 5: 验证和确定最终 gridSize
        float horizontalDistance = rightTopPattern.CenterX - leftTopPattern.CenterX;
        float verticalDistance = bottomLeftPattern.CenterY - leftTopPattern.CenterY;
        
        int gridSize;
        if (gridSizeH > 0 && gridSizeV > 0)
        {
            if (Math.Abs(gridSizeH - gridSizeV) > 2)
            {
                logger?.LogDebug("[FindGridInScreen] Timing Pattern 检测的网格大小不一致: H={H}, V={V}", gridSizeH, gridSizeV);
                return null;
            }
            gridSize = (gridSizeH + gridSizeV) / 2;
            logger?.LogDebug("[FindGridInScreen] 通过 Timing Pattern 确定 GridSize={Size}×{Size}", gridSize, gridSize);
        }
        else if (gridSizeV > 0)
        {
            gridSize = gridSizeV;
            logger?.LogDebug("[FindGridInScreen] 仅垂直 Timing Pattern 可用，GridSize={Size}×{Size}", gridSize, gridSize);
        }
        else
        {
            // 回退：使用 Finder Pattern 距离估算
            float centerDistanceH = horizontalDistance / leftTopPattern.EstimatedModuleSizeX;
            float centerDistanceV = verticalDistance / leftTopPattern.EstimatedModuleSizeY;
            const int FINDER_CENTER_CELL = 4;
            gridSize = (int)Math.Round((centerDistanceH + centerDistanceV) / 2 + 2 * FINDER_CENTER_CELL);
            logger?.LogWarning("[FindGridInScreen] Timing Pattern 检测失败，回退到估算 GridSize={Size}×{Size}", gridSize, gridSize);
        }

        // 步骤 6: 计算网格原点
        float calibratedCellSizeX = horizontalDistance / (gridSize - 8);
        float calibratedCellSizeY = verticalDistance / (gridSize - 8);
        
        int gridX = (int)Math.Round(leftTopPattern.CenterX - 3.5f * calibratedCellSizeX);
        int gridY = (int)Math.Round(leftTopPattern.CenterY - 3.5f * calibratedCellSizeY);

        logger?.LogInformation("[FindGridInScreen] 成功定位: Grid={Size}×{Size}, Origin=({X},{Y}), Timing={Timing}", 
            gridSize, gridSize, gridX, gridY, xBoundaries != null && yBoundaries != null ? "YES" : "PARTIAL");

        return new GridLocation
        {
            X = gridX,
            Y = gridY,
            CellSize = (int)Math.Round(leftTopPattern.EstimatedModuleSize),
            CalibratedCellSizeX = calibratedCellSizeX,
            CalibratedCellSizeY = calibratedCellSizeY,
            GridSize = gridSize,
            XBoundaries = xBoundaries,
            YBoundaries = yBoundaries
        };
    }

    /// <summary>
    /// 在 Timing Pattern 行同时找到两个 Finder 和 Timing Pattern
    /// Timing Pattern 在第3行（Finder内部），一次扫描即可
    /// </summary>
    /// <returns>(leftTopFinder, rightTopFinder, gridSize, xBoundaries, timingRow) 或 null</returns>
    private static (FinderPattern leftTop, FinderPattern rightTop, int gridSize, float[] xBoundaries, int timingRow)? 
        FindFindersAndTimingPattern(Image<Bgra32> image, ILogger<DataToTextGridDecoder>? logger)
    {
        int width = image.Width;
        int height = image.Height;
        int stepSize = 3;

        for (int y = 0; y < height; y += stepSize)
        {
            var (rawRuns, startsWithBlack) = ExtractRunLengths(image, y, width);
            
            // 在这一行中查找两个 Finder Pattern
            FinderPattern? firstFinder = null;
            FinderPattern? secondFinder = null;
            int firstEndRunIndex = -1;
            int secondStartRunIndex = -1;
            
            for (int i = 0; i <= rawRuns.Count - 9; i++)
            {
                bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
                if (!currentIsBlack) continue;

                int[] window = rawRuns.Skip(i).Take(9).ToArray();
                int[]? merged = TryMergeGapsInWindow(window);

                if (merged != null && IsFinderPatternRatio(merged))
                {
                    int endX = CalculateRunEndPosition(rawRuns, i + 8);
                    float centerX = endX - merged[4] - merged[3] - merged[2] / 2.0f;

                    int estimatedHeight = merged.Sum();
                    var (success, preciseCenterY, verticalMerged) = VerifyVerticalPattern(image, (int)centerX, y, estimatedHeight);
                    if (success)
                    {
                        var pattern = CreateFinderPattern(centerX, preciseCenterY, merged, verticalMerged);
                        
                        if (firstFinder == null)
                        {
                            firstFinder = pattern;
                            firstEndRunIndex = i + 8;
                        }
                        else if (secondFinder == null)
                        {
                            secondFinder = pattern;
                            secondStartRunIndex = i;
                            
                            // 找到两个 Finder，检测 Timing Pattern
                            var (gridSize, xBoundaries) = DetectGridSizeAndBoundariesFromTimingPattern(
                                rawRuns, startsWithBlack,
                                firstFinder, secondFinder,
                                firstEndRunIndex, secondStartRunIndex,
                                true, logger);
                            
                            if (gridSize > 0 && xBoundaries != null)
                            {
                                logger?.LogDebug("[FindFindersAndTimingPattern] 在行 {Row} 找到两个 Finder 和 Timing Pattern", y);
                                return (firstFinder, secondFinder, gridSize, xBoundaries, y);
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 创建 FinderPattern 对象（复用逻辑）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FinderPattern CreateFinderPattern(float centerX, float centerY, int[] horizontalMerged, int[]? verticalMerged)
    {
        float moduleSizeX = horizontalMerged.Sum() / 7.0f;
        float moduleSizeY = verticalMerged?.Sum() / 7.0f ?? moduleSizeX;
        return new FinderPattern(centerX, centerY, moduleSizeX, moduleSizeY);
    }



    /// <summary>
    /// 在垂直 runs 中查找第三个 Finder Pattern
    /// </summary>
    private static (FinderPattern pattern, int topEndRunIndex, int bottomStartRunIndex)? FindThirdFinderInVerticalRuns(
        List<int> runs, bool startsWithBlack, FinderPattern firstPattern, 
        Image<Bgra32> image, int col)
    {
        int currentY = 0;

        for (int i = 0; i <= runs.Count - 9; i++)
        {
            bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
            if (!currentIsBlack)
            {
                currentY += runs[i];
                continue;
            }

            int[] window = runs.Skip(i).Take(9).ToArray();
            int[]? merged = TryMergeGapsInWindow(window);

            if (merged != null && IsFinderPatternRatio(merged))
            {
                int endY = currentY;
                for (int j = 0; j < 9; j++)
                    endY += runs[i + j];

                float centerY = endY - merged[4] - merged[3] - merged[2] / 2.0f;

                // 验证水平方向
                int estimatedWidth = merged.Sum();
                var (success, preciseCenterX, horizontalMerged) = VerifyHorizontalPattern(image, col, (int)centerY, estimatedWidth);
                if (success && centerY > firstPattern.CenterY)
                {
                    // 注意：merged 是垂直方向的，horizontalMerged 是水平方向的
                    // CreateFinderPattern 参数顺序: (x, y, horizontalMerged, verticalMerged)
                    var pattern = horizontalMerged != null 
                        ? CreateFinderPattern(preciseCenterX, centerY, horizontalMerged, merged)
                        : CreateFinderPattern(preciseCenterX, centerY, merged, null);
                    
                    // 计算第一个 Finder 在 runs 中的结束索引(估算)
                    int topEndRunIndex = (int)(firstPattern.CenterY / (pattern.EstimatedModuleSizeY * 2));
                    return (pattern, topEndRunIndex, i);
                }
            }

            currentY += runs[i];
        }

        return null;
    }


    /// <summary>
    /// 从 rawRuns 检测网格大小和单元格边界（去除 Finder 内部的缝隙）
    /// 核心思路：rawRuns 本身就是黑白交替的 cell 序列，去掉两个 Finder 内部的缝隙后，
    /// 每个 run 就对应一个 cell，累加 run 长度就得到所有 cell 的边界
    /// </summary>
    /// <param name="runs">run-length 数组（包含缝隙）</param>
    /// <param name="startsWithBlack">runs 是否从黑色开始</param>
    /// <param name="firstFinder">第一个 Finder Pattern</param>
    /// <param name="secondFinder">第二个 Finder Pattern</param>
    /// <param name="firstEndRunIndex">第一个 Finder 结束的 run 索引（9个runs窗口的最后一个）</param>
    /// <param name="secondStartRunIndex">第二个 Finder 开始的 run 索引（9个runs窗口的第一个）</param>
    /// <param name="isHorizontal">是否是水平方向</param>
    /// <param name="logger">日志记录器</param>
    /// <returns>(gridSize, boundaries) 或 (0, null) 表示检测失败</returns>
    private static (int gridSize, float[]? boundaries) DetectGridSizeAndBoundariesFromTimingPattern(
        List<int> runs,
        bool startsWithBlack,
        FinderPattern firstFinder,
        FinderPattern secondFinder,
        int firstEndRunIndex,
        int secondStartRunIndex,
        bool isHorizontal,
        ILogger<DataToTextGridDecoder>? logger)
    {
        // 1. 合并两个 Finder 内部的缝隙，得到干净的 cell runs
        // 思路：遍历 runs，在 Finder 区域内识别缝隙并合并
        List<int> cellRuns = new(runs.Count);
        
        int i = 0;
        while (i < runs.Count)
        {
            // 判断当前 run 是否在 Finder 内部
            bool inFirstFinder = (i <= firstEndRunIndex);
            bool inSecondFinder = (i >= secondStartRunIndex && i <= Math.Min(secondStartRunIndex + 8, runs.Count - 1));
            
            if (inFirstFinder || inSecondFinder)
            {
                // 在 Finder 内部：检查是否是缝隙
                bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
                
                // 缝隙位置：9个runs窗口中的索引3和5（白色run）
                int finderStartIdx = inFirstFinder ? 0 : secondStartRunIndex;
                int relativeIdx = i - finderStartIdx;
                bool isGap = !currentIsBlack && (relativeIdx == 3 || relativeIdx == 5);
                
                if (isGap && i + 1 < runs.Count)
                {
                    // 是缝隙：合并 [前黑 + 缝隙 + 后黑]
                    // 前黑已经在 cellRuns 中，这里合并缝隙和后黑
                    if (cellRuns.Count > 0)
                    {
                        cellRuns[cellRuns.Count - 1] += runs[i] + runs[i + 1];
                        i += 2; // 跳过缝隙和后黑
                        continue;
                    }
                }
            }
            
            // 不是缝隙：正常添加
            cellRuns.Add(runs[i]);
            i++;
        }
        
        // 2. gridSize = cellRuns 的数量
        int gridSize = cellRuns.Count;
        
        logger?.LogDebug("[DetectBoundaries] {Direction}: 原始{OrigCount}个runs -> 合并缝隙后{CellCount}个cells -> GridSize={GridSize}", 
            isHorizontal ? "水平" : "垂直", runs.Count, cellRuns.Count, gridSize);
        
        // 3. 构建边界数组：累加每个 run 的长度
        float[] boundaries = new float[gridSize + 1];
        float currentPos = 0;
        
        boundaries[0] = 0;
        for (int j = 0; j < cellRuns.Count; j++)
        {
            currentPos += cellRuns[j];
            boundaries[j + 1] = currentPos;
        }
        
        logger?.LogDebug("[DetectBoundaries] {Direction} 边界检测完成: GridSize={GridSize}, {BoundaryCount} 个边界", 
            isHorizontal ? "水平" : "垂直", gridSize, boundaries.Length);
        
        return (gridSize, boundaries);
    }


    /// <summary>
    /// 提取一行的 run-length 序列
    /// </summary>
    /// <param name="startX">起始 X 坐标（默认0）</param>
    /// <returns>(runs, startsWithBlack)</returns>
    private static (List<int> runs, bool startsWithBlack) ExtractRunLengths(Image<Bgra32> image, int y, int width, int startX = 0)
    {
        List<int> runs = new();
        
        // 边界检查
        if (startX < 0 || startX >= width || y < 0 || y >= image.Height)
            return (runs, false);
        
        bool startsWithBlack = IsPixelBlack(image[startX, y]);
        bool currentIsBlack = startsWithBlack;
        int runLength = 0;

        for (int x = startX; x < width; x++)
        {
            bool isBlack = IsPixelBlack(image[x, y]);
            
            if (isBlack == currentIsBlack)
            {
                runLength++;
            }
            else
            {
                runs.Add(runLength);
                runLength = 1;
                currentIsBlack = isBlack;
            }
        }
        
        runs.Add(runLength); // 添加最后一个 run
        return (runs, startsWithBlack);
    }

    /// <summary>
    /// 尝试合并窗口内的间隙，返回 5 个 run 的数组
    /// 窗口必须从黑色 run 开始（黑-白-黑-白-黑...）
    /// </summary>
    /// <param name="window">候选窗口（5/7/9 个 run）</param>
    /// <returns>合并后的 5 个 run 数组，失败返回 null</returns>
    private static int[]? TryMergeGapsInWindow(int[] window)
    {
        int len = window.Length;
        
        // 只处理 5/7/9 个 run
        if (len != 5 && len != 7 && len != 9)
            return null;
        
        // 如果已经是 5 个，直接返回
        if (len == 5)
            return window;
        
        // 配置：需要合并的白色 run 位置（索引）
        // 7个run: 黑₀-白₁-黑₂-[白₃]-黑₄-白₅-黑₆ → 合并索引3
        // 9个run: 黑₀-白₁-黑₂-[白₃]-黑₄-[白₅]-黑₆-白₇-黑₈ → 合并索引3,5
        int[] gapPositions = len switch
        {
            7 => new[] { 3 },
            9 => new[] { 3, 5 },
            _ => Array.Empty<int>()
        };
        
        const int TARGET_RUN_COUNT = 5;
        const float GAP_THRESHOLD_RATIO = 0.2f; // 间隙 < 前黑色块的 1/5
        
        int[] merged = new int[TARGET_RUN_COUNT];
        int mergedIdx = 0;
        
        for (int i = 0; i < len; )
        {
            if (i % 2 == 0) // 黑色 run
            {
                merged[mergedIdx++] = window[i];
                i++;
            }
            else // 白色 run
            {
                if (Array.IndexOf(gapPositions, i) >= 0)
                {
                    // 应该是间隙：验证并合并
                    if (i + 1 >= len)
                        return null; // 缺少后续黑色 run
                    
                    int prevBlack = window[i - 1];
                    if (window[i] * 0.5f >= prevBlack * GAP_THRESHOLD_RATIO) // 间隙的一半参于比较
                        return null; // 不满足间隙条件
                    
                    // 合并: 前黑 + 间隙 + 后黑
                    merged[mergedIdx - 1] += window[i] + window[i + 1];
                    i += 2; // 跳过间隙和后黑
                }
                else
                {
                    // 正常的白色框
                    merged[mergedIdx++] = window[i];
                    i++;
                }
            }
        }
        
        return mergedIdx == TARGET_RUN_COUNT ? merged : null;
    }


    /// <summary>
    /// 计算 run 序列中某个索引对应的图像 x 位置
    /// </summary>
    private static int CalculateRunEndPosition(List<int> runs, int runIndex)
    {
        int position = 0;
        for (int i = 0; i <= runIndex && i < runs.Count; i++)
        {
            position += runs[i];
        }
        return position;
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
    /// 垂直方向验证 Finder Pattern（使用 run-length + 窗口合并）
    /// </summary>
    /// <param name="estimatedHeight">估算的 Finder Pattern 高度（用于确定搜索范围）</param>
    /// <returns>(success, preciseCenterY, verticalMerged) - 成功标志、精确的 Y 中心坐标和垂直方向合并后的runs</returns>
    private static (bool success, float preciseCenterY, int[]? verticalMerged) VerifyVerticalPattern(
        Image<Bgra32> image, int centerX, int centerY, int estimatedHeight)
    {
        // 从 centerY 向上下扫描，提取垂直 run-length
        int searchRadius = estimatedHeight * 2; // 留出足够余量

        int startY = Math.Max(0, centerY - searchRadius);
        int endY = Math.Min(image.Height - 1, centerY + searchRadius);

        // 提取垂直 run-length
        var (rawRuns, startsWithBlack) = ExtractRunLengthsVertical(image, centerX, startY, endY + 1);
        if (rawRuns.Count < 5) return (false, 0, null);

        // 遍历所有可能的窗口，寻找包含 centerY 的 Finder Pattern
        int currentY = startY;
        for (int i = 0; i <= rawRuns.Count - 5; i++)
        {
            // 窗口必须从黑色 run 开始
            bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
            if (!currentIsBlack)
            {
                currentY += rawRuns[i];
                continue;
            }

            // 尝试不同窗口大小
            // for (int windowSize = 5; windowSize <= Math.Min(9, rawRuns.Count - i); windowSize += 2)
            var windowSize = 9;
            if(rawRuns.Count - i < windowSize)
                return (false, 0, null);   // 不够9个了, 目前的实现中一定会有缝隙, 所以我们只考虑9个 Run List 的情况
            {
                // 计算窗口覆盖的范围
                int windowEnd = currentY;
                for (int j = 0; j < windowSize && i + j < rawRuns.Count; j++)
                    windowEnd += rawRuns[i + j];

                // 检查 centerY 是否在此窗口范围内
                if (centerY >= currentY && centerY < windowEnd)
                {
                    int[] window = rawRuns.Skip(i).Take(windowSize).ToArray();
                    int[]? merged = TryMergeGapsInWindow(window);

                    if (merged != null && IsFinderPatternRatio(merged))
                    {
                        // 计算精确的 centerY
                        float preciseCenterY = windowEnd - merged[4] - merged[3] - merged[2] / 2.0f;
                        return (true, preciseCenterY, merged);
                    }
                }
            }

            currentY += rawRuns[i];
        }

        return (false, 0, null);
    }

    /// <summary>
    /// 水平方向验证 Finder Pattern（使用 run-length + 窗口合并）
    /// </summary>
    /// <param name="estimatedWidth">估算的 Finder Pattern 宽度（用于优化，当前未使用）</param>
    /// <returns>(success, preciseCenterX, horizontalMerged) - 成功标志、精确的 X 中心坐标和水平方向合并后的runs</returns>
    private static (bool success, float preciseCenterX, int[]? horizontalMerged) VerifyHorizontalPattern(
        Image<Bgra32> image, int centerX, int centerY, int estimatedWidth)
    {
        // 提取整行的水平 run-length
        var (rawRuns, startsWithBlack) = ExtractRunLengths(image, centerY, image.Width);
        if (rawRuns.Count < 5) return (false, 0, null);

        // 遍历所有可能的窗口，寻找包含 centerX 的 Finder Pattern
        int currentX = 0;
        for (int i = 0; i <= rawRuns.Count - 5; i++)
        {
            // 窗口必须从黑色 run 开始
            bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
            if (!currentIsBlack)
            {
                currentX += rawRuns[i];
                continue;
            }

            // 尝试不同窗口大小
            for (int windowSize = 5; windowSize <= Math.Min(9, rawRuns.Count - i); windowSize += 2)
            {
                // 计算窗口覆盖的范围
                int windowEnd = currentX;
                for (int j = 0; j < windowSize && i + j < rawRuns.Count; j++)
                    windowEnd += rawRuns[i + j];

                // 检查 centerX 是否在此窗口范围内
                if (centerX >= currentX && centerX < windowEnd)
                {
                    int[] window = rawRuns.Skip(i).Take(windowSize).ToArray();
                    int[]? merged = TryMergeGapsInWindow(window);

                    if (merged != null && IsFinderPatternRatio(merged))
                    {
                        // 计算精确的 centerX
                        float preciseCenterX = windowEnd - merged[4] - merged[3] - merged[2] / 2.0f;
                        return (true, preciseCenterX, merged);
                    }
                }
            }

            currentX += rawRuns[i];
        }

        return (false, 0, null);
    }

    /// <summary>
    /// 提取垂直方向的 run-length 序列
    /// </summary>
    /// <returns>(runs, startsWithBlack)</returns>
    private static (List<int> runs, bool startsWithBlack) ExtractRunLengthsVertical(Image<Bgra32> image, int x, int startY, int maxHeight)
    {
        List<int> runs = new();
        if (x < 0 || x >= image.Width || startY >= image.Height)
            return (runs, false);

        bool startsWithBlack = IsPixelBlack(image[x, startY]);
        bool currentIsBlack = startsWithBlack;
        int runLength = 0;

        for (int y = startY; y < maxHeight && y < image.Height; y++)
        {
            bool isBlack = IsPixelBlack(image[x, y]);
            
            if (isBlack == currentIsBlack)
            {
                runLength++;
            }
            else
            {
                runs.Add(runLength);
                runLength = 1;
                currentIsBlack = isBlack;
            }
        }
        
        runs.Add(runLength); // 添加最后一个 run
        return (runs, startsWithBlack);
    }










    /// <summary>
    /// 在指定位置尝试解码网格数据
    /// </summary>
    private bool TryDecodeAtLocation(Image<Bgra32> image, GridLocation location)
    {
        logger?.LogDebug("[Decode] GridSize={GridSize}, CellSize={CellSize}, Origin=({X}, {Y})", 
            location.GridSize, location.CellSize, location.X, location.Y);
        
        // 保存位置信息用于调试
        LastGridLocation = location;
        
        // 1. 采样网格数据
        byte[,] grid = SampleGrid(image, location);
        
        // 保存采样结果用于调试
        LastSampledGrid = grid;

        // 2. 提取位流 (跳过角标记)
        // 计算需要的位数：元数据 + 数据 + CRC32
        int requiredBits = (METADATA_BYTES + DATA_BYTES + CRC32_BYTES) * 8; // 2656 bits
        
        int totalCells = location.GridSize * location.GridSize;
        int markerCells = 4 * CORNER_TOTAL_SIZE * CORNER_TOTAL_SIZE;
        int availableDataCells = totalCells - markerCells;
        
        // 只提取需要的位数
        byte[] bits = ExtractBits(grid, location.GridSize, requiredBits);
        logger?.LogDebug("[Decode] 可用数据单元格={Available}, 需要位数={Required}, 实际提取={Actual}", 
            availableDataCells, requiredBits, bits.Length);
        logger?.LogDebug("[Decode] 跳过的角标记单元格数: {MarkerCells}", markerCells);
        
        // 输出前64个bits用于诊断
        if (bits.Length >= 64)
        {
            logger?.LogDebug("[Decode] 前64个bits: {Bits}", string.Join("", bits.Take(64)));
        }

        // 3. 位转字节
        byte[] bytes = BitsToBytes(bits);
        int expectedByteCount = METADATA_BYTES + DATA_BYTES + CRC32_BYTES;
        int expectedBits = expectedByteCount * 8; // 2656 bits
        logger?.LogDebug("[Decode] bytes.Length={BytesLength}, Expected={Expected}", 
            bytes.Length, expectedByteCount);
        logger?.LogDebug("[Decode] 期望总位数: {ExpectedBits}, 实际位数: {ActualBits}", 
            expectedBits, bits.Length);
        
        // 输出前 32 字节的十六进制
        if (bytes.Length >= 32)
        {
            logger?.LogDebug("[Decode] First 32 bytes: {Bytes}", BitConverter.ToString(bytes, 0, 32));
        }

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
        logger?.LogDebug("[Decode] Metadata: Version={Version}, FieldCount={FieldCount}", 
            LastMetadata.Version, LastMetadata.FieldCount);

        // 6. 验证版本和字段数
        if (LastMetadata.FieldCount != FIELD_COUNT)
        {
            LastError = $"字段数不匹配: 期望{FIELD_COUNT}, 实际{LastMetadata.FieldCount}";
            return false;
        }

        // 7. 验证CRC32
        int crcStartIndex = METADATA_BYTES + DATA_BYTES;
        logger?.LogDebug("[Decode] CRC32 position: index={Index}, bytes={B0:X2}-{B1:X2}-{B2:X2}-{B3:X2}", 
            crcStartIndex, bytes[crcStartIndex], bytes[crcStartIndex+1], bytes[crcStartIndex+2], bytes[crcStartIndex+3]);
        
        // 输出用于 CRC 计算的完整数据（前 328 字节）
        if (logger != null && logger.IsEnabled(LogLevel.Debug))
        {
            string hexDump = BitConverter.ToString(bytes, 0, METADATA_BYTES + DATA_BYTES);
            logger.LogDebug("[Decode] 前328字节(用于CRC计算):\n{HexDump}", hexDump);
        }
        
        ReadOnlySpan<byte> dataForCrc = bytes.AsSpan(0, METADATA_BYTES + DATA_BYTES);
        uint calculatedCrc = CalculateCRC32(dataForCrc);
        uint storedCrc = BinaryPrimitives.ReadUInt32BigEndian(
            bytes.AsSpan(METADATA_BYTES + DATA_BYTES, CRC32_BYTES));

        if (calculatedCrc != storedCrc)
        {
            logger?.LogWarning("[Decode] CRC32校验失败: 计算={Calculated:X8}, 存储={Stored:X8}", calculatedCrc, storedCrc);
            logger?.LogDebug("[Decode] CRC 数据范围: 0 到 {End} 字节 (元数据 + 数据 = {Meta} + {Data})", 
                METADATA_BYTES + DATA_BYTES, METADATA_BYTES, DATA_BYTES);
            
            // 输出前后几个字段的值用于诊断
            logger?.LogDebug("[Decode] 前5个字段值: [{F0}] [{F1}] [{F2}] [{F3}] [{F4}]",
                (bytes[4] << 16) | (bytes[5] << 8) | bytes[6],
                (bytes[7] << 16) | (bytes[8] << 8) | bytes[9],
                (bytes[10] << 16) | (bytes[11] << 8) | bytes[12],
                (bytes[13] << 16) | (bytes[14] << 8) | bytes[15],
                (bytes[16] << 16) | (bytes[17] << 8) | bytes[18]);
            
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
    /// 计算单元格中心坐标
    /// 优先使用 Timing Pattern 检测的边界数组，回退到线性插值
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int x, int y) GetCellCenter(GridLocation location, int row, int col)
    {
        int centerX, centerY;
        
        // 如果有 Timing Pattern 检测的边界数组，使用精确边界
        if (location.XBoundaries != null && location.YBoundaries != null)
        {
            float cellLeft = location.XBoundaries[col];
            float cellRight = location.XBoundaries[col + 1];
            float cellTop = location.YBoundaries[row];
            float cellBottom = location.YBoundaries[row + 1];
            
            centerX = (int)Math.Round((cellLeft + cellRight) / 2.0f);
            centerY = (int)Math.Round((cellTop + cellBottom) / 2.0f);
        }
        else
        {
            // 回退：使用通过三个 Finder Pattern 校准后的单元格尺寸
            // 这样可以补偿 Lua FontString 渲染的非均匀间距
            float calibratedCellSizeX = location.CalibratedCellSizeX;
            float calibratedCellSizeY = location.CalibratedCellSizeY;
            
            float halfCellX = calibratedCellSizeX / 2.0f;
            float halfCellY = calibratedCellSizeY / 2.0f;
            
            // 四舍五入到最近的整数像素
            centerX = (int)Math.Round(location.X + col * calibratedCellSizeX + halfCellX);
            centerY = (int)Math.Round(location.Y + row * calibratedCellSizeY + halfCellY);
        }
        
        return (centerX, centerY);
    }

    /// <summary>
    /// 从图像中采样网格
    /// </summary>
    private static byte[,] SampleGrid(Image<Bgra32> image, GridLocation location)
    {
        int gridSize = location.GridSize;
        byte[,] grid = new byte[gridSize, gridSize];

        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                // 获取单元格中心点
                var (x, y) = GetCellCenter(location, row, col);

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
    /// 从网格中提取位流 (跳过角标记)
    /// </summary>
    /// <param name="grid">采样的网格数据</param>
    /// <param name="gridSize">网格大小</param>
    /// <param name="maxBits">最多提取的位数（用于限制提取范围）</param>
    private static byte[] ExtractBits(byte[,] grid, int gridSize, int maxBits)
    {
        byte[] bits = new byte[maxBits];
        int bitIndex = 0;

        for (int row = 0; row < gridSize && bitIndex < maxBits; row++)
        {
            for (int col = 0; col < gridSize && bitIndex < maxBits; col++)
            {
                if (!IsInCorner(row, col, gridSize) && !IsTimingPattern(row, col, gridSize))
                {
                    bits[bitIndex++] = grid[row, col];
                }
            }
        }

        return bits;
    }

    /// <summary>
    /// 判断是否是 Timing Pattern 位置
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsTimingPattern(int row, int col, int gridSize)
    {
        int timingStart = CORNER_TOTAL_SIZE;  // 8
        int timingEnd = gridSize - CORNER_TOTAL_SIZE - 1;  // 56 (for 65x65)
        
        // 水平 Timing Pattern：第3行（0-indexed），从第8列到第56列
        if (row == TIMING_PATTERN_ROW && col >= timingStart && col <= timingEnd)
        {
            return true;
        }
        
        // 垂直 Timing Pattern：第3列（0-indexed），从第8行到第56行
        if (col == TIMING_PATTERN_COL && row >= timingStart && row <= timingEnd)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 检查单元格是否在角标记区域（包含定向静区）
    /// 定向静区：静区只在朝向数据区的方向，会扩展到角标记区域外1格
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInCorner(int row, int col, int gridSize)
    {
        int cornerMaxRow = CORNER_TOTAL_SIZE;  // 8
        int cornerMinRow = gridSize - CORNER_TOTAL_SIZE;  // 57 (for 65x65)
        int cornerMaxCol = CORNER_TOTAL_SIZE;  // 8
        int cornerMinCol = gridSize - CORNER_TOTAL_SIZE;  // 57 (for 65x65)
        
        // 左上角：Finder(0-6,0-6) + 静区(第7行,第7列)
        // 占据区域：0-7行, 0-7列
        if (row < cornerMaxRow && col < cornerMaxCol)
        {
            return true;
        }
        
        // 右上角：Finder(0-6,57-63) + 静区(第7行, 第56列)
        // 占据区域：0-7行, 56-64列（包含左侧静区第56列）
        if (row < cornerMaxRow && col >= cornerMinCol - 1)
        {
            return true;
        }
        
        // 左下角：Finder(57-63,0-6) + 静区(第56行, 第7列)
        // 占据区域：56-64行（包含上侧静区第56行）, 0-7列
        if (row >= cornerMinRow - 1 && col < cornerMaxCol)
        {
            return true;
        }
        
        // 右下角：Finder(57-63,57-63) + 静区(第56行, 第56列)
        // 占据区域：56-64行, 56-64列
        if (row >= cornerMinRow - 1 && col >= cornerMinCol - 1)
        {
            return true;
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
        // FontString 渲染的黑色非常接近纯黑
        // 使用更严格的阈值: 所有通道都 < 30
        return pixel.R < 30 && pixel.G < 30 && pixel.B < 30;
    }

    /// <summary>
    /// 保存采样点可视化调试图
    /// 在每个单元格中心绘制4×4标记: 亮绿色=黑色单元格, 洋红色=白色单元格
    /// </summary>
    public static void SaveSamplingVisualization(Image<Bgra32> originalImage, GridLocation location, byte[,] sampledGrid, string outputPath = "sampling_debug.jpg")
    {
        // 克隆原图用于绘制
        using var debugImage = originalImage.Clone();
        int gridSize = location.GridSize;

        // 定义标记颜色 (BGR格式，使用高对比度颜色)
        var greenMarker = new Bgra32(0, 255, 0, 255);    // 亮绿色 - 标记黑色单元格(bit=1)
        var magentaMarker = new Bgra32(255, 0, 255, 255); // 洋红色 - 标记白色单元格(bit=0)

        // 标记大小 (4×4 更明显)
        int markerSize = 4;
        int markerOffset = -markerSize / 2; // 居中偏移

        for (int row = 0; row < gridSize; row++)
        {
            for (int col = 0; col < gridSize; col++)
            {
                // 使用统一的坐标计算函数
                var (centerX, centerY) = GetCellCenter(location, row, col);

                // 选择颜色: 黑色单元格用亮绿色标记, 白色单元格用洋红色标记
                var markerColor = sampledGrid[row, col] == 1 ? greenMarker : magentaMarker;

                // 绘制4×4标记 (居中在采样点)
                for (int dy = 0; dy < markerSize; dy++)
                {
                    for (int dx = 0; dx < markerSize; dx++)
                    {
                        int x = centerX + markerOffset + dx;
                        int y = centerY + markerOffset + dy;

                        if (x >= 0 && x < debugImage.Width && y >= 0 && y < debugImage.Height)
                        {
                            debugImage[x, y] = markerColor;
                        }
                    }
                }
            }
        }

        debugImage.SaveAsJpeg(outputPath);
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

        // 注意: 这里使用 65 作为估算值，实际网格大小会动态检测
        int minCellSize = 4;
        int estimatedGridSize = 65; // 典型网格大小
        int maxCellSize = Math.Min(width, height) / estimatedGridSize;
        maxCellSize = Math.Max(maxCellSize, minCellSize);
        maxCellSize = Math.Min(maxCellSize, 16);

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
    public float CalibratedCellSizeX { get; init; }  // 通过 Finder Pattern 校准的 X 方向单元格尺寸
    public float CalibratedCellSizeY { get; init; }  // 通过 Finder Pattern 校准的 Y 方向单元格尺寸
    public int GridSize { get; init; }  // 动态检测的网格大小 (如 65×65)
    public float[]? XBoundaries { get; init; }  // X方向单元格边界数组 (length = gridSize + 1)
    public float[]? YBoundaries { get; init; }  // Y方向单元格边界数组 (length = gridSize + 1)

    public override string ToString() => $"({X}, {Y}) Grid={GridSize}×{GridSize} Cell={CellSize}px CalX={CalibratedCellSizeX:F2}px CalY={CalibratedCellSizeY:F2}px";
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
    public float EstimatedModuleSizeX { get; init; }
    public float EstimatedModuleSizeY { get; init; }
    
    public float EstimatedModuleSize => (EstimatedModuleSizeX + EstimatedModuleSizeY) / 2.0f;

    public FinderPattern(float centerX, float centerY, float moduleSizeX, float moduleSizeY)
    {
        CenterX = centerX;
        CenterY = centerY;
        EstimatedModuleSizeX = moduleSizeX;
        EstimatedModuleSizeY = moduleSizeY;
    }
    
    public FinderPattern(float centerX, float centerY, float moduleSize)
        : this(centerX, centerY, moduleSize, moduleSize)
    {
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
    
    public override string ToString() =>
        $"Center=({CenterX:F1}, {CenterY:F1}), ModuleSize=({EstimatedModuleSizeX:F2}, {EstimatedModuleSizeY:F2})";
}
