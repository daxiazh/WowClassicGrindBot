using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace VizAura.Converters;

/// <summary>
/// 将布尔值转换为可用性图标 (true="🟢", false="🔴")
/// 优化: 使用 static readonly 常量避免重复字符串实例创建
/// </summary>
public sealed class BoolToUsableIconConverter : IValueConverter
{
    private static readonly string GreenIcon = "🟢";
    private static readonly string RedIcon = "🔴";
    private static readonly string GrayIcon = "⚪";

    /// <summary>
    /// 将布尔值转换为可用性图标
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>"🟢"或"🔴"</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? GreenIcon : RedIcon;
        }

        return GrayIcon;
    }

    /// <summary>
    /// 反向转换（不支持）
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
