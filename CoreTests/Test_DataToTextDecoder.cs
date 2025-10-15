using Core;
using Core.DataToText;
using Game;
using Microsoft.Extensions.Logging;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CoreTests;

/// <summary>
/// DataToText解码器实时测试
/// 从游戏窗口截图并解码65×65网格数据
/// </summary>
internal sealed class Test_DataToTextDecoder : IDisposable
{
    private readonly Microsoft.Extensions.Logging.ILogger logger;
    private readonly WowScreenDXGI screen;
    private readonly DataToTextGridDecoder decoder;

    private long totalDecodeTime;
    private int successCount;
    private int failureCount;

    public Test_DataToTextDecoder(
        Microsoft.Extensions.Logging.ILogger logger,
        WowScreenDXGI screen)
    {
        this.logger = logger;
        this.screen = screen;
        this.decoder = new DataToTextGridDecoder();

        // 显示搜索范围信息
        var (minCell, maxCell, searchCount) = DataToTextGridDecoder.GetSearchRangeInfo(screen.ScreenImage);
        logger.LogInformation($"DataToText解码器初始化完成");
        logger.LogInformation($"屏幕尺寸: {screen.ScreenImage.Width}×{screen.ScreenImage.Height}");
        logger.LogInformation($"单元格搜索范围: {minCell}px - {maxCell}px ({searchCount}个尺寸)");
        logger.LogInformation($"预期网格尺寸: {minCell * 65}×{minCell * 65}px - {maxCell * 65}×{maxCell * 65}px");
    }

    public void Dispose()
    {
        // 打印统计信息
        int totalAttempts = successCount + failureCount;
        if (totalAttempts > 0)
        {
            double avgTime = totalDecodeTime / (double)totalAttempts;
            double successRate = (successCount * 100.0) / totalAttempts;

            logger.LogInformation("=== DataToText解码统计 ===");
            logger.LogInformation($"总尝试次数: {totalAttempts}");
            logger.LogInformation($"成功: {successCount} ({successRate:F1}%)");
            logger.LogInformation($"失败: {failureCount}");
            logger.LogInformation($"平均解码时间: {avgTime:F2}ms");
        }
    }

    /// <summary>
    /// 执行一次解码测试
    /// </summary>
    /// <returns>解码耗时(毫秒)</returns>
    public double Execute()
    {
        // 更新屏幕截图（这是关键！）
        screen.Update();

        long startTime = Stopwatch.GetTimestamp();

        // 从WowScreenDXGI获取屏幕截图
        Image<Bgra32> screenImage = screen.ScreenImage;

        // 执行解码
        bool success = decoder.DecodeFromScreen(screenImage);

        double elapsed = Stopwatch.GetElapsedTime(startTime).TotalMilliseconds;
        totalDecodeTime += (long)elapsed;

        if (success)
        {
            successCount++;
            LogSuccessfulDecode(elapsed);
        }
        else
        {
            failureCount++;
            LogFailedDecode(elapsed);
        }

        return elapsed;
    }

    /// <summary>
    /// 记录成功的解码
    /// </summary>
    private void LogSuccessfulDecode(double elapsed)
    {
        var meta = decoder.LastMetadata;
        var fields = decoder.Fields;

        logger.LogInformation($"✓ 解码成功 ({elapsed:F2}ms) - {meta}");

        // 打印前10个字段的值（示例）
        logger.LogInformation("前10个字段:");
        for (int i = 0; i < Math.Min(10, fields.Length); i++)
        {
            logger.LogInformation($"  字段[{i:D3}] = {fields[i]:D8} (0x{fields[i]:X6})");
        }

        // 打印一些关键字段 (参考DataToColor的字段定义)
        PrintKeyFields();
    }

    /// <summary>
    /// 打印关键游戏字段
    /// </summary>
    private void PrintKeyFields()
    {
        var fields = decoder.Fields;

        // 根据FieldCollector.lua的字段定义
        // 这里只是示例，实际字段映射需要根据Lua实现调整

        logger.LogInformation("关键字段:");

        // 假设字段0-9是玩家基础信息
        if (fields.Length > 0)
            logger.LogInformation($"  玩家等级: {fields[0]}");

        if (fields.Length > 1)
            logger.LogInformation($"  生命值: {fields[1]}");

        if (fields.Length > 2)
            logger.LogInformation($"  法力值: {fields[2]}");

        // 更多字段可以根据需要添加
    }

    /// <summary>
    /// 记录失败的解码
    /// </summary>
    private void LogFailedDecode(double elapsed)
    {
        string error = decoder.LastError ?? "未知错误";
        logger.LogWarning($"✗ 解码失败 ({elapsed:F2}ms) - {error}");
    }

