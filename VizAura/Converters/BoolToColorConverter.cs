using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace VizAura.Converters;

/// <summary>
/// 将布尔值转换为颜色 (true=绿色, false=红色)
/// </summary>
public sealed class BoolToColorConverter : IValueConverter
{
    /// <summary>
    /// 将布尔值转换为颜色
    /// </summary>
    /// <param name="value">布尔值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>绿色或红色</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Color.FromRgb(0, 200, 83) : Color.FromRgb(244, 67, 54);
        }

        return Color.FromRgb(158, 158, 158); // 灰色作为默认值
    }

    /// <summary>
    /// 反向转换（不支持）
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
