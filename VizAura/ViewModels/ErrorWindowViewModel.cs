using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Text;

namespace VizAura.ViewModels;

/// <summary>
/// 错误窗口 ViewModel
/// </summary>
public sealed partial class ErrorWindowViewModel : ViewModelBase
{
    private readonly Exception exception;
    private readonly IClipboard? clipboard;

    /// <summary>
    /// 异常类型
    /// </summary>
    [ObservableProperty]
    private string exceptionType;

    /// <summary>
    /// 异常消息
    /// </summary>
    [ObservableProperty]
    private string message;

    /// <summary>
    /// 完整的异常信息(包含堆栈跟踪)
    /// </summary>
    [ObservableProperty]
    private string fullText;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="exception">异常对象</param>
    /// <param name="clipboard">剪贴板服务</param>
    public ErrorWindowViewModel(Exception exception, IClipboard? clipboard)
    {
        this.exception = exception;
        this.clipboard = clipboard;

        exceptionType = exception.GetType().Name;
        message = exception.Message;
        fullText = FormatException(exception);
    }

    /// <summary>
    /// 复制异常信息到剪贴板
    /// </summary>
    [RelayCommand]
    private async void CopyToClipboard()
    {
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(fullText);
        }
    }

    /// <summary>
    /// 格式化异常信息
    /// </summary>
    /// <param name="ex">异常对象</param>
    /// <returns>格式化后的文本</returns>
    private static string FormatException(Exception ex)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("=== 异常信息 ===");
        sb.AppendLine($"类型: {ex.GetType().FullName}");
        sb.AppendLine($"消息: {ex.Message}");
        sb.AppendLine();
        
        sb.AppendLine("=== 堆栈跟踪 ===");
        sb.AppendLine(ex.StackTrace ?? "(无堆栈跟踪)");
        sb.AppendLine();

        // 内部异常
        if (ex.InnerException != null)
        {
            sb.AppendLine("=== 内部异常 ===");
            sb.AppendLine(FormatInnerException(ex.InnerException, 1));
        }

        // 聚合异常特殊处理
        if (ex is AggregateException aggregateEx)
        {
            sb.AppendLine("=== 聚合异常详情 ===");
            int index = 1;
            foreach (var innerEx in aggregateEx.InnerExceptions)
            {
                sb.AppendLine($"[{index}] {innerEx.GetType().Name}: {innerEx.Message}");
                sb.AppendLine(innerEx.StackTrace ?? "(无堆栈跟踪)");
                sb.AppendLine();
                index++;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 格式化内部异常
    /// </summary>
    /// <param name="ex">内部异常</param>
    /// <param name="level">嵌套层级</param>
    /// <returns>格式化后的文本</returns>
    private static string FormatInnerException(Exception ex, int level)
    {
        var sb = new StringBuilder();
        var indent = new string(' ', level * 2);

        sb.AppendLine($"{indent}类型: {ex.GetType().FullName}");
        sb.AppendLine($"{indent}消息: {ex.Message}");
        sb.AppendLine($"{indent}堆栈:");
        
        var stackLines = (ex.StackTrace ?? "(无堆栈跟踪)").Split('\n');
        foreach (var line in stackLines)
        {
            sb.AppendLine($"{indent}  {line.TrimEnd()}");
        }

        if (ex.InnerException != null)
        {
            sb.AppendLine();
            sb.AppendLine($"{indent}内部异常:");
            sb.Append(FormatInnerException(ex.InnerException, level + 1));
        }

        return sb.ToString();
    }
}
