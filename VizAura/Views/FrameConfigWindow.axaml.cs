using Avalonia.Controls;
using System;
using VizAura.ViewModels;

namespace VizAura.Views;

public partial class FrameConfigWindow : Window
{
    public FrameConfigWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        
        // 订阅 ViewModel 的 Bitmap 更新事件
        if (DataContext is FrameConfigViewModel vm)
        {
            vm.OnBitmapUpdated += OnBitmapUpdated;
        }
    }
    
    /// <summary>
    /// Bitmap 更新时,强制 Image 控件重绘
    /// </summary>
    private void OnBitmapUpdated()
    {
        FullScreenImage?.InvalidateVisual();
        AddonImage?.InvalidateVisual();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // 取消订阅事件
        if (DataContext is FrameConfigViewModel vm)
        {
            vm.OnBitmapUpdated -= OnBitmapUpdated;
            vm.Dispose();
        }
        
        base.OnClosing(e);
    }
}
