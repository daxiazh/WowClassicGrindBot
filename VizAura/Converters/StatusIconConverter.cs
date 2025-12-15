using Avalonia.Data.Converters;
using System;
using System.Globalization;
using VizAura.Models;

namespace VizAura.Converters;

/// <summary>
/// 将 ValidationStatus 转换为图标字符
/// </summary>
public sealed class StatusIconConverter : IValueConverter
{
    /// <summary>
    /// 将 ValidationStatus 转换为图标字符
    /// </summary>
    /// <param name="value">ValidationStatus 值</param>
    /// <param name="targetType">目标类型</param>
    /// <param name="parameter">转换参数</param>
    /// <param name="culture">文化信息</param>
    /// <returns>图标字符串</returns>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ValidationStatus status)
        {
            return status switch
            {
                ValidationStatus.Pending => "⏳",
                ValidationStatus.InProgress => "🔄",
                ValidationStatus.Success => "✅",
                ValidationStatus.Failed => "❌",
                _ => "❓"
            };
        }

        return "❓";
    }

    /// <summary>
    /// 反向转换（不支持）
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