    /// <summary>
    /// 持续监控模式 (用于调试)
    /// </summary>
    /// <param name="intervalMs">更新间隔(毫秒)</param>
    /// <param name="count">测试次数</param>
    public void ContinuousMonitor(int intervalMs = 500, int count = 100)
    {
        logger.LogInformation($"开始持续监控模式 (间隔={intervalMs}ms, 次数={count})");

        for (int i = 0; i < count; i++)
        {
            logger.LogInformation($"--- 第 {i + 1}/{count} 次测试 ---");
            Execute();
            Thread.Sleep(intervalMs);
        }

        logger.LogInformation("持续监控完成");
    }

    /// <summary>
    /// 性能压力测试
    /// </summary>
    /// <param name="count">测试次数</param>
    public void PerformanceTest(int count = 50)
    {
        logger.LogInformation($"开始性能测试 ({count}次)");

        double[] times = new double[count];
        int successfulDecodes = 0;

        for (int i = 0; i < count; i++)
        {
            times[i] = Execute();
            if (decoder.LastDecodeSuccess)
                successfulDecodes++;

            Thread.Sleep(50); // 短暂延迟避免过载
        }

        // 统计分析
        Array.Sort(times);
        double min = times[0];
        double max = times[^1];
        double avg = 0;
        for (int i = 0; i < count; i++)
            avg += times[i];
        avg /= count;

        double median = times[count / 2];
        double p95 = times[(int)(count * 0.95)];
        double p99 = times[(int)(count * 0.99)];

        logger.LogInformation("=== 性能测试结果 ===");
        logger.LogInformation($"样本数: {count}");
        logger.LogInformation($"成功率: {successfulDecodes * 100.0 / count:F1}%");
        logger.LogInformation($"最小值: {min:F2}ms");
        logger.LogInformation($"最大值: {max:F2}ms");
        logger.LogInformation($"平均值: {avg:F2}ms");
        logger.LogInformation($"中位数: {median:F2}ms");
        logger.LogInformation($"P95: {p95:F2}ms");
        logger.LogInformation($"P99: {p99:F2}ms");
    }

    /// <summary>
    /// 保存调试截图 (用于离线分析)
    /// </summary>
    public void SaveDebugImage(string filename = "datatotext_debug.jpg")
    {
        screen.ScreenImage.SaveAsJpeg(filename);
        logger.LogInformation($"调试截图已保存: {filename}");
    }

    /// <summary>
    /// 使用保存的调试图片进行离线测试
    /// </summary>
    public static void TestWithDebugImage(
        Microsoft.Extensions.Logging.ILogger logger,
        string imagePath = "datatotext_debug.jpg")
    {
        if (!File.Exists(imagePath))
        {
            logger.LogError($"调试图片不存在: {imagePath}");
            return;
        }

        logger.LogInformation($"=== 离线测试: {imagePath} ===");
        logger.LogInformation($"文件大小: {new FileInfo(imagePath).Length / 1024} KB");

        // 加载图片
        using var image = Image.Load<Bgra32>(imagePath);
        logger.LogInformation($"图片尺寸: {image.Width}×{image.Height}");

        // 显示搜索范围信息
        var (minCell, maxCell, searchCount) = DataToTextGridDecoder.GetSearchRangeInfo(image);
        logger.LogInformation($"单元格搜索范围: {minCell}px - {maxCell}px ({searchCount}个尺寸)");
        logger.LogInformation($"预期网格尺寸: {minCell * 65}×{minCell * 65}px - {maxCell * 65}×{maxCell * 65}px");

        // 创建解码器
        var decoder = new DataToTextGridDecoder();

        // 执行解码并计时
        var sw = Stopwatch.StartNew();
        bool success = decoder.DecodeFromScreen(image);
        sw.Stop();

        logger.LogInformation($"解码耗时: {sw.Elapsed.TotalMilliseconds:F2}ms");

        if (success)
        {
            logger.LogInformation("✓ 解码成功!");
            logger.LogInformation($"元数据: {decoder.LastMetadata}");

            // 打印前10个字段
            logger.LogInformation("前10个字段:");
            for (int i = 0; i < Math.Min(10, decoder.Fields.Length); i++)
            {
                logger.LogInformation($"  字段[{i:D3}] = {decoder.Fields[i]:D8} (0x{decoder.Fields[i]:X6})");
            }
        }
        else
        {
            logger.LogError($"✗ 解码失败: {decoder.LastError}");
            logger.LogWarning("开始详细诊断...");

            // 执行详细诊断
            DiagnoseGridLocation(logger, image);
        }
    }

    /// <summary>
    /// 诊断网格定位问题
    /// </summary>
    private static void DiagnoseGridLocation(Microsoft.Extensions.Logging.ILogger logger, Image<Bgra32> image)
    {
        logger.LogInformation("=== 网格定位诊断 ===");

        int width = image.Width;
        int height = image.Height;
        int minCellSize = 4;
        int maxCellSize = Math.Min(width, height) / 65;
        maxCellSize = Math.Max(maxCellSize, minCellSize);
        maxCellSize = Math.Min(maxCellSize, 100);

        logger.LogInformation($"图片尺寸: {width}×{height}");
        logger.LogInformation($"搜索范围: cellSize={minCellSize}-{maxCellSize}");

        // 先尝试在图片中心采样一个3x3区域，看看像素值
        logger.LogInformation("\n采样图片左上角 10×10 区域的像素:");
        SampleRegion(image, 0, 0, 10, 10, logger);

        // 尝试在不同单元格大小下查找角标记
        for (int cellSize = minCellSize; cellSize <= maxCellSize; cellSize++)
        {
            int gridPixelSize = 65 * cellSize;
            if (gridPixelSize > width || gridPixelSize > height)
            {
                logger.LogWarning($"cellSize={cellSize}: 网格尺寸{gridPixelSize}×{gridPixelSize}px 超出图片范围，跳过");
                continue;
            }

            logger.LogInformation($"\n测试 cellSize={cellSize}px (网格尺寸={gridPixelSize}×{gridPixelSize}px)");

            int step = Math.Max(1, cellSize / 3);
            int foundCount = 0;

            // 扫描整个图像
            for (int y = 0; y <= height - gridPixelSize; y += step)
            {
                for (int x = 0; x <= width - gridPixelSize; x += step)
                {
                    // 测试左上角标记
                    if (TestCornerMarker(image, x, y, cellSize, logger, verbose: false))
                    {
                        foundCount++;
                        logger.LogInformation($"  找到疑似角标记 @ ({x}, {y})");

                        // 详细查看这个位置的像素
                        logger.LogInformation($"  详细检查位置 ({x}, {y}):");
                        TestCornerMarker(image, x, y, cellSize, logger, verbose: true);

                        // 验证完整的四个角
                        bool allCornersValid = TestAllCorners(image, x, y, cellSize, logger);
                        if (allCornersValid)
                        {
                            logger.LogInformation($"  ✓ 所有四个角都验证通过! 位置=({x}, {y}), cellSize={cellSize}");

                            // 可视化这个位置
                            VisualizeGrid(image, x, y, cellSize, $"found_grid_{cellSize}px.jpg");
                            return;
                        }
                        else
                        {
                            logger.LogWarning($"  ✗ 其他角标记验证失败");
                        }
                    }
                }
            }

            logger.LogInformation($"  cellSize={cellSize}: 找到 {foundCount} 个疑似位置");
        }

        logger.LogError("未能找到有效的网格位置");
        logger.LogWarning("\n建议:");
        logger.LogWarning("1. 检查游戏中 DataToText 插件是否正确显示");
        logger.LogWarning("2. 确认网格是否在截图范围内");
        logger.LogWarning("3. 检查网格渲染的颜色对比度");
    }

    /// <summary>
    /// 采样图片区域的像素值
    /// </summary>
    private static void SampleRegion(Image<Bgra32> image, int startX, int startY, int width, int height, Microsoft.Extensions.Logging.ILogger logger)
    {
        for (int y = 0; y < height && startY + y < image.Height; y++)
        {
            string line = "";
            for (int x = 0; x < width && startX + x < image.Width; x++)
            {
                var pixel = image[startX + x, startY + y];
                int brightness = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                bool isBlack = IsPixelBlack(pixel);
                line += isBlack ? "█" : "░";
            }
            logger.LogInformation($"  {line}");
        }
    }

