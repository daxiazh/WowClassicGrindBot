using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using VizAura.Models;

namespace VizAura.Converters;

/// <summary>
/// 将 ValidationStatus 转换为边框颜色
/// </summary>
public sealed class StatusBorderBrushConverter : IValueConverter
{
    /// <summary>
    /// 将 ValidationStatus 转换为边框画刷
    /// </summary>
    /// <param name="value">ValidationStatus 值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>边框画刷</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status)
        {
            return status switch
            {
                ValidationStatus.InProgress => new SolidColorBrush(Color.Parse("#2196F3")),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }

        return new SolidColorBrush(Colors.Transparent);
    }

    /// <summary>
    /// 反向转换（不支持）
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
