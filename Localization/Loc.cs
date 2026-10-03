using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Platform;

namespace LithophaneGUI.Localization;

public sealed record LanguageInfo(string Code, string NativeName);

/// <summary>
/// UI strings. One flat JSON table per language in <c>Assets/Lang</c>; English is the fallback for any
/// missing key. The language follows the OS unless the user picked one (<see cref="Override"/>).
/// Numbers are formatted with the OS culture regardless of the UI language.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static readonly Loc Current = new();

    public static readonly IReadOnlyList<LanguageInfo> Languages = new[]
    {
        new LanguageInfo("en", "English"),
        new LanguageInfo("zh", "中文"),
        new LanguageInfo("hi", "हिन्दी"),
        new LanguageInfo("es", "Español"),
        new LanguageInfo("ar", "العربية"),
        new LanguageInfo("fr", "Français"),
        new LanguageInfo("bn", "বাংলা"),
        new LanguageInfo("pt", "Português"),
        new LanguageInfo("ru", "Русский"),
        new LanguageInfo("ur", "اردو"),
    };

    private static readonly HashSet<string> RightToLeft = new() { "ar", "ur" };
    private static readonly HashSet<string> LargeScripts = new() { "zh", "hi", "bn", "ar", "ur" };

    private readonly Dictionary<string, Dictionary<string, string>> _tables = new();
    private Dictionary<string, string> _english = new();
    private Dictionary<string, string> _table = new();
    private string _language = "en";
    private string? _override;

    private Loc()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    /// <summary>Loads the tables and picks the language. Call once Avalonia is up (assets need it).</summary>
    public void Initialize(string? overrideCode)
    {
        _english = Load("en");
        _override = IsSupported(overrideCode) ? overrideCode : null;
        Apply(_override ?? Detect(), notify: false);
    }

    /// <summary>Language chosen by the user, or null to follow the OS.</summary>
    public string? Override
    {
        get => _override;
        set
        {
            value = IsSupported(value) ? value : null;
            if (value == _override) return;
            _override = value;
            Apply(value ?? Detect(), notify: true);
        }
    }

    /// <summary>The language in use (ISO 639-1 code).</summary>
    public string Language => _language;

    public bool IsRightToLeft => RightToLeft.Contains(_language);

    /// <summary>Scripts with tall or dense glyphs need a little more size to stay legible in the sketches.</summary>
    public double SketchFontScale => LargeScripts.Contains(_language) ? 1.15 : 1.0;

    public CultureInfo Culture
    {
        get
        {
            try { return CultureInfo.GetCultureInfo(_language); }
            catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
        }
    }

    public string this[string key] =>
        _table.TryGetValue(key, out var s) || _english.TryGetValue(key, out s) ? s : key;

    public bool Has(string key) => _english.ContainsKey(key);

    /// <summary>Formatted string; numbers use the OS culture. Falls back to English, then to the key.</summary>
    public static string Get(string key, params object[] args)
    {
        var loc = Current;
        if (args.Length == 0) return loc[key];
        if (loc._table.TryGetValue(key, out var s) && TryFormat(s, args, out var r)) return r;
        if (loc._english.TryGetValue(key, out s) && TryFormat(s, args, out r)) return r;
        return key;
    }

    /// <summary>A one-way binding to the string for <paramref name="key"/> that follows language changes.</summary>
    public static IBinding Bind(string key) => new Binding(nameof(Language))
    {
        Source = Current,
        Mode = BindingMode.OneWay,
        Converter = LocConverter.Instance,
        ConverterParameter = key,
    };

    public static string Detect()
    {
        string two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (two == "zh" || CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)) return "zh";
        return IsSupported(two) ? two : "en";
    }

    private static bool IsSupported(string? code) => code != null && Languages.Any(l => l.Code == code);

    private void Apply(string code, bool notify)
    {
        if (!_tables.TryGetValue(code, out var table))
            _tables[code] = table = code == "en" ? _english : Load(code);
        _language = code;
        _table = table;
        if (!notify) return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Dictionary<string, string> Load(string code)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://LithophaneGUI/Assets/Lang/{code}.json"));
            return JsonSerializer.Deserialize<Dictionary<string, string>>(stream, new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            }) ?? new Dictionary<string, string>();
        }
        catch (Exception)
        {
            return new Dictionary<string, string>();
        }
    }

    private static bool TryFormat(string format, object[] args, out string result)
    {
        try
        {
            result = string.Format(CultureInfo.CurrentCulture, format, args);
            return true;
        }
        catch (FormatException)
        {
            result = "";
            return false;
        }
    }
}

/// <summary>Turns the bound <see cref="Loc.Language"/> into the string for the key given as parameter.</summary>
public sealed class LocConverter : IValueConverter
{
    public static readonly LocConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        parameter is string key ? Loc.Current[key] : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