    /// <summary>
    /// 测试单个7×7 Finder Pattern角标记
    /// </summary>
    private static bool TestCornerMarker(Image<Bgra32> image, int startX, int startY, int cellSize,
        Microsoft.Extensions.Logging.ILogger logger, bool verbose = false)
    {
        int markerWidth = 7 * cellSize;
        int markerHeight = 7 * cellSize;
        if (startX + markerWidth > image.Width || startY + markerHeight > image.Height)
            return false;

        int matchingSamples = 0;
        int totalSamples = 0;

        for (int row = 0; row < 7; row++)
        {
            for (int col = 0; col < 7; col++)
            {
                // 7×7 QR码 Finder Pattern:
                // 外层黑框 (row/col == 0 或 6)
                // 内层白框 (1 <= row/col <= 5 且不在中心)
                // 中心黑块 (2 <= row/col <= 4)
                bool shouldBeBlack;
                if (row == 0 || row == 6 || col == 0 || col == 6)
                    shouldBeBlack = true;  // 外层黑框
                else if (row >= 2 && row <= 4 && col >= 2 && col <= 4)
                    shouldBeBlack = true;  // 中心黑块
                else
                    shouldBeBlack = false; // 内层白框

                int cellX = startX + col * cellSize;
                int cellY = startY + row * cellSize;

                // 中心点采样
                int x = cellX + cellSize / 2;
                int y = cellY + cellSize / 2;

                if (x < image.Width && y < image.Height)
                {
                    var pixel = image[x, y];
                    bool isBlack = IsPixelBlack(pixel);
                    totalSamples++;

                    if (verbose)
                    {
                        int brightness = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                        string expected = shouldBeBlack ? "黑" : "白";
                        string actual = isBlack ? "黑" : "白";
                        string match = shouldBeBlack == isBlack ? "✓" : "✗";
                        logger.LogInformation($"    [{row},{col}] @ ({x},{y}) RGB=({pixel.R},{pixel.G},{pixel.B}) " +
                            $"亮度={brightness} 期望={expected} 实际={actual} {match}");
                    }

                    if (shouldBeBlack == isBlack)
                        matchingSamples++;
                }
            }
        }

        // 要求至少90%的采样点匹配
        return matchingSamples >= (totalSamples * 9 / 10);
    }

    /// <summary>
    /// 测试所有四个7×7角标记
    /// </summary>
    private static bool TestAllCorners(Image<Bgra32> image, int gridX, int gridY, int cellSize,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        int gridPixelSize = 65 * cellSize;
        int markerPixelSize = 7 * cellSize;

        // 左上角
        if (!TestCornerMarker(image, gridX, gridY, cellSize, logger, verbose: true))
        {
            logger.LogWarning("    左上角失败");
            return false;
        }

        // 右上角
        if (!TestCornerMarker(image, gridX + gridPixelSize - markerPixelSize, gridY, cellSize, logger, verbose: false))
        {
            logger.LogWarning("    右上角失败");
            return false;
        }

        // 左下角
        if (!TestCornerMarker(image, gridX, gridY + gridPixelSize - markerPixelSize, cellSize, logger, verbose: false))
        {
            logger.LogWarning("    左下角失败");
            return false;
        }

        // 右下角
        if (!TestCornerMarker(image, gridX + gridPixelSize - markerPixelSize,
            gridY + gridPixelSize - markerPixelSize, cellSize, logger, verbose: false))
        {
            logger.LogWarning("    右下角失败");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 判断像素是否为黑色（与解码器中的逻辑一致）
    /// </summary>
    private static bool IsPixelBlack(Bgra32 pixel)
    {
        return pixel.R < 40 && pixel.G < 40 && pixel.B < 40;
    }

    /// <summary>
    /// 可视化找到的网格位置
    /// </summary>
    private static void VisualizeGrid(Image<Bgra32> image, int gridX, int gridY, int cellSize, string outputPath)
    {
        var visualImage = image.Clone();
        int gridSize = 65 * cellSize;

        // 在网格边界画红框
        var red = new Bgra32(255, 0, 0, 255);

        // 上边界
        for (int x = gridX; x < gridX + gridSize && x < visualImage.Width; x++)
        {
            for (int t = 0; t < 3 && gridY + t < visualImage.Height; t++)
                visualImage[x, gridY + t] = red;
        }

        // 下边界
        for (int x = gridX; x < gridX + gridSize && x < visualImage.Width; x++)
        {
            for (int t = 0; t < 3 && gridY + gridSize - 1 - t >= 0; t++)
                visualImage[x, gridY + gridSize - 1 - t] = red;
        }

        // 左边界
        for (int y = gridY; y < gridY + gridSize && y < visualImage.Height; y++)
        {
            for (int t = 0; t < 3 && gridX + t < visualImage.Width; t++)
                visualImage[gridX + t, y] = red;
        }

        // 右边界
        for (int y = gridY; y < gridY + gridSize && y < visualImage.Height; y++)
        {
            for (int t = 0; t < 3 && gridX + gridSize - 1 - t >= 0; t++)
                visualImage[gridX + gridSize - 1 - t, y] = red;
        }

        visualImage.SaveAsJpeg(outputPath);
        Console.WriteLine($"可视化图片已保存: {outputPath}");
    }
}
