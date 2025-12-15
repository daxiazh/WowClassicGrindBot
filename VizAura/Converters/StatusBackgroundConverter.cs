using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;
using VizAura.Models;

namespace VizAura.Converters;

/// <summary>
/// 将 ValidationStatus 转换为背景色
/// </summary>
public sealed class StatusBackgroundConverter : IValueConverter
{
    /// <summary>
    /// 将 ValidationStatus 转换为背景画刷
    /// </summary>
    /// <param name="value">ValidationStatus 值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>背景画刷</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status)
        {
            return status switch
            {
                ValidationStatus.InProgress => new SolidColorBrush(Color.Parse("#E3F2FD")),
                ValidationStatus.Success => new SolidColorBrush(Color.Parse("#F5F5F5")),
                ValidationStatus.Failed => new SolidColorBrush(Color.Parse("#F5F5F5")),
                ValidationStatus.Pending => new SolidColorBrush(Color.Parse("#F5F5F5")),
                _ => new SolidColorBrush(Color.Parse("#F5F5F5"))
            };
        }

        return new SolidColorBrush(Color.Parse("#F5F5F5"));
    }

    /// <summary>
    /// 反向转换（不支持）
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
