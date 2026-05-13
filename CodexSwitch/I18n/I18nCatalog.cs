namespace CodexSwitch.I18n;

public sealed class I18nCatalog
{
    public string DefaultLanguage { get; set; } = "zh-CN";

    public Collection<I18nLanguageOption> Languages { get; set; } = [];
}

public sealed class I18nLanguageOption
{
    public string Code { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string NativeName { get; set; } = "";

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(NativeName))
            return NativeName;

        if (!string.IsNullOrWhiteSpace(DisplayName))
            return DisplayName;

        return Code;
    }
}

public sealed class I18nLanguageResource
{
    public string Code { get; set; } = "";

    public Dictionary<string, string> Translations { get; set; } = [];
}
