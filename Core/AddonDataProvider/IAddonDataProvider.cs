using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

using System;
using System.Runtime.CompilerServices;

namespace Core;

public interface IAddonDataProvider : IDisposable
{
    private static readonly Bgra32 firstColor = new(0, 0, 0, 255);

    void UpdateData();
    void InitFrames(DataFrame[] frames);

    int[] Data { get; }

    [SkipLocalsInit]
    static void InternalUpdate(Image<Bgra32> bd,
        ReadOnlySpan<DataFrame> frames, Span<int> output)
    {
        // 1. 首帧校验 (Frame[0] 必须为 0)
        ref readonly Bgra32 first = ref bd.DangerousGetPixelRowMemory(frames[0].Y)
            .Span[frames[0].X];

        if (!first.Equals(firstColor))
        {
            return;
        }

        // 2. 读取所有帧数据
        for (int i = 0; i < frames.Length; i++)
        {
            DataFrame frame = frames[i];

            ReadOnlySpan<Bgra32> row = bd.DangerousGetPixelRowMemory(frame.Y).Span;
            ref readonly Bgra32 pixel = ref row[frame.X];

            output[frame.Index] = pixel.B | (pixel.G << 8) | (pixel.R << 16);
        }

        // 3. CRC16 校验 (Frame[0..n-2] 的数据 vs Frame[n-1] 的 CRC)
        int receivedCRC = output[^1];
        int calculatedCRC = CalculateCRC16(output[..^1]);

        if (receivedCRC != calculatedCRC)
        {
            // CRC 校验失败,清空数据防止使用脏数据
            output.Clear();
            return;
        }
    }

    /// <summary>
    /// 计算 CRC16-CCITT 校验码 (公开方法供 AddonDataSnapshot 复用)
    /// </summary>
    /// <param name="data">待校验的数据</param>
    /// <returns>CRC16 校验码</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // ReSharper disable once InconsistentNaming
    public static int CalculateCRC16(ReadOnlySpan<int> data)
    {
        ushort crc = 0xFFFF;
        const ushort poly = 0x8005;

        foreach (int value in data)
        {
            // 将 24bit 值拆成 3 个字节进行 CRC 计算
            for (int shift = 16; shift >= 0; shift -= 8)
            {
                byte b = (byte)((value >> shift) & 0xFF);
                crc ^= (ushort)(b << 8);

                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ poly);
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
            }
        }

        return crc;
    }

    int GetInt(int index)
    {
        return Data[index];
    }

    float GetFixed(int index)
    {
        return Data[index] / 100000f;
    }

    string GetString(int index)
    {
        int color = GetInt(index);
        if ((uint)color > 999999)
            return string.Empty;

        Span<char> buffer = stackalloc char[3];
        int count = 0;

        int n1 = color / 10000;
        int n2 = color / 100 % 100;
        int n3 = color % 100;

        if (n1 > 0) buffer[count++] = (char)n1;
        if (n2 > 0) buffer[count++] = (char)n2;
        if (n3 > 0) buffer[count++] = (char)n3;

        return buffer[..count].ToString();
    }
}
