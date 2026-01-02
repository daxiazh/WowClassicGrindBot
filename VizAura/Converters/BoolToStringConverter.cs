using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace VizAura.Converters;

/// <summary>
/// 将布尔值转换为字符串 (true="已启用", false="已禁用")
/// 优化: 使用 static readonly 常量避免重复字符串实例创建
/// </summary>
public sealed class BoolToStringConverter : IValueConverter
{
    private static readonly string EnabledText = "已启用";
    private static readonly string DisabledText = "已禁用";
    private static readonly string UnknownText = "未知";

    /// <summary>
    /// 将布尔值转换为字符串
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>"已启用"或"已禁用"</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? EnabledText : DisabledText;
        }

        return UnknownText;
    }

    /// <summary>
    /// 反向转换（不支持）
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            return stringValue == EnabledText;
        }
    
        return false;
    }
}
