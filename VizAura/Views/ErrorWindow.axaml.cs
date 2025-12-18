using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VizAura.Views;

/// <summary>
/// 错误窗口
/// </summary>
public partial class ErrorWindow : Window
{
    public ErrorWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    /// <param name="sender">事件源</param>
    /// <param name="e">事件参数</param>
    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
