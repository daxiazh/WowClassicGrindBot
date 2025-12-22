using Avalonia.Controls;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using VizAura.ViewModels;

namespace VizAura.Views;

public partial class MainWindow : Window
{
    private ListBox? logListBox;

    public MainWindow()
    {
        InitializeComponent();
        
        // 订阅 DataContext 变化
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// DataContext 变化时订阅日志集合变化事件
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            // 订阅日志集合变化事件
            vm.LogMessages.CollectionChanged += OnLogMessagesChanged;
        }
    }

    /// <summary>
    /// 日志集合变化时自动滚动到底部
    /// </summary>
    private void OnLogMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.AutoScrollLog)
            return;

        // 延迟滚动,确保 UI 已更新
        _ = Task.Delay(50).ContinueWith(_ =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // 懒加载 ListBox
                logListBox ??= this.FindControl<ListBox>("LogListBox");
                
                if (logListBox != null && vm.LogMessages.Count > 0)
                {
                    // 滚动到最后一项
                    logListBox.ScrollIntoView(vm.LogMessages.Count - 1);
                }
            });
        });
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