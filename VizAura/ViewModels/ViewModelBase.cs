using CommunityToolkit.Mvvm.ComponentModel;

namespace VizAura.ViewModels;

public abstract class ViewModelBase : ObservableObject
{
    /// <summary>
    /// ViewModel 清理资源 (窗口关闭时调用)
    /// 子类可重写此方法来释放资源 (如取消订阅、释放 native 资源等)
    /// </summary>
    public virtual void OnWindowClosing()
    {
        // 默认空实现
    }
}
