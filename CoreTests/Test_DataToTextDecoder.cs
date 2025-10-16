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
    private readonly ILogger<DataToTextGridDecoder> decoderLogger;
    private readonly WowScreenDXGI screen;
    private readonly DataToTextGridDecoder decoder;

    private long totalDecodeTime;
    private int successCount;
    private int failureCount;

    public Test_DataToTextDecoder(
        Microsoft.Extensions.Logging.ILogger logger,
        ILogger<DataToTextGridDecoder> decoderLogger,
        WowScreenDXGI screen)
    {
        this.logger = logger;
        this.decoderLogger = decoderLogger;
        this.screen = screen;
        this.decoder = new DataToTextGridDecoder(decoderLogger);

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
        string imagePath = "datatotext_debug.jpg",
        ILogger<DataToTextGridDecoder>? decoderLogger = null)
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

        // 创建解码器（默认使用 NullLogger，可由调用方指定）
        decoderLogger ??= Microsoft.Extensions.Logging.Abstractions.NullLogger<DataToTextGridDecoder>.Instance;
        var decoder = new DataToTextGridDecoder(decoderLogger);

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
        }
    }
}
