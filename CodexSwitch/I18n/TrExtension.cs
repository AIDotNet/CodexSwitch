using Avalonia;
using Avalonia.Markup.Xaml;

namespace CodexSwitch.I18n;

public sealed class TrExtension : MarkupExtension
{
    public TrExtension()
    {
    }

    public TrExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new TranslationObservable(Key).ToBinding();
    }

    private sealed class TranslationObservable(string key) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            observer.OnNext(I18nService.Current.Translate(key));
            EventHandler handler = (_, _) => observer.OnNext(I18nService.Current.Translate(key));
            I18nService.Current.LanguageChanged += handler;
            return new ActionDisposable(() => I18nService.Current.LanguageChanged -= handler);
        }
    }

    private sealed class ActionDisposable(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
