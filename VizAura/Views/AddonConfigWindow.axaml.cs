using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VizAura.Views;

/// <summary>
/// AddOns 配置窗口
/// </summary>
public partial class AddonConfigWindow : Window
{
    public AddonConfigWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 关闭按钮点击事件
    /// </summary>
    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
