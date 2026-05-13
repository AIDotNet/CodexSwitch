using CodexSwitch.I18n;

namespace CodexSwitch.Tests;

public sealed class I18nServiceTests
{
    [Fact]
    public void BuiltInCatalog_DeserializesLanguageManifestAndLanguageFiles()
    {
        var catalog = ReadCatalog();

        Assert.Equal("zh-CN", catalog.DefaultLanguage);
        Assert.Collection(
            catalog.Languages,
            language => Assert.Equal("zh-CN", language.Code),
            language => Assert.Equal("en-US", language.Code),
            language => Assert.Equal("ja-JP", language.Code));

        foreach (var language in catalog.Languages)
        {
            var resource = ReadLanguageResource(language.Code);
            Assert.Equal(language.Code, resource.Code);
            Assert.True(resource.Translations.Count > 0);
            Assert.True(resource.Translations.ContainsKey("settings.title"));
        }
    }

    [Fact]
    public void LoadDefault_ExposesLanguageOptionsFromManifest()
    {
        var catalog = ReadCatalog();
        var resources = catalog.Languages.Select(language => ReadLanguageResource(language.Code));
        var service = new I18nService(catalog, resources);

        Assert.Equal("zh-CN", service.DefaultLanguageCode);
        Assert.Collection(
            service.Languages,
            language =>
            {
                Assert.Equal("zh-CN", language.Code);
                Assert.Equal("中文", language.NativeName);
            },
            language =>
            {
                Assert.Equal("en-US", language.Code);
                Assert.Equal("English", language.NativeName);
            },
            language =>
            {
                Assert.Equal("ja-JP", language.Code);
                Assert.Equal("日本語", language.NativeName);
            });
    }

    [Fact]
    public void Translate_UsesCurrentLanguageDefaultFallbackAndKeyFallback()
    {
        var service = CreateTestService();

        Assert.Equal("设置", service.Translate("settings.title"));

        Assert.True(service.SetLanguage("en-US"));
        Assert.Equal("Settings", service.Translate("settings.title"));
        Assert.Equal("Only default", service.Translate("fallback.onlyDefault"));
        Assert.Equal("missing.key", service.Translate("missing.key"));
    }

    [Fact]
    public void SetLanguage_RejectsInvalidCodeByFallingBackToDefault()
    {
        var service = CreateTestService();

        service.SetLanguage("en-US");
        Assert.Equal("en-US", service.CurrentLanguageCode);

        Assert.True(service.SetLanguage("missing"));
        Assert.Equal("zh-CN", service.CurrentLanguageCode);
        Assert.Equal("设置", service.Translate("settings.title"));
    }

    private static I18nService CreateTestService()
    {
        var catalog = new I18nCatalog
        {
            DefaultLanguage = "zh-CN",
            Languages =
            {
                new I18nLanguageOption { Code = "zh-CN", DisplayName = "Chinese", NativeName = "中文" },
                new I18nLanguageOption { Code = "en-US", DisplayName = "English", NativeName = "English" }
            }
        };
        var resources = new[]
        {
            new I18nLanguageResource
            {
                Code = "zh-CN",
                Translations =
                {
                    ["settings.title"] = "设置",
                    ["fallback.onlyDefault"] = "Only default"
                }
            },
            new I18nLanguageResource
            {
                Code = "en-US",
                Translations =
                {
                    ["settings.title"] = "Settings"
                }
            }
        };

        return new I18nService(catalog, resources);
    }

    private static I18nCatalog ReadCatalog()
    {
        using var stream = File.OpenRead(Path.Combine(I18nDirectory, "languages.json"));
        return I18nService.DeserializeCatalog(stream);
    }

    private static I18nLanguageResource ReadLanguageResource(string code)
    {
        using var stream = File.OpenRead(Path.Combine(I18nDirectory, code + ".json"));
        return I18nService.DeserializeLanguageResource(stream, code);
    }

    private static string I18nDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "i18n");
}
