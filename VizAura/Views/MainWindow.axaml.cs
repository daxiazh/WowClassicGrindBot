using Avalonia.Controls;
using System;
using System.Threading.Tasks;
using VizAura.ViewModels;

namespace VizAura.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 窗口关闭时退出应用
    /// 说明: 跳过手动清理,由操作系统回收所有资源 (内存/文件句柄/native 资源等)
    /// </summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // 延迟 50ms 让窗口关闭动画完成,然后强制退出进程
        // 操作系统会自动回收所有资源,包括:
        //   - ScreenCaptureKit native 资源
        //   - WowScreenMacOS 托管内存
        //   - DI Scope 中的所有服务
        _ = Task.Delay(50).ContinueWith(_ => Environment.Exit(0));
        
        base.OnClosing(e);
    }
}