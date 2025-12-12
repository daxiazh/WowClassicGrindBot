using Microsoft.Extensions.Logging;

namespace VizAura.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> logger;

    public MainWindowViewModel(ILogger<MainWindowViewModel> logger)
    {
        this.logger = logger;
        
        logger.LogInformation("MainWindowViewModel initialized");
    }

    public string Greeting { get; } = "Welcome to VizAura!";
}
