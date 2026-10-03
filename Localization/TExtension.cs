using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace LithophaneGUI.Localization;

/// <summary>XAML: <c>Text="{l:T Model.Thickness}"</c> binds the property to a translated string.</summary>
public sealed class TExtension : MarkupExtension
{
    public TExtension(string key) => Key = key;

    public string Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Bind(Key);
}
