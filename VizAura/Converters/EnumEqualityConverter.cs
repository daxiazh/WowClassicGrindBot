using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace VizAura.Converters;

/// <summary>
/// 枚举相等性转换器，用于比较枚举值是否相等
/// </summary>
public sealed class EnumEqualityConverter : IValueConverter
{
    /// <summary>
    /// 比较枚举值是否与参数相等
    /// </summary>
    /// <param name="value">枚举值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">比较参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>是否相等</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        return value.Equals(parameter);
    }

    /// <summary>
    /// 反向转换（不支持）
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
