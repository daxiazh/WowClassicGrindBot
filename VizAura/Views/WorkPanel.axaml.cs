using Avalonia.Controls;
using Avalonia.Input;
using VizAura.ViewModels;

namespace VizAura.Views;

public partial class WorkPanel : UserControl
{
    public WorkPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 技能 1 点击事件
    /// </summary>
    private void OnSpell1Click(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is WorkViewModel vm)
        {
            vm.SendSpell1KeybindCommand.Execute(null);
        }
    }

    /// <summary>
    /// 技能 2 点击事件
    /// </summary>
    private void OnSpell2Click(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is WorkViewModel vm)
        {
            vm.SendSpell2KeybindCommand.Execute(null);
        }
    }
}
