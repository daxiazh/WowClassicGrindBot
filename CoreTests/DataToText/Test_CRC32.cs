using System;
using Microsoft.Extensions.Logging;

namespace CoreTests.DataToText;

/// <summary>
/// CRC32 算法验证测试
/// 用于验证 C# 和 Lua 的 CRC32 实现是否一致
/// </summary>
public static class Test_CRC32
{
    private delegate uint CRC32Delegate(ReadOnlySpan<byte> data);
    private static CRC32Delegate? crc32Func;

    /// <summary>
    /// 使用反射访问私有的 CRC32 方法
    /// </summary>
    private static uint CalculateCRC32(byte[] data)
    {
        if (crc32Func == null)
        {
            var decoderType = typeof(Core.DataToText.DataToTextGridDecoder);
            var method = decoderType.GetMethod("CalculateCRC32", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            
            if (method == null)
                throw new Exception("找不到 CalculateCRC32 方法");
            
            crc32Func = (CRC32Delegate)Delegate.CreateDelegate(typeof(CRC32Delegate), method);
        }
        
        return crc32Func(data);
    }

    public static void RunAllTests(ILogger logger)
    {
        logger.LogInformation("=== CRC32 算法验证测试 (C#) ===");
        
        // 测试 1: 空字符串
        {
            byte[] data = Array.Empty<byte>();
            uint crc = CalculateCRC32(data);
            logger.LogInformation("Test 1 - Empty string: 0x{CRC:X8} (预期: 0x00000000)", crc);
            
            if (crc != 0x00000000)
                logger.LogWarning("  ⚠ 与预期值不符！");
        }

        // 测试 2: 标准测试向量 "123456789"
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes("123456789");
            uint crc = CalculateCRC32(data);
            logger.LogInformation("Test 2 - '123456789': 0x{CRC:X8} (预期: 0xCBF43926)", crc);
            
            if (crc != 0xCBF43926)
                logger.LogWarning("  ⚠ 与预期值不符！");
        }

        // 测试 3: "Hello World"
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes("Hello World");
            uint crc = CalculateCRC32(data);
            logger.LogInformation("Test 3 - 'Hello World': 0x{CRC:X8}", crc);
        }

        // 测试 4: 二进制数据 0x01, 0x02, 0x03, 0x04, 0x05
        {
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };
            uint crc = CalculateCRC32(data);
            logger.LogInformation("Test 4 - Binary [01 02 03 04 05]: 0x{CRC:X8}", crc);
        }

        // 测试 5: 只有元数据 Version=1, FieldCount=108, Reserved=0,0
        {
            byte[] data = new byte[] { 0x01, 0x6C, 0x00, 0x00 };
            uint crc = CalculateCRC32(data);
            logger.LogInformation("Test 5 - Metadata [01 6C 00 00]: 0x{CRC:X8}", crc);
        }

        // 测试 6: 328 字节全 0
        {
            byte[] data = new byte[328];
            uint crc = CalculateCRC32(data);
            logger.LogInformation("Test 6 - 328 bytes of zeros: 0x{CRC:X8}", crc);
        }

        // 测试 7: 顺序字节 0, 1, 2, ..., 255, 0, 1, ...
        {
            byte[] data = new byte[328];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)(i % 256);
            }
            uint crc = CalculateCRC32(data);
            logger.LogInformation("Test 7 - Sequential 328 bytes: 0x{CRC:X8}", crc);
        }

        logger.LogInformation("=== 测试完成 ===");
        logger.LogInformation("请在游戏中输入 /testcrc32 运行 Lua 测试并对比结果");
    }
}
