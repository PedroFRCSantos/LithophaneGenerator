using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LithophaneGUI.Localization;

namespace LithophaneGUI;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // The language must be known before the window builds its bindings.
        Loc.Current.Initialize(AppState.Load()?.Language);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(desktop.Args ?? Array.Empty<string>());
        base.OnFrameworkInitializationCompleted();
    }
}
