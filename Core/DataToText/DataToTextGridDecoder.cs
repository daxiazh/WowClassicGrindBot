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
    /// 在屏幕中搜索DataToText网格 - 优化版（渐进式定位）
    /// 假设：屏幕中只有一个网格，完整显示
    /// </summary>
    private static GridLocation? FindGridInScreen(Image<Bgra32> screenImage, ILogger<DataToTextGridDecoder>? logger)
    {
        int width = screenImage.Width;
        int height = screenImage.Height;

        logger?.LogDebug("[FindGridInScreen] 开始搜索 {Width}×{Height} 屏幕", width, height);

        // 步骤 1: 跳行全屏搜索第一个 Finder Pattern
        FinderPattern? firstPattern = FindFirstFinderPattern(screenImage);
        if (firstPattern == null)
        {
            logger?.LogDebug("[FindGridInScreen] 未找到任何 Finder Pattern");
            return null;
        }

        int cellSize = (int)Math.Round(firstPattern.EstimatedModuleSize);
        logger?.LogDebug("[FindGridInScreen] 找到第一个角 @ ({CenterX:F1}, {CenterY:F1}), CellSize={CellSize}px", 
            firstPattern.CenterX, firstPattern.CenterY, cellSize);

        // 步骤 2: 在第一个角的同一行向右搜索第二个角
        FinderPattern? topRightPattern = FindPatternInRow(screenImage, firstPattern, cellSize);
        if (topRightPattern == null)
        {
            logger?.LogDebug("[FindGridInScreen] 未找到同行的第二个角");
            return null;
        }

        // 步骤 3: 在第一个角的同一列向下搜索第三个角
        FinderPattern? bottomLeftPattern = FindPatternInColumn(screenImage, firstPattern, cellSize);
        if (bottomLeftPattern == null)
        {
            logger?.LogDebug("[FindGridInScreen] 未找到同列的第三个角");
            return null;
        }

        // 步骤 4: 计算网格大小和原点
        // Finder Pattern 中心在 (3.5, 3.5) 个模块位置，即第 4 个单元格 (1-indexed)
        // 两个 Finder 中心之间的距离 = gridSize - 2*4 = gridSize - 8
        // 因此: gridSize = centerDistance + 8
        int horizontalDistance = (int)Math.Abs(topRightPattern.CenterX - firstPattern.CenterX);
        int verticalDistance = (int)Math.Abs(bottomLeftPattern.CenterY - firstPattern.CenterY);
        int centerDistanceH = (int)Math.Round((float)horizontalDistance / cellSize);
        int centerDistanceV = (int)Math.Round((float)verticalDistance / cellSize);
        
        const int FINDER_CENTER_CELL = 4; // Finder Pattern 中心在第 4 个单元格
        int gridSizeH = centerDistanceH + 2 * FINDER_CENTER_CELL;
        int gridSizeV = centerDistanceV + 2 * FINDER_CENTER_CELL;

        // 验证两个方向的网格大小一致
        if (Math.Abs(gridSizeH - gridSizeV) > 2)
        {
            logger?.LogDebug("[FindGridInScreen] 网格大小不一致: H={GridSizeH}, V={GridSizeV}", gridSizeH, gridSizeV);
            return null;
        }

        int gridSize = (gridSizeH + gridSizeV) / 2; // 取平均
        logger?.LogDebug("[FindGridInScreen] 找到三个角, GridSize={GridSize}×{GridSize}", gridSize, gridSize);

        // Finder Pattern 中心在 (3.5, 3.5) 个模块位置
        float finderCenterOffset = 3.5f * cellSize;
        int gridX = (int)Math.Round(Math.Min(firstPattern.CenterX, topRightPattern.CenterX) - finderCenterOffset);
        int gridY = (int)Math.Round(Math.Min(firstPattern.CenterY, bottomLeftPattern.CenterY) - finderCenterOffset);
        logger?.LogDebug("[FindGridInScreen] 计算网格原点: ({GridX}, {GridY})", gridX, gridY);

        logger?.LogInformation("[FindGridInScreen] 成功定位网格: Grid={GridSize}×{GridSize}, Cell={CellSize}px", gridSize, gridSize, cellSize);

        return new GridLocation
        {
            X = gridX,
            Y = gridY,
            CellSize = cellSize,
            GridSize = gridSize
        };
    }

    /// <summary>
    /// 跳行全屏搜索第一个 Finder Pattern
    /// </summary>
    private static FinderPattern? FindFirstFinderPattern(Image<Bgra32> image)
    {
        int width = image.Width;
        int height = image.Height;
        int stepSize = 3; // 跳行步长（Finder Pattern 高度约 7×cellSize ≈ 28-70px）

        for (int y = 0; y < height; y += stepSize)
        {
            var (rawRuns, startsWithBlack) = ExtractRunLengths(image, y, width);

            // 遍历所有可能的窗口起点
            for (int i = 0; i <= rawRuns.Count - 5; i++)
            {
                // 窗口必须从黑色 run 开始
                bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
                if (!currentIsBlack) continue;

                // 尝试不同窗口大小：5（无间隙）、7（1个间隙）、9（2个间隙）
                // for (int windowSize = 5; windowSize <= Math.Min(9, rawRuns.Count - i); windowSize += 2)
                var windowSize = 9;
                if(rawRuns.Count - i < windowSize)
                    continue;   // 不够9个了, 目前的实现中一定会有缝隙, 所以我们只考虑9个 Run List 的情况, 跳过这一行吧
                {
                    int[] window = rawRuns.Skip(i).Take(windowSize).ToArray();
                    int[]? merged = TryMergeGapsInWindow(window);

                    if (merged != null && IsFinderPatternRatio(merged))
                    {
                        // 计算在原始 runs 中的结束位置
                        int endX = CalculateRunEndPosition(rawRuns, i + windowSize - 1);
                        float centerX = endX - merged[4] - merged[3] - merged[2] / 2.0f;

                        int estimatedHeight = merged.Sum();
                        var (success, preciseCenterY) = VerifyVerticalPattern(image, (int)centerX, y, estimatedHeight);
                        if (success)
                        {
                            float moduleSize = merged.Sum() / 7.0f;
                            return new FinderPattern(centerX, preciseCenterY, moduleSize);
                        }
                    }
                }
            }
        }

        return null;
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
    /// <returns>(success, preciseCenterY) - 成功标志和精确的 Y 中心坐标</returns>
    private static (bool success, float preciseCenterY) VerifyVerticalPattern(
        Image<Bgra32> image, int centerX, int centerY, int estimatedHeight)
    {
        // 从 centerY 向上下扫描，提取垂直 run-length
        int searchRadius = estimatedHeight * 2; // 留出足够余量

        int startY = Math.Max(0, centerY - searchRadius);
        int endY = Math.Min(image.Height - 1, centerY + searchRadius);

        // 提取垂直 run-length
        var (rawRuns, startsWithBlack) = ExtractRunLengthsVertical(image, centerX, startY, endY + 1);
        if (rawRuns.Count < 5) return (false, 0);

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
                return (false, 0);   // 不够9个了, 目前的实现中一定会有缝隙, 所以我们只考虑9个 Run List 的情况
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
                        return (true, preciseCenterY);
                    }
                }
            }

            currentY += rawRuns[i];
        }

        return (false, 0);
    }

    /// <summary>
    /// 水平方向验证 Finder Pattern（使用 run-length + 窗口合并）
    /// </summary>
    /// <param name="estimatedWidth">估算的 Finder Pattern 宽度（用于优化，当前未使用）</param>
    /// <returns>(success, preciseCenterX) - 成功标志和精确的 X 中心坐标</returns>
    private static (bool success, float preciseCenterX) VerifyHorizontalPattern(
        Image<Bgra32> image, int centerX, int centerY, int estimatedWidth)
    {
        // 提取整行的水平 run-length
        var (rawRuns, startsWithBlack) = ExtractRunLengths(image, centerY, image.Width);
        if (rawRuns.Count < 5) return (false, 0);

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
                        return (true, preciseCenterX);
                    }
                }
            }

            currentX += rawRuns[i];
        }

        return (false, 0);
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
    /// 在同一行向右搜索下一个 Finder Pattern
    /// </summary>
    private static FinderPattern? FindPatternInRow(
        Image<Bgra32> image, FinderPattern anchor, int cellSize)
    {
        const int MIN_GRID_SIZE = 30;
        const int MAX_GRID_SIZE = 200;
        const int TOLERANCE = 5;

        int centerX = (int)anchor.CenterX;
        int centerY = (int)anchor.CenterY;
        int minDistance = MIN_GRID_SIZE * cellSize;

        for (int dy = -TOLERANCE; dy <= TOLERANCE; dy++)
        {
            int searchY = centerY + dy;
            if (searchY < 0 || searchY >= image.Height) continue;

            // 从 anchor 右侧开始搜索，避免找到 anchor 自身
            int searchStartX = centerX + cellSize * 5; // 跳过 anchor 区域
            if (searchStartX >= image.Width) continue;
            
            var (rawRuns, startsWithBlack) = ExtractRunLengths(image, searchY, image.Width, searchStartX);

            for (int i = 0; i <= rawRuns.Count - 5; i++)
            {
                bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
                if (!currentIsBlack) continue;

                // 目前的实现中 FontString 一定会有缝隙，所以我们只考虑9个 Run List 的情况
                var windowSize = 9;
                if (rawRuns.Count - i < windowSize)
                    continue; // 不够9个了，跳过这个起点
                {
                    int[] window = rawRuns.Skip(i).Take(windowSize).ToArray();
                    int[]? merged = TryMergeGapsInWindow(window);

                    if (merged != null && IsFinderPatternRatio(merged))
                    {
                        int endOffset = CalculateRunEndPosition(rawRuns, i + windowSize - 1);
                        float patternCenterX = searchStartX + endOffset - merged[4] - merged[3] - merged[2] / 2.0f;

                        // 必须在右侧且距离合理
                        if (patternCenterX <= centerX + cellSize * 5) continue;
                        int distance = (int)(patternCenterX - centerX);
                        if (distance < minDistance) continue;

                        int gridSize = (int)Math.Round((float)distance / cellSize);
                        if (gridSize < MIN_GRID_SIZE || gridSize > MAX_GRID_SIZE) continue;

                        int estimatedHeight = merged.Sum();
                        var (success, preciseCenterY) = VerifyVerticalPattern(image, (int)patternCenterX, searchY, estimatedHeight);
                        if (success)
                        {
                            float moduleSize = merged.Sum() / 7.0f;
                            return new FinderPattern(patternCenterX, preciseCenterY, moduleSize);
                        }
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// 在同一列向下搜索下一个 Finder Pattern
    /// </summary>
    private static FinderPattern? FindPatternInColumn(
        Image<Bgra32> image, FinderPattern anchor, int cellSize)
    {
        const int MIN_GRID_SIZE = 30;
        const int MAX_GRID_SIZE = 200;
        const int TOLERANCE = 5;

        int centerX = (int)anchor.CenterX;
        int centerY = (int)anchor.CenterY;
        int minDistance = MIN_GRID_SIZE * cellSize;

        for (int dx = -TOLERANCE; dx <= TOLERANCE; dx++)
        {
            int searchX = centerX + dx;
            if (searchX < 0 || searchX >= image.Width) continue;

            // 从 anchor 下方开始搜索，避免找到 anchor 自身
            int searchStartY = centerY + cellSize * 5; // 跳过 anchor 区域
            if (searchStartY >= image.Height) continue;
            
            var (rawRuns, startsWithBlack) = ExtractRunLengthsVertical(image, searchX, searchStartY, image.Height);

            for (int i = 0; i <= rawRuns.Count - 5; i++)
            {
                bool currentIsBlack = startsWithBlack ? (i % 2 == 0) : (i % 2 == 1);
                if (!currentIsBlack) continue;

                // 目前的实现中 FontString 一定会有缝隙，所以我们只考虑9个 Run List 的情况
                var windowSize = 9;
                if (rawRuns.Count - i < windowSize)
                    continue; // 不够9个了，跳过这个起点
                {
                    int[] window = rawRuns.Skip(i).Take(windowSize).ToArray();
                    int[]? merged = TryMergeGapsInWindow(window);

                    if (merged != null && IsFinderPatternRatio(merged))
                    {
                        int endOffset = CalculateRunEndPosition(rawRuns, i + windowSize - 1);
                        float patternCenterY = searchStartY + endOffset - merged[4] - merged[3] - merged[2] / 2.0f;

                        // 必须在下方且距离合理
                        if (patternCenterY <= centerY + cellSize * 5) continue;
                        int distance = (int)(patternCenterY - centerY);
                        if (distance < minDistance) continue;

                        int gridSize = (int)Math.Round((float)distance / cellSize);
                        if (gridSize < MIN_GRID_SIZE || gridSize > MAX_GRID_SIZE) continue;

                        int estimatedWidth = merged.Sum();
                        var (success, preciseCenterX) = VerifyHorizontalPattern(image, searchX, (int)patternCenterY, estimatedWidth);
                        if (success)
                        {
                            float moduleSize = merged.Sum() / 7.0f;
                            return new FinderPattern(preciseCenterX, patternCenterY, moduleSize);
                        }
                    }
                }
            }
        }

        return null;
    }


    /// <summary>
    /// 验证指定位置是否存在 Finder Pattern（采样验证）
    /// </summary>
    private static bool VerifyFinderPatternAt(Image<Bgra32> image, int centerX, int centerY, int cellSize)
    {
        int startX = centerX - (int)(3.5f * cellSize);
        int startY = centerY - (int)(3.5f * cellSize);

        int matchCount = 0;
        int totalCount = 0;

        // 采样 7×7 个点
        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                int sampleX = startX + col * cellSize + cellSize / 2;
                int sampleY = startY + row * cellSize + cellSize / 2;

                // 边界检查
                if (sampleX < 0 || sampleY < 0 || sampleX >= image.Width || sampleY >= image.Height)
                    continue;

                bool shouldBeBlack;
                if (row == 0 || row == 6 || col == 0 || col == 6)
                    shouldBeBlack = true; // 外框
                else if (row >= 2 && row <= 4 && col >= 2 && col <= 4)
                    shouldBeBlack = true; // 中心 3×3
                else
                    shouldBeBlack = false; // 内白框

                bool isBlack = IsPixelBlack(image[sampleX, sampleY]);
                totalCount++;

                if (shouldBeBlack == isBlack)
                    matchCount++;
            }
        }

        // 85% 阈值
        return matchCount >= (totalCount * 85 / 100);
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
        logger?.LogDebug("[Decode] GridSize={GridSize}, CellSize={CellSize}, Origin=({X}, {Y})", 
            location.GridSize, location.CellSize, location.X, location.Y);
        
        // 1. 采样网格数据
        byte[,] grid = SampleGrid(image, location);

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
                if (!IsInCorner(row, col, gridSize))
                {
                    bits[bitIndex++] = grid[row, col];
                }
            }
        }

        return bits;
    }

    /// <summary>
    /// 检查单元格是否在角标记区域（包含静区）
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInCorner(int row, int col, int gridSize)
    {
        // 上半部分（前8行：7行Finder + 1行静区）
        if (row < CORNER_TOTAL_SIZE)
        {
            return col < CORNER_TOTAL_SIZE || col >= gridSize - CORNER_TOTAL_SIZE;
        }
        // 下半部分（后8行）
        if (row >= gridSize - CORNER_TOTAL_SIZE)
        {
            return col < CORNER_TOTAL_SIZE || col >= gridSize - CORNER_TOTAL_SIZE;
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
    public int GridSize { get; init; }  // 动态检测的网格大小 (如 65×65)

    public override string ToString() => $"({X}, {Y}) Grid={GridSize}×{GridSize} CellSize={CellSize}px";
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
