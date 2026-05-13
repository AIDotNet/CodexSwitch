using System.ComponentModel;
using System.Globalization;
using Avalonia.Platform;

namespace CodexSwitch.I18n;

public sealed class I18nService : INotifyPropertyChanged
{
    public const string CatalogResourceUri = "avares://CodexSwitch/Assets/i18n/languages.json";

    private readonly I18nCatalog _catalog;
    private readonly Dictionary<string, I18nLanguageOption> _languages;
    private readonly Dictionary<string, Dictionary<string, string>> _translations;

    public static I18nService Current { get; } = LoadDefault();

    public I18nService(I18nCatalog catalog, IEnumerable<I18nLanguageResource> resources)
    {
        _catalog = catalog;
        _languages = catalog.Languages
            .Where(language => !string.IsNullOrWhiteSpace(language.Code))
            .GroupBy(language => language.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _translations = resources
            .Where(resource => !string.IsNullOrWhiteSpace(resource.Code))
            .GroupBy(resource => resource.Code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => new Dictionary<string, string>(group.First().Translations, StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);

        DefaultLanguageCode = ResolveLanguageCode(catalog.DefaultLanguage);
        CurrentLanguageCode = DefaultLanguageCode;
        Languages = catalog.Languages
            .Where(language => !string.IsNullOrWhiteSpace(language.Code) && _languages.ContainsKey(language.Code.Trim()))
            .Select(language => _languages[language.Code.Trim()])
            .ToArray();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    public IReadOnlyList<I18nLanguageOption> Languages { get; }

    public string DefaultLanguageCode { get; }

    public string CurrentLanguageCode { get; private set; }

    public I18nLanguageOption CurrentLanguage => GetLanguage(CurrentLanguageCode);

    public string this[string key] => Translate(key);

    public static I18nService LoadDefault()
    {
        var catalog = LoadCatalog();
        var resources = catalog.Languages.Select(language => LoadLanguageResource(language.Code)).ToArray();
        return new I18nService(catalog, resources);
    }

    public static I18nCatalog LoadCatalog()
    {
        using var stream = AssetLoader.Open(new Uri(CatalogResourceUri));
        return DeserializeCatalog(stream);
    }

    public static I18nLanguageResource LoadLanguageResource(string code)
    {
        var resourceUri = $"avares://CodexSwitch/Assets/i18n/{code}.json";
        using var stream = AssetLoader.Open(new Uri(resourceUri));
        return DeserializeLanguageResource(stream, code);
    }

    public static I18nCatalog DeserializeCatalog(Stream stream)
    {
        return JsonSerializer.Deserialize(stream, CodexSwitchJsonContext.Default.I18nCatalog)
            ?? new I18nCatalog();
    }

    public static I18nLanguageResource DeserializeLanguageResource(Stream stream, string code = "")
    {
        return JsonSerializer.Deserialize(stream, CodexSwitchJsonContext.Default.I18nLanguageResource)
            ?? new I18nLanguageResource { Code = code };
    }

    public bool SetLanguage(string? code)
    {
        var resolved = ResolveLanguageCode(code);
        if (string.Equals(CurrentLanguageCode, resolved, StringComparison.OrdinalIgnoreCase))
            return false;

        CurrentLanguageCode = resolved;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguageCode)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public I18nLanguageOption GetLanguage(string? code)
    {
        var resolved = ResolveLanguageCode(code);
        return _languages.TryGetValue(resolved, out var language)
            ? language
            : _languages.Values.FirstOrDefault() ?? new I18nLanguageOption
            {
                Code = DefaultLanguageCode,
                DisplayName = DefaultLanguageCode,
                NativeName = DefaultLanguageCode
            };
    }

    public string Translate(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";

        if (TryTranslate(CurrentLanguageCode, key, out var current))
            return current;

        return TryTranslate(DefaultLanguageCode, key, out var fallback)
            ? fallback
            : key;
    }

    public string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Translate(key), args);
    }

    private bool TryTranslate(string languageCode, string key, out string value)
    {
        value = "";
        return _translations.TryGetValue(languageCode, out var table) &&
            table.TryGetValue(key, out value!);
    }

    private string ResolveLanguageCode(string? code)
    {
        if (!string.IsNullOrWhiteSpace(code) && _languages.ContainsKey(code.Trim()))
            return code.Trim();

        if (!string.IsNullOrWhiteSpace(_catalog.DefaultLanguage) &&
            _languages.ContainsKey(_catalog.DefaultLanguage.Trim()))
            return _catalog.DefaultLanguage.Trim();

        return _languages.Keys.FirstOrDefault() ?? "zh-CN";
    }
}
