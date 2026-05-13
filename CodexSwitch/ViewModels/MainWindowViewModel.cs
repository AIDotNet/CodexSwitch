using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodexSwitch.I18n;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;
using Lucide.Avalonia;

namespace CodexSwitch.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly AppPaths _paths;
    private readonly ConfigurationStore _store;
    private readonly PriceCalculator _priceCalculator;
    private readonly UsageMeter _usageMeter;
    private readonly UsageLogWriter _usageLogWriter;
    private readonly UsageLogReader _usageLogReader;
    private readonly CodexConfigWriter _codexConfigWriter;
    private readonly I18nService _i18n;
    private readonly HttpClient _sharedHttpClient;
    private readonly IconCacheService _iconCacheService;
    private readonly ProviderAuthService _providerAuthService;
    private readonly CodexOAuthLoginService _codexOAuthLoginService;
    private readonly ProxyHostService _proxyHostService;
    private readonly UpdateCheckService _updateCheckService;
    private AppConfig _config;
    private ModelPricingCatalog _pricing;
    private string _returnPage = "Providers";
    private string? _editingProviderId;
    private string? _editingModelId;
    private ModelCatalogItem? _modelPendingDelete;
    private string? _providerPendingDeleteId;
    private bool _isRefreshingSettingsFields;
    private bool _isLoadingProviderFields;

    [ObservableProperty]
    private string _currentPage = "Providers";

    [ObservableProperty]
    private string _settingsTab = "General";

    [ObservableProperty]
    private string _usageTab = "Requests";

    [ObservableProperty]
    private UsageTimeRange _usageTimeRange = UsageTimeRange.Last24Hours;

    [ObservableProperty]
    private bool _isUsageRefreshing;

    [ObservableProperty]
    private ClientAppKind _selectedClientApp = ClientAppKind.Codex;

    [ObservableProperty]
    private string _endpoint = "";

    [ObservableProperty]
    private string _proxyStatus = "Starting";

    [ObservableProperty]
    private string _activeProviderId = "";

    [ObservableProperty]
    private string _selectedProviderId = "";

    [ObservableProperty]
    private string _selectedProviderName = "";

    [ObservableProperty]
    private string _selectedProviderNote = "";

    [ObservableProperty]
    private string _selectedProviderWebsite = "";

    [ObservableProperty]
    private string _selectedBaseUrl = "";

    [ObservableProperty]
    private string _selectedDefaultModel = "";

    [ObservableProperty]
    private string _selectedApiKey = "";

    [ObservableProperty]
    private ProviderProtocol _selectedProtocol = ProviderProtocol.OpenAiResponses;

    [ObservableProperty]
    private bool _selectedFastMode;

    [ObservableProperty]
    private bool _selectedOverrideModel;

    [ObservableProperty]
    private string _selectedServiceTier = "";

    [ObservableProperty]
    private long _requestCount;

    [ObservableProperty]
    private long _errorCount;

    [ObservableProperty]
    private long _inputTokens;

    [ObservableProperty]
    private long _cachedInputTokens;

    [ObservableProperty]
    private long _cacheCreationInputTokens;

    [ObservableProperty]
    private long _outputTokens;

    [ObservableProperty]
    private long _reasoningOutputTokens;

    [ObservableProperty]
    private decimal _estimatedCost;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _proxyListenHost = "127.0.0.1";

    [ObservableProperty]
    private int _proxyPort = 12785;

    [ObservableProperty]
    private string _inboundApiKey = "";

    [ObservableProperty]
    private bool _proxyEnabled = true;

    [ObservableProperty]
    private long _billingUnitTokens = 1_000_000;

    [ObservableProperty]
    private decimal _defaultFastMultiplier = 2m;

    [ObservableProperty]
    private decimal _gpt55FastMultiplier = 2.5m;

    [ObservableProperty]
    private string _pricingCurrency = "USD";

    [ObservableProperty]
    private string _uiLanguage = "zh-CN";

    [ObservableProperty]
    private I18nLanguageOption? _selectedLanguage;

    [ObservableProperty]
    private string _uiTheme = "system";

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _defaultClientAppIsCodex = true;

    [ObservableProperty]
    private bool _isProviderDialogOpen;

    [ObservableProperty]
    private bool _isModelDialogOpen;

    [ObservableProperty]
    private bool _isDeleteModelDialogOpen;

    [ObservableProperty]
    private bool _isDeleteProviderDialogOpen;

    [ObservableProperty]
    private string _providerPendingDeleteName = "";

    [ObservableProperty]
    private string _selectedProviderTemplateId = ProviderTemplateCatalog.CustomTemplateId;

    [ObservableProperty]
    private string _providerDialogTitle = "";

    [ObservableProperty]
    private string _modelDialogTitle = "";

    [ObservableProperty]
    private string _modelEditorId = "";

    [ObservableProperty]
    private string _modelEditorDisplayName = "";

    [ObservableProperty]
    private string _modelEditorAliases = "";

    [ObservableProperty]
    private string _modelEditorIconSlug = "";

    [ObservableProperty]
    private long? _modelEditorInputTierLimit;

    [ObservableProperty]
    private decimal _modelEditorInputPrice;

    [ObservableProperty]
    private decimal _modelEditorInputOverflowPrice;

    [ObservableProperty]
    private decimal _modelEditorCachedInputPrice;

    [ObservableProperty]
    private decimal _modelEditorCacheCreationInputPrice;

    [ObservableProperty]
    private long? _modelEditorOutputTierLimit;

    [ObservableProperty]
    private decimal _modelEditorOutputPrice;

    [ObservableProperty]
    private decimal _modelEditorOutputOverflowPrice;

    [ObservableProperty]
    private string _modelEditorFastMultiplierOverride = "";

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string _currentVersionTag = AppReleaseInfo.CurrentVersionTag;

    [ObservableProperty]
    private string _latestVersionTag = "";

    [ObservableProperty]
    private string _latestReleasePublishedAtText = "";

    [ObservableProperty]
    private string _updateStatusDetails = "";

    [ObservableProperty]
    private string _latestReleaseUrl = AppReleaseInfo.ReleasesUrl;

    public MainWindowViewModel()
    {
        _paths = new AppPaths();
        _store = new ConfigurationStore(_paths);
        _config = _store.LoadConfig();
        _i18n = I18nService.Current;
        _i18n.SetLanguage(_config.Ui.Language);
        _i18n.LanguageChanged += (_, _) => RefreshLocalizedText();
        AppThemeService.Apply(_config.Ui.Theme);
        _pricing = _store.LoadPricing();
        _priceCalculator = new PriceCalculator(_pricing);
        _usageMeter = new UsageMeter(_priceCalculator);
        _usageLogWriter = new UsageLogWriter(_paths);
        _usageLogReader = new UsageLogReader(_paths);
        _codexConfigWriter = new CodexConfigWriter(_paths);

        _sharedHttpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        });
        _iconCacheService = new IconCacheService(_paths, _sharedHttpClient);
        _providerAuthService = new ProviderAuthService(_store, _config, _sharedHttpClient);
        _codexOAuthLoginService = new CodexOAuthLoginService(_sharedHttpClient);
        _updateCheckService = new UpdateCheckService(_sharedHttpClient);
        ProxyStatus = T("proxy.starting");
        LatestVersionTag = T("update.noReleaseYet");
        LatestReleasePublishedAtText = T("update.notPublished");
        UpdateStatusDetails = T("update.checking");
        _proxyHostService = new ProxyHostService(
            _usageMeter,
            _priceCalculator,
            _usageLogWriter,
            _codexConfigWriter,
            _providerAuthService,
            [
                new OpenAiResponsesAdapter(_sharedHttpClient),
                new OpenAiChatAdapter(),
                new AnthropicMessagesAdapter()
            ]);

        ClientApps = [];
        ProviderTemplates = [];
        ProviderRows = [];
        ModelRows = [];
        PricingRows = [];
        ModelCatalogRows = [];
        UsageMetrics = [];
        UsageLogRows = [];
        ProviderUsageRows = [];
        ModelUsageRows = [];
        TrendPoints = [];
        ProtocolOptions = Enum.GetValues<ProviderProtocol>();

        SelectClientAppCommand = new RelayCommand<ClientAppItem>(SelectClientApp);
        ShowProvidersCommand = new RelayCommand(() => CurrentPage = "Providers");
        ShowUsageCommand = new RelayCommand(() => CurrentPage = "Usage");
        ShowModelsCommand = new RelayCommand(() => CurrentPage = "Models");
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        BackFromSettingsCommand = new RelayCommand(BackFromSettings);
        SelectSettingsTabCommand = new RelayCommand<string>(tab => SettingsTab = tab ?? "General");
        SelectUsageTabCommand = new RelayCommand<string>(tab => UsageTab = tab ?? "Requests");
        SelectUsageRangeCommand = new RelayCommand<string>(SelectUsageRange);
        SelectThemeCommand = new RelayCommand<string>(SelectTheme);
        ToggleProxyCommand = new AsyncRelayCommand(ToggleProxyAsync);
        RestartProxyCommand = new AsyncRelayCommand(RestartProxyAsync);
        StopProxyCommand = new AsyncRelayCommand(StopProxyAsync);
        SelectProviderCommand = new RelayCommand<ProviderListItem>(row => _ = ActivateProviderAsync(row));
        EditProviderCommand = new RelayCommand<ProviderListItem>(OpenEditProvider);
        AddProviderCommand = new RelayCommand(OpenAddProvider);
        SelectProviderTemplateCommand = new RelayCommand<ProviderTemplateItem>(SelectProviderTemplate);
        LoginCodexOAuthCommand = new AsyncRelayCommand(LoginCodexOAuthAsync);
        RequestRemoveProviderCommand = new RelayCommand<ProviderListItem>(RequestRemoveProvider);
        CancelRemoveProviderCommand = new RelayCommand(() => IsDeleteProviderDialogOpen = false);
        ConfirmRemoveProviderCommand = new AsyncRelayCommand(ConfirmRemoveProviderAsync);
        SelectOAuthAccountCommand = new RelayCommand<OAuthAccountListItem>(SelectOAuthAccount);
        RemoveOAuthAccountCommand = new RelayCommand<OAuthAccountListItem>(RemoveOAuthAccount);
        SaveOAuthAccountNameCommand = new RelayCommand<OAuthAccountListItem>(SaveOAuthAccountName);
        CloseProviderDialogCommand = new RelayCommand(() => IsProviderDialogOpen = false);
        SaveProviderCommand = new AsyncRelayCommand(SaveProviderDialogAsync);
        AddProviderModelCommand = new RelayCommand(AddProviderModel);
        RemoveProviderModelCommand = new RelayCommand<ModelEditorItem>(RemoveProviderModel);
        AddPricingModelCommand = new RelayCommand(OpenAddModel);
        EditPricingModelCommand = new RelayCommand<ModelCatalogItem>(OpenEditModel);
        RequestRemovePricingModelCommand = new RelayCommand<ModelCatalogItem>(RequestRemovePricingModel);
        CancelRemovePricingModelCommand = new RelayCommand(() => IsDeleteModelDialogOpen = false);
        ConfirmRemovePricingModelCommand = new RelayCommand(ConfirmRemovePricingModel);
        CloseModelDialogCommand = new RelayCommand(() => IsModelDialogOpen = false);
        SaveModelCommand = new RelayCommand(SaveModelDialog);
        ApplyCommand = new AsyncRelayCommand(ApplyAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        RefreshUsageCommand = new AsyncRelayCommand(RefreshUsageDashboardAsync);
        CheckForUpdatesCommand = new AsyncRelayCommand(() => CheckForUpdatesAsync(false));
        OpenLatestReleaseCommand = new RelayCommand(OpenLatestRelease);

        _usageMeter.Changed += (_, snapshot) => ApplySnapshot(snapshot);
        _proxyHostService.StateChanged += (_, state) => ApplyProxyState(state);

        RefreshProviderTemplates();
        RefreshClientApps();
        RefreshProviderRows();
        RefreshSettingsFields();
        RefreshPricingRows();
        RefreshModelCatalogRows();
        RefreshUsageDashboard();
        SelectProvider(ProviderRows.FirstOrDefault(row => row.IsActive) ?? ProviderRows.FirstOrDefault());
        _ = EnsureIconsAsync();
        _ = CheckForUpdatesAsync(true);
        _ = _config.Proxy.Enabled
            ? RestartProxyAsync()
            : _proxyHostService.StartAsync(_config);
    }

    public ObservableCollection<ClientAppItem> ClientApps { get; }

    public ObservableCollection<ProviderTemplateItem> ProviderTemplates { get; }

    public ObservableCollection<ProviderListItem> ProviderRows { get; }

    public ObservableCollection<ModelEditorItem> ModelRows { get; }

    public ObservableCollection<ModelPricingEditorItem> PricingRows { get; }

    public ObservableCollection<ModelCatalogItem> ModelCatalogRows { get; }

    public ObservableCollection<UsageMetricItem> UsageMetrics { get; }

    public ObservableCollection<UsageLogItem> UsageLogRows { get; }

    public ObservableCollection<ProviderUsageItem> ProviderUsageRows { get; }

    public ObservableCollection<ModelUsageItem> ModelUsageRows { get; }

    public ObservableCollection<UsageTrendPoint> TrendPoints { get; }

    public ProviderProtocol[] ProtocolOptions { get; }

    public IReadOnlyList<I18nLanguageOption> SupportedLanguages => _i18n.Languages;

    public IRelayCommand<ClientAppItem> SelectClientAppCommand { get; }

    public IRelayCommand ShowProvidersCommand { get; }

    public IRelayCommand ShowUsageCommand { get; }

    public IRelayCommand ShowModelsCommand { get; }

    public IRelayCommand OpenSettingsCommand { get; }

    public IRelayCommand BackFromSettingsCommand { get; }

    public IRelayCommand<string> SelectSettingsTabCommand { get; }

    public IRelayCommand<string> SelectUsageTabCommand { get; }

    public IRelayCommand<string> SelectUsageRangeCommand { get; }

    public IRelayCommand<string> SelectThemeCommand { get; }

    public IAsyncRelayCommand ToggleProxyCommand { get; }

    public IAsyncRelayCommand RestartProxyCommand { get; }

    public IAsyncRelayCommand StopProxyCommand { get; }

    public IRelayCommand<ProviderListItem> SelectProviderCommand { get; }

    public IRelayCommand<ProviderListItem> EditProviderCommand { get; }

    public IRelayCommand AddProviderCommand { get; }

    public IRelayCommand<ProviderTemplateItem> SelectProviderTemplateCommand { get; }

    public IAsyncRelayCommand LoginCodexOAuthCommand { get; }

    public IRelayCommand<ProviderListItem> RequestRemoveProviderCommand { get; }

    public IRelayCommand CancelRemoveProviderCommand { get; }

    public IAsyncRelayCommand ConfirmRemoveProviderCommand { get; }

    public IRelayCommand<OAuthAccountListItem> SelectOAuthAccountCommand { get; }

    public IRelayCommand<OAuthAccountListItem> RemoveOAuthAccountCommand { get; }

    public IRelayCommand<OAuthAccountListItem> SaveOAuthAccountNameCommand { get; }

    public IRelayCommand CloseProviderDialogCommand { get; }

    public IAsyncRelayCommand SaveProviderCommand { get; }

    public IRelayCommand AddProviderModelCommand { get; }

    public IRelayCommand<ModelEditorItem> RemoveProviderModelCommand { get; }

    public IRelayCommand AddPricingModelCommand { get; }

    public IRelayCommand<ModelCatalogItem> EditPricingModelCommand { get; }

    public IRelayCommand<ModelCatalogItem> RequestRemovePricingModelCommand { get; }

    public IRelayCommand CancelRemovePricingModelCommand { get; }

    public IRelayCommand ConfirmRemovePricingModelCommand { get; }

    public IRelayCommand CloseModelDialogCommand { get; }

    public IRelayCommand SaveModelCommand { get; }

    public IAsyncRelayCommand ApplyCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    public IAsyncRelayCommand RefreshUsageCommand { get; }

    public IAsyncRelayCommand CheckForUpdatesCommand { get; }

    public IRelayCommand OpenLatestReleaseCommand { get; }

    public async ValueTask DisposeAsync()
    {
        await _proxyHostService.DisposeAsync();
        _sharedHttpClient.Dispose();
    }

    private async Task EnsureIconsAsync()
    {
        await _iconCacheService.EnsureDefaultIconsAsync();
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RefreshClientApps();
            RefreshProviderRows();
            RefreshModelCatalogRows();
        });
    }

    private void SelectClientApp(ClientAppItem? item)
    {
        if (item is null)
            return;

        SelectedClientApp = item.Kind;
        _config.Ui.DefaultApp = item.Kind;
        DefaultClientAppIsCodex = item.Kind == ClientAppKind.Codex;
        _store.SaveConfig(_config);
        RefreshClientApps();
        CurrentPage = item.Kind == ClientAppKind.Codex ? "Providers" : "Claude";
    }

    private async Task ToggleProxyAsync()
    {
        if (_proxyHostService.State.IsRunning)
            await StopProxyAsync();
        else
            await RestartProxyAsync();
    }

    private void SelectTheme(string? theme)
    {
        UiTheme = AppThemeService.Normalize(theme);
        _config.Ui.Theme = UiTheme;
        _store.SaveConfig(_config);
        AppThemeService.Apply(UiTheme);
        StatusMessage = F("status.themeSwitched", T("settings.theme." + UiTheme));
    }

    private void SelectUsageRange(string? range)
    {
        UsageTimeRange = range switch
        {
            "Last7Days" => UsageTimeRange.Last7Days,
            "Last30Days" => UsageTimeRange.Last30Days,
            _ => UsageTimeRange.Last24Hours
        };
    }

    private async Task RefreshUsageDashboardAsync()
    {
        if (IsUsageRefreshing)
            return;

        IsUsageRefreshing = true;
        var startedAt = DateTimeOffset.UtcNow;
        try
        {
            await Task.Yield();
            RefreshUsageDashboard();
            var remaining = TimeSpan.FromMilliseconds(420) - (DateTimeOffset.UtcNow - startedAt);
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);
        }
        finally
        {
            IsUsageRefreshing = false;
        }
    }

    private async Task SaveAsync()
    {
        await PersistSettingsAsync(T("status.settingsSaved"));
    }

    private async Task ApplyAsync()
    {
        await PersistSettingsAsync(T("status.settingsApplied"));
    }

    private async Task CheckForUpdatesAsync(bool silent)
    {
        var started = false;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (IsCheckingForUpdates)
                return;

            started = true;
            IsCheckingForUpdates = true;
            if (!silent)
                UpdateStatusDetails = T("update.checking");
            OnPropertyChanged(nameof(UpdateCheckButtonText));
        });

        if (!started)
            return;

        try
        {
            var result = await _updateCheckService.CheckForUpdatesAsync();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyUpdateCheckResult(result);
                if (!silent)
                    StatusMessage = result.Message ?? T("status.updateFinished");
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsCheckingForUpdates = false;
                OnPropertyChanged(nameof(UpdateCheckButtonText));
            });
        }
    }

    private async Task PersistSettingsAsync(string successMessage)
    {
        _config.Proxy.Host = string.IsNullOrWhiteSpace(ProxyListenHost) ? "127.0.0.1" : ProxyListenHost.Trim();
        _config.Proxy.Port = ProxyPort <= 0 ? 12785 : ProxyPort;
        _config.Proxy.InboundApiKey = InboundApiKey.Trim();
        _config.Proxy.Enabled = ProxyEnabled;
        _config.Ui.Language = string.IsNullOrWhiteSpace(UiLanguage) ? _i18n.DefaultLanguageCode : UiLanguage.Trim();
        _config.Ui.Theme = AppThemeService.Normalize(UiTheme);
        UiTheme = _config.Ui.Theme;
        _config.Ui.StartWithWindows = StartWithWindows;
        _config.Ui.DefaultApp = DefaultClientAppIsCodex ? ClientAppKind.Codex : ClientAppKind.ClaudeCode;

        _pricing.BillingUnitTokens = BillingUnitTokens <= 0 ? 1_000_000 : BillingUnitTokens;
        _pricing.FastMode.DefaultMultiplier = DefaultFastMultiplier <= 0 ? 1m : DefaultFastMultiplier;
        _pricing.FastMode.ModelOverrides["gpt-5.5*"] = Gpt55FastMultiplier <= 0 ? _pricing.FastMode.DefaultMultiplier : Gpt55FastMultiplier;

        _store.SaveConfig(_config);
        _store.SavePricing(_pricing);
        AppThemeService.Apply(_config.Ui.Theme);
        RefreshSettingsFields();
        RefreshModelCatalogRows();
        if (_config.Proxy.Enabled)
            await RestartProxyAsync();
        else
            await StopProxyAsync();
        StatusMessage = successMessage;
    }

    private async Task RestartProxyAsync()
    {
        _config.Proxy.Enabled = true;
        ProxyEnabled = true;
        _store.SaveConfig(_config);
        StatusMessage = T("status.proxyStarting");
        await _proxyHostService.RestartAsync(_config);
        StatusMessage = _proxyHostService.State.IsRunning
            ? T("status.proxyRunning")
            : _proxyHostService.State.Error ?? T("status.proxyStopped");
        OnProxyStateDisplayChanged();
    }

    private async Task StopProxyAsync()
    {
        _config.Proxy.Enabled = false;
        ProxyEnabled = false;
        _store.SaveConfig(_config);
        await _proxyHostService.StopAsync();
        StatusMessage = T("status.proxyStopped");
        OnProxyStateDisplayChanged();
    }

    private async Task ActivateProviderAsync(ProviderListItem? row)
    {
        if (row is null)
            return;

        SelectProvider(row);
        _config.ActiveProviderId = row.Id;
        _store.SaveConfig(_config);
        RefreshProviderRows();
        SelectProvider(ProviderRows.FirstOrDefault(provider => provider.Id == row.Id));
        if (_config.Proxy.Enabled)
            await RestartProxyAsync();
    }

    private void SelectProvider(ProviderListItem? row)
    {
        if (row is null)
            return;

        SelectedProviderId = row.Id;
        foreach (var providerRow in ProviderRows)
            providerRow.IsSelected = string.Equals(providerRow.Id, SelectedProviderId, StringComparison.OrdinalIgnoreCase);

        var provider = FindSelectedProvider();
        if (provider is null)
            return;

        LoadProviderFields(provider);
    }

    private void OpenAddProvider()
    {
        CurrentPage = "AddProvider";
        _editingProviderId = null;
        ProviderDialogTitle = T("providerDialog.addTitle");
        SelectProviderTemplate(ProviderTemplates.FirstOrDefault(template => template.Id == ProviderTemplateCatalog.CustomTemplateId));
    }

    private void SelectProviderTemplate(ProviderTemplateItem? template)
    {
        if (template is null)
            return;

        SelectedProviderTemplateId = template.Id;
        foreach (var item in ProviderTemplates)
            item.IsSelected = string.Equals(item.Id, template.Id, StringComparison.OrdinalIgnoreCase);

        var provider = ProviderTemplateCatalog.CreateProvider(template.Id, _config.Providers.Select(item => item.Id));
        _editingProviderId = null;
        SelectedProviderId = "";
        LoadProviderFields(provider);
        StatusMessage = template.Id == ProviderTemplateCatalog.CustomTemplateId
            ? T("status.providerTemplateCustom")
            : F("status.providerTemplateApplied", template.DisplayName);
    }

    private void OpenEditProvider(ProviderListItem? row)
    {
        if (row is null)
            return;

        var provider = _config.Providers.FirstOrDefault(item => string.Equals(item.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return;

        _editingProviderId = provider.Id;
        ProviderDialogTitle = T("providerDialog.editTitle");
        SelectedProviderId = provider.Id;
        LoadProviderFields(provider);
        IsProviderDialogOpen = true;
    }

    private async Task SaveProviderDialogAsync()
    {
        var isNew = string.IsNullOrWhiteSpace(_editingProviderId);
        var provider = isNew
            ? new ProviderConfig { Id = MakeUniqueId(CreateProviderId(SelectedProviderName), _config.Providers.Select(item => item.Id)) }
            : _config.Providers.FirstOrDefault(item => string.Equals(item.Id, _editingProviderId, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
            return;

        if (isNew && !string.Equals(SelectedProviderTemplateId, ProviderTemplateCatalog.CustomTemplateId, StringComparison.OrdinalIgnoreCase))
            ProviderTemplateCatalog.ApplyTemplate(provider, SelectedProviderTemplateId);

        provider.DisplayName = string.IsNullOrWhiteSpace(SelectedProviderName) ? provider.Id : SelectedProviderName.Trim();
        provider.Note = string.IsNullOrWhiteSpace(SelectedProviderNote) ? null : SelectedProviderNote.Trim();
        provider.Website = string.IsNullOrWhiteSpace(SelectedProviderWebsite) ? null : SelectedProviderWebsite.Trim();
        provider.BaseUrl = SelectedBaseUrl.Trim();
        provider.ApiKey = provider.AuthMode == ProviderAuthMode.OAuth ? "" : SelectedApiKey.Trim();
        provider.DefaultModel = SelectedDefaultModel.Trim();
        provider.Protocol = SelectedProtocol;
        provider.OverrideRequestModel = SelectedOverrideModel;
        provider.ServiceTier = string.IsNullOrWhiteSpace(SelectedServiceTier) ? null : SelectedServiceTier.Trim();
        provider.Cost ??= new ProviderCostSettings();
        provider.Cost.FastMode = SelectedFastMode;
        provider.Models.Clear();

        foreach (var row in ModelRows)
        {
            if (string.IsNullOrWhiteSpace(row.Id))
                continue;

            provider.Models.Add(new ModelRouteConfig
            {
                Id = row.Id.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(row.DisplayName) ? null : row.DisplayName.Trim(),
                UpstreamModel = string.IsNullOrWhiteSpace(row.UpstreamModel) ? null : row.UpstreamModel.Trim(),
                Protocol = row.Protocol,
                ServiceTier = string.IsNullOrWhiteSpace(row.ServiceTier) ? null : row.ServiceTier.Trim(),
                Cost = new ProviderCostSettings { FastMode = row.FastMode }
            });
        }

        if (provider.Models.Count == 0 && !string.IsNullOrWhiteSpace(provider.DefaultModel))
        {
            provider.Models.Add(new ModelRouteConfig
            {
                Id = provider.DefaultModel,
                Protocol = provider.Protocol,
                ServiceTier = provider.ServiceTier,
                Cost = new ProviderCostSettings { FastMode = provider.Cost.FastMode }
            });
        }

        if (isNew)
        {
            _config.Providers.Add(provider);
            if (string.IsNullOrWhiteSpace(_config.ActiveProviderId))
                _config.ActiveProviderId = provider.Id;
        }

        _store.SaveConfig(_config);
        RefreshProviderRows();
        SelectProvider(ProviderRows.FirstOrDefault(row => row.Id == provider.Id));
        IsProviderDialogOpen = false;
        StatusMessage = isNew ? T("status.providerAdded") : T("status.providerSaved");
        if (_config.Proxy.Enabled && string.Equals(_config.ActiveProviderId, provider.Id, StringComparison.OrdinalIgnoreCase))
            await RestartProxyAsync();
    }

    private void LoadProviderFields(ProviderConfig provider)
    {
        _isLoadingProviderFields = true;
        try
        {
            SelectedProviderName = provider.DisplayName;
            SelectedProviderNote = provider.Note ?? "";
            SelectedProviderWebsite = provider.Website ?? "";
            SelectedBaseUrl = provider.BaseUrl;
            SelectedApiKey = provider.AuthMode == ProviderAuthMode.OAuth ? "" : provider.ApiKey;
            SelectedDefaultModel = provider.DefaultModel;
            SelectedProtocol = provider.Protocol;
            SelectedOverrideModel = provider.OverrideRequestModel;
            SelectedServiceTier = provider.ServiceTier ?? "";
            SelectedFastMode = provider.Cost?.FastMode ?? _config.GlobalCost.FastMode;
            ModelRows.Clear();

            foreach (var model in provider.Models)
            {
                ModelRows.Add(new ModelEditorItem
                {
                    Id = model.Id,
                    DisplayName = model.DisplayName ?? "",
                    UpstreamModel = model.UpstreamModel ?? "",
                    Protocol = model.Protocol,
                    ServiceTier = model.ServiceTier ?? "",
                    FastMode = model.Cost?.FastMode ?? provider.Cost?.FastMode ?? _config.GlobalCost.FastMode,
                    RemoveCommand = RemoveProviderModelCommand
                });
            }

            if (ModelRows.Count == 0)
            {
                ModelRows.Add(new ModelEditorItem
                {
                    Id = provider.DefaultModel,
                    Protocol = provider.Protocol,
                    ServiceTier = provider.ServiceTier ?? "",
                    FastMode = provider.Cost?.FastMode ?? false,
                    RemoveCommand = RemoveProviderModelCommand
                });
            }
        }
        finally
        {
            _isLoadingProviderFields = false;
        }
    }

    private void AddProviderModel()
    {
        var seed = string.IsNullOrWhiteSpace(SelectedDefaultModel) ? "new-model" : SelectedDefaultModel.Trim();
        ModelRows.Add(new ModelEditorItem
        {
            Id = MakeUniqueId(seed, ModelRows.Select(row => row.Id)),
            Protocol = SelectedProtocol,
            ServiceTier = SelectedServiceTier,
            FastMode = SelectedFastMode,
            RemoveCommand = RemoveProviderModelCommand
        });
        StatusMessage = T("status.modelRouteAdded");
    }

    private void RemoveProviderModel(ModelEditorItem? row)
    {
        if (row is null)
            return;

        ModelRows.Remove(row);
        StatusMessage = T("status.modelRouteRemoved");
    }

    private async Task LoginCodexOAuthAsync()
    {
        var provider = _config.Providers.FirstOrDefault(item =>
            string.Equals(item.BuiltinId, ProviderTemplateCatalog.CodexOAuthBuiltinId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            provider = ProviderTemplateCatalog.CreateProvider(
                ProviderTemplateCatalog.CodexOAuthBuiltinId,
                _config.Providers.Select(item => item.Id));
            _config.Providers.Add(provider);
        }

        if (provider.OAuth is null)
        {
            StatusMessage = T("status.oauthIncomplete");
            return;
        }

        StatusMessage = T("status.oauthOpeningBrowser");
        try
        {
            var account = await _codexOAuthLoginService.LoginAsync(provider.OAuth, CancellationToken.None);
            _providerAuthService.AddOrUpdateOAuthAccount(provider, account, makeActive: true);
            _config.ActiveProviderId = provider.Id;
            _store.SaveConfig(_config);
            RefreshProviderRows();
            SelectProvider(ProviderRows.FirstOrDefault(row => row.Id == provider.Id));
            StatusMessage = F("status.oauthLoggedIn", ProviderAuthService.ResolveAccountDisplayName(account));
            if (_config.Proxy.Enabled)
                await RestartProxyAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = F("status.oauthLoginFailed", ex.Message);
        }
    }

    private void RequestRemoveProvider(ProviderListItem? row)
    {
        if (row is null)
            return;

        _providerPendingDeleteId = row.Id;
        ProviderPendingDeleteName = row.DisplayName;
        IsDeleteProviderDialogOpen = true;
    }

    private async Task ConfirmRemoveProviderAsync()
    {
        if (string.IsNullOrWhiteSpace(_providerPendingDeleteId))
            return;

        var provider = _config.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, _providerPendingDeleteId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            IsDeleteProviderDialogOpen = false;
            return;
        }

        var wasActive = string.Equals(provider.Id, _config.ActiveProviderId, StringComparison.OrdinalIgnoreCase);
        _config.Providers.Remove(provider);
        if (wasActive)
            _config.ActiveProviderId = _config.Providers.FirstOrDefault()?.Id ?? "";

        _providerPendingDeleteId = null;
        ProviderPendingDeleteName = "";
        IsDeleteProviderDialogOpen = false;
        _store.SaveConfig(_config);
        RefreshProviderRows();
        SelectProvider(ProviderRows.FirstOrDefault(row => row.Id == _config.ActiveProviderId) ?? ProviderRows.FirstOrDefault());
        StatusMessage = T("status.providerRemoved");
        if (wasActive && _config.Proxy.Enabled)
            await RestartProxyAsync();
    }

    private void SelectOAuthAccount(OAuthAccountListItem? row)
    {
        if (row is null)
            return;

        var provider = _config.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, row.ProviderId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return;

        provider.ActiveAccountId = row.AccountId;
        _store.SaveConfig(_config);
        RefreshProviderRows();
        StatusMessage = T("status.oauthAccountSwitched");
    }

    private void RemoveOAuthAccount(OAuthAccountListItem? row)
    {
        if (row is null)
            return;

        var provider = _config.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, row.ProviderId, StringComparison.OrdinalIgnoreCase));
        var account = provider?.OAuthAccounts.FirstOrDefault(item =>
            string.Equals(item.Id, row.AccountId, StringComparison.OrdinalIgnoreCase));
        if (provider is null || account is null)
            return;

        provider.OAuthAccounts.Remove(account);
        if (string.Equals(provider.ActiveAccountId, row.AccountId, StringComparison.OrdinalIgnoreCase))
            provider.ActiveAccountId = provider.OAuthAccounts.FirstOrDefault()?.Id;

        _store.SaveConfig(_config);
        RefreshProviderRows();
        StatusMessage = T("status.oauthAccountRemoved");
    }

    private void SaveOAuthAccountName(OAuthAccountListItem? row)
    {
        if (row is null)
            return;

        var provider = _config.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, row.ProviderId, StringComparison.OrdinalIgnoreCase));
        var account = provider?.OAuthAccounts.FirstOrDefault(item =>
            string.Equals(item.Id, row.AccountId, StringComparison.OrdinalIgnoreCase));
        if (account is null)
            return;

        account.DisplayName = string.IsNullOrWhiteSpace(row.DisplayName)
            ? ProviderAuthService.ResolveAccountDisplayName(account)
            : row.DisplayName.Trim();
        _store.SaveConfig(_config);
        RefreshProviderRows();
        StatusMessage = T("status.oauthAccountNameSaved");
    }

    private void OpenAddModel()
    {
        _editingModelId = null;
        ModelDialogTitle = T("modelDialog.addTitle");
        ModelEditorId = MakeUniqueId("new-model", _pricing.Models.Select(model => model.Id));
        ModelEditorDisplayName = "";
        ModelEditorAliases = "";
        ModelEditorIconSlug = "openai";
        ModelEditorInputTierLimit = null;
        ModelEditorInputPrice = 0m;
        ModelEditorInputOverflowPrice = 0m;
        ModelEditorCachedInputPrice = 0m;
        ModelEditorCacheCreationInputPrice = 0m;
        ModelEditorOutputTierLimit = null;
        ModelEditorOutputPrice = 0m;
        ModelEditorOutputOverflowPrice = 0m;
        ModelEditorFastMultiplierOverride = "";
        IsModelDialogOpen = true;
    }

    private void OpenEditModel(ModelCatalogItem? row)
    {
        if (row is null)
            return;

        var rule = _pricing.Models.FirstOrDefault(item => string.Equals(item.Id, row.Id, StringComparison.OrdinalIgnoreCase));
        if (rule is null)
            return;

        _editingModelId = rule.Id;
        ModelDialogTitle = T("modelDialog.editTitle");
        ModelEditorId = rule.Id;
        ModelEditorDisplayName = rule.DisplayName ?? "";
        ModelEditorAliases = string.Join(", ", rule.Aliases);
        ModelEditorIconSlug = IconCacheService.ResolveModelIconSlug(rule.Id, rule.IconSlug);
        ModelEditorInputTierLimit = GetFirstTierLimit(rule.Input);
        ModelEditorInputPrice = GetTierPrice(rule.Input, 0);
        ModelEditorInputOverflowPrice = GetTierPrice(rule.Input, 1);
        ModelEditorCachedInputPrice = GetTierPrice(rule.CachedInput, 0);
        ModelEditorCacheCreationInputPrice = GetTierPrice(rule.CacheCreationInput, 0);
        ModelEditorOutputTierLimit = GetFirstTierLimit(rule.Output);
        ModelEditorOutputPrice = GetTierPrice(rule.Output, 0);
        ModelEditorOutputOverflowPrice = GetTierPrice(rule.Output, 1);
        ModelEditorFastMultiplierOverride = ResolveFastOverride(rule);
        IsModelDialogOpen = true;
    }

    private void SaveModelDialog()
    {
        if (string.IsNullOrWhiteSpace(ModelEditorId))
            return;

        var id = ModelEditorId.Trim();
        var isNew = string.IsNullOrWhiteSpace(_editingModelId);
        var rule = isNew
            ? new ModelPricingRule()
            : _pricing.Models.FirstOrDefault(item => string.Equals(item.Id, _editingModelId, StringComparison.OrdinalIgnoreCase));

        if (rule is null)
            return;

        rule.Id = id;
        rule.DisplayName = string.IsNullOrWhiteSpace(ModelEditorDisplayName) ? null : ModelEditorDisplayName.Trim();
        rule.IconSlug = IconCacheService.ResolveModelIconSlug(id, ModelEditorIconSlug);
        rule.Aliases = ParseAliases(ModelEditorAliases);
        rule.Input = BuildTieredPriceTable(ModelEditorInputTierLimit, ModelEditorInputPrice, ModelEditorInputOverflowPrice);
        rule.CachedInput = BuildFlatPriceTable(ModelEditorCachedInputPrice);
        rule.CacheCreationInput = BuildFlatPriceTable(ModelEditorCacheCreationInputPrice);
        rule.Output = BuildTieredPriceTable(ModelEditorOutputTierLimit, ModelEditorOutputPrice, ModelEditorOutputOverflowPrice);

        if (isNew)
            _pricing.Models.Add(rule);

        if (TryParsePositiveDecimal(ModelEditorFastMultiplierOverride, out var overrideMultiplier))
            _pricing.FastMode.ModelOverrides[id] = overrideMultiplier;
        else
            _pricing.FastMode.ModelOverrides.Remove(id);

        _store.SavePricing(_pricing);
        _ = _iconCacheService.EnsureIconAsync(rule.IconSlug);
        RefreshModelCatalogRows();
        RefreshPricingRows();
        IsModelDialogOpen = false;
        StatusMessage = isNew ? T("status.modelAdded") : T("status.modelSaved");
    }

    private void RequestRemovePricingModel(ModelCatalogItem? row)
    {
        if (row is null)
            return;

        _modelPendingDelete = row;
        IsDeleteModelDialogOpen = true;
    }

    private void ConfirmRemovePricingModel()
    {
        if (_modelPendingDelete is null)
            return;

        var rule = _pricing.Models.FirstOrDefault(item => string.Equals(item.Id, _modelPendingDelete.Id, StringComparison.OrdinalIgnoreCase));
        if (rule is not null)
            _pricing.Models.Remove(rule);

        _pricing.FastMode.ModelOverrides.Remove(_modelPendingDelete.Id);
        _store.SavePricing(_pricing);
        RefreshModelCatalogRows();
        RefreshPricingRows();
        IsDeleteModelDialogOpen = false;
        StatusMessage = T("status.modelRemoved");
    }

    private void OpenSettings()
    {
        _returnPage = IsSettingsPageVisible ? _returnPage : CurrentPage;
        CurrentPage = "Settings";
    }

    private void BackFromSettings()
    {
        CurrentPage = string.IsNullOrWhiteSpace(_returnPage) ? "Providers" : _returnPage;
    }

    private void ApplyUpdateCheckResult(UpdateCheckResult result)
    {
        CurrentVersionTag = "v" + result.CurrentVersion;
        LatestVersionTag = result.LatestVersion is null ? T("update.noReleaseYet") : "v" + result.LatestVersion;
        LatestReleasePublishedAtText = result.PublishedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            ?? T("update.notPublished");
        LatestReleaseUrl = string.IsNullOrWhiteSpace(result.ReleaseUrl) ? AppReleaseInfo.ReleasesUrl : result.ReleaseUrl;
        UpdateStatusDetails = result.Status switch
        {
            UpdateCheckStatus.NoRelease => T("update.noRelease"),
            UpdateCheckStatus.UpToDate => T("update.upToDate"),
            UpdateCheckStatus.UpdateAvailable => T("update.available"),
            UpdateCheckStatus.Failed => F("update.failed", result.Message),
            _ => result.Message ?? T("update.unavailable")
        };

        OnPropertyChanged(nameof(CanOpenLatestRelease));
    }

    private void OpenLatestRelease()
    {
        OpenExternalUrl(LatestReleaseUrl);
    }

    private static void OpenExternalUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private void RefreshClientApps()
    {
        EnsureClientApp(ClientAppKind.Codex, "Codex", _iconCacheService.GetIconPath("codex-color"));
        EnsureClientApp(ClientAppKind.ClaudeCode, "Claude Code", _iconCacheService.GetIconPath("claudecode-color"));

        foreach (var app in ClientApps)
            app.IsSelected = app.Kind == SelectedClientApp;
    }

    private void EnsureClientApp(ClientAppKind kind, string name, string iconPath)
    {
        if (ClientApps.Any(app => app.Kind == kind))
            return;

        ClientApps.Add(new ClientAppItem
        {
            Kind = kind,
            Name = name,
            IconPath = iconPath,
            SelectCommand = SelectClientAppCommand
        });
    }

    private void RefreshProviderTemplates()
    {
        ProviderTemplates.Clear();
        foreach (var template in ProviderTemplateCatalog.VisibleTemplates)
        {
            ProviderTemplates.Add(new ProviderTemplateItem
            {
                Id = template.Id,
                DisplayName = template.DisplayName,
                Description = template.Description,
                IconPath = _iconCacheService.GetIconPath(template.IconSlug),
                IsSelected = string.Equals(template.Id, SelectedProviderTemplateId, StringComparison.OrdinalIgnoreCase),
                SelectCommand = SelectProviderTemplateCommand
            });
        }
    }

    private void RefreshProviderRows()
    {
        ConfigurationStore.EnsureValidDefaults(_config);
        ProviderRows.Clear();
        foreach (var provider in _config.Providers)
        {
            var iconSlug = provider.IconSlug ?? (provider.Protocol == ProviderProtocol.AnthropicMessages ? "claude" : "openai");
            var activeAccount = provider.OAuthAccounts.FirstOrDefault(account =>
                string.Equals(account.Id, provider.ActiveAccountId, StringComparison.OrdinalIgnoreCase));
            var row = new ProviderListItem
            {
                Id = provider.Id,
                DisplayName = string.IsNullOrWhiteSpace(provider.DisplayName) ? provider.Id : provider.DisplayName,
                BaseUrl = provider.BaseUrl,
                IconPath = _iconCacheService.GetIconPath(iconSlug),
                Protocol = provider.Protocol.ToString(),
                DefaultModel = provider.DefaultModel,
                AuthMode = provider.AuthMode == ProviderAuthMode.OAuth ? "OAuth" : T("providers.apiKey"),
                IsOAuth = provider.AuthMode == ProviderAuthMode.OAuth,
                AccountSummary = provider.AuthMode == ProviderAuthMode.OAuth
                    ? activeAccount is null
                        ? T("providers.notLoggedIn")
                        : F("providers.currentAccount", ProviderAuthService.ResolveAccountDisplayName(activeAccount))
                    : T("providers.apiKey"),
                ModelsText = provider.Models.Count == 0
                    ? provider.DefaultModel
                    : string.Join(", ", provider.Models.Select(model => $"{model.Id}:{model.Protocol}")),
                IsActive = string.Equals(provider.Id, _config.ActiveProviderId, StringComparison.OrdinalIgnoreCase),
                IsSelected = string.Equals(provider.Id, SelectedProviderId, StringComparison.OrdinalIgnoreCase),
                SelectCommand = SelectProviderCommand,
                EditCommand = EditProviderCommand,
                DeleteCommand = RequestRemoveProviderCommand
            };

            foreach (var account in provider.OAuthAccounts)
            {
                row.OAuthAccounts.Add(new OAuthAccountListItem
                {
                    ProviderId = provider.Id,
                    AccountId = account.Id,
                    DisplayName = ProviderAuthService.ResolveAccountDisplayName(account),
                    Email = account.Email ?? "",
                    IsActive = string.Equals(account.Id, provider.ActiveAccountId, StringComparison.OrdinalIgnoreCase),
                    SelectCommand = SelectOAuthAccountCommand,
                    RemoveCommand = RemoveOAuthAccountCommand,
                    SaveNameCommand = SaveOAuthAccountNameCommand
                });
            }

            ProviderRows.Add(row);
        }

        ActiveProviderId = _config.ActiveProviderId;
    }

    private void RefreshSettingsFields()
    {
        _isRefreshingSettingsFields = true;
        try
        {
            ProxyListenHost = _config.Proxy.Host;
            ProxyPort = _config.Proxy.Port;
            InboundApiKey = _config.Proxy.InboundApiKey;
            ProxyEnabled = _config.Proxy.Enabled;
            Endpoint = _config.Proxy.Endpoint;
            UiLanguage = _i18n.GetLanguage(_config.Ui.Language).Code;
            SelectedLanguage = _i18n.GetLanguage(UiLanguage);
            UiTheme = AppThemeService.Normalize(_config.Ui.Theme);
            _config.Ui.Theme = UiTheme;
            StartWithWindows = _config.Ui.StartWithWindows;
            SelectedClientApp = _config.Ui.DefaultApp;
            DefaultClientAppIsCodex = _config.Ui.DefaultApp == ClientAppKind.Codex;
            BillingUnitTokens = _pricing.BillingUnitTokens;
            DefaultFastMultiplier = _pricing.FastMode.DefaultMultiplier;
            Gpt55FastMultiplier = ResolveGpt55FastMultiplier();
            PricingCurrency = string.IsNullOrWhiteSpace(_pricing.Currency) ? "USD" : _pricing.Currency;
            RefreshClientApps();
        }
        finally
        {
            _isRefreshingSettingsFields = false;
        }
    }

    private void RefreshPricingRows()
    {
        PricingRows.Clear();
        foreach (var rule in _pricing.Models)
        {
            PricingRows.Add(new ModelPricingEditorItem
            {
                Id = rule.Id,
                AliasesText = string.Join(", ", rule.Aliases),
                InputTierLimit = GetFirstTierLimit(rule.Input),
                InputPrice = GetTierPrice(rule.Input, 0),
                InputOverflowPrice = GetTierPrice(rule.Input, 1),
                CachedInputPrice = GetTierPrice(rule.CachedInput, 0),
                CacheCreationInputPrice = GetTierPrice(rule.CacheCreationInput, 0),
                OutputTierLimit = GetFirstTierLimit(rule.Output),
                OutputPrice = GetTierPrice(rule.Output, 0),
                OutputOverflowPrice = GetTierPrice(rule.Output, 1),
                FastMultiplierOverride = ResolveFastOverride(rule)
            });
        }
    }

    private void RefreshModelCatalogRows()
    {
        ModelCatalogRows.Clear();
        foreach (var rule in _pricing.Models)
        {
            var iconSlug = IconCacheService.ResolveModelIconSlug(rule.Id, rule.IconSlug);
            var providerIds = ProviderRoutingResolver.FindProvidersForPatterns(_config, [rule.Id, .. rule.Aliases]);
            var providerList = providerIds.Count == 0 ? "-" : string.Join(", ", providerIds);
            ModelCatalogRows.Add(new ModelCatalogItem
            {
                Id = rule.Id,
                DisplayName = string.IsNullOrWhiteSpace(rule.DisplayName) ? rule.Id : rule.DisplayName!,
                AliasesText = rule.Aliases.Count == 0 ? "-" : string.Join(", ", rule.Aliases),
                ProvidersText = F("models.providers", providerList),
                IconPath = _iconCacheService.GetIconPath(iconSlug),
                IconSlug = iconSlug,
                InputPriceText = FormatPrice(rule.Input),
                InputTierText = FormatTierHint(rule.Input),
                CachedInputPriceText = FormatPrice(rule.CachedInput),
                CacheCreationInputPriceText = FormatPrice(rule.CacheCreationInput),
                OutputPriceText = FormatPrice(rule.Output),
                OutputTierText = FormatTierHint(rule.Output),
                FastMultiplierText = string.IsNullOrWhiteSpace(ResolveFastOverride(rule)) ? $"{_pricing.FastMode.DefaultMultiplier:0.####}x" : ResolveFastOverride(rule) + "x",
                EditCommand = EditPricingModelCommand,
                DeleteCommand = RequestRemovePricingModelCommand
            });
        }

        OnPropertyChanged(nameof(ModelCatalogCountText));
    }

    private void RefreshUsageDashboard()
    {
        var dashboard = _usageLogReader.Read(UsageTimeRange);
        UsageMetrics.Clear();
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.requests"),
            dashboard.Requests.ToString("N0", CultureInfo.InvariantCulture),
            LucideIconKind.ChartNoAxesColumnIncreasing,
            "#60A5FA",
            "#1D3B5F"));
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.cost"),
            DisplayFormatters.FormatCost(dashboard.EstimatedCost),
            LucideIconKind.BadgeDollarSign,
            "#34D399",
            "#153B2D"));
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.tokens"),
            DisplayFormatters.FormatTokenCount(dashboard.InputTokens + dashboard.CachedInputTokens + dashboard.CacheCreationInputTokens + dashboard.OutputTokens + dashboard.ReasoningOutputTokens),
            LucideIconKind.Layers2,
            "#A78BFA",
            "#31254D"));
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.cachedTokens"),
            DisplayFormatters.FormatTokenCount(dashboard.CachedInputTokens + dashboard.CacheCreationInputTokens),
            LucideIconKind.DatabaseZap,
            "#FBBF24",
            "#453516"));

        UsageLogRows.Clear();
        foreach (var record in dashboard.Logs.Take(80))
            UsageLogRows.Add(UsageLogItem.From(record));

        ProviderUsageRows.Clear();
        foreach (var summary in dashboard.ProviderSummaries)
            ProviderUsageRows.Add(ProviderUsageItem.From(summary));

        ModelUsageRows.Clear();
        foreach (var summary in dashboard.ModelSummaries)
            ModelUsageRows.Add(ModelUsageItem.From(summary));

        TrendPoints.Clear();
        foreach (var point in dashboard.TrendPoints)
            TrendPoints.Add(point);

        OnPropertyChanged(nameof(UsageRangeCaption));
        OnPropertyChanged(nameof(UsageTrendGranularity));
        ApplySnapshot(_usageMeter.Snapshot);
    }

    private static UsageMetricItem CreateUsageMetric(
        string label,
        string value,
        LucideIconKind icon,
        string foreground,
        string background)
    {
        return new UsageMetricItem(
            label,
            value,
            icon,
            Brush.Parse(foreground),
            Brush.Parse(background));
    }

    private ProviderConfig? FindSelectedProvider()
    {
        return _config.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Id, SelectedProviderId, StringComparison.OrdinalIgnoreCase));
    }

    private decimal ResolveGpt55FastMultiplier()
    {
        if (_pricing.FastMode.ModelOverrides.TryGetValue("gpt-5.5*", out var wildcard))
            return wildcard;

        if (_pricing.FastMode.ModelOverrides.TryGetValue("gpt-5.5", out var exact))
            return exact;

        return 2.5m;
    }

    private string ResolveFastOverride(ModelPricingRule rule)
    {
        if (_pricing.FastMode.ModelOverrides.TryGetValue(rule.Id, out var exact))
            return exact.ToString("0.####", CultureInfo.InvariantCulture);

        foreach (var alias in rule.Aliases)
        {
            if (_pricing.FastMode.ModelOverrides.TryGetValue(alias, out var aliasOverride))
                return aliasOverride.ToString("0.####", CultureInfo.InvariantCulture);
        }

        if (rule.Id.StartsWith("gpt-5.5", StringComparison.OrdinalIgnoreCase))
            return ResolveGpt55FastMultiplier().ToString("0.####", CultureInfo.InvariantCulture);

        return "";
    }

    private static long? GetFirstTierLimit(TokenPriceTable table)
    {
        return table.Tiers.Count > 1 ? table.Tiers[0].UpToTokens : null;
    }

    private static decimal GetTierPrice(TokenPriceTable table, int index)
    {
        return table.Tiers.Count > index ? table.Tiers[index].PricePerUnit : 0m;
    }

    private static string FormatPrice(TokenPriceTable table)
    {
        var price = GetTierPrice(table, 0);
        return price > 0m ? price.ToString("0.####", CultureInfo.InvariantCulture) : "-";
    }

    private string FormatTierHint(TokenPriceTable table)
    {
        var tierLimit = GetFirstTierLimit(table);
        var overflowPrice = GetTierPrice(table, 1);
        if (tierLimit is null || overflowPrice <= 0m)
            return T("pricing.flat");

        return F("pricing.tierHint", DisplayFormatters.FormatTokenCount(tierLimit.Value), overflowPrice.ToString("0.####", CultureInfo.InvariantCulture));
    }

    private static TokenPriceTable BuildFlatPriceTable(decimal price)
    {
        var table = new TokenPriceTable();
        if (price > 0m)
            table.Tiers.Add(new PricingTier { UpToTokens = null, PricePerUnit = price });

        return table;
    }

    private static TokenPriceTable BuildTieredPriceTable(long? tierLimit, decimal firstPrice, decimal overflowPrice)
    {
        var table = new TokenPriceTable();
        if (tierLimit is > 0 && overflowPrice > 0m)
        {
            table.Tiers.Add(new PricingTier { UpToTokens = tierLimit, PricePerUnit = Math.Max(0m, firstPrice) });
            table.Tiers.Add(new PricingTier { UpToTokens = null, PricePerUnit = overflowPrice });
            return table;
        }

        if (firstPrice > 0m)
            table.Tiers.Add(new PricingTier { UpToTokens = null, PricePerUnit = firstPrice });

        return table;
    }

    private static Collection<string> ParseAliases(string aliasesText)
    {
        var aliases = new Collection<string>();
        foreach (var alias in aliasesText.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!aliases.Any(existing => string.Equals(existing, alias, StringComparison.OrdinalIgnoreCase)))
                aliases.Add(alias);
        }

        return aliases;
    }

    private static bool TryParsePositiveDecimal(string text, out decimal value)
    {
        return decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0m;
    }

    private static string MakeUniqueId(string seed, IEnumerable<string> existingIds)
    {
        var normalized = string.IsNullOrWhiteSpace(seed) ? "new-model" : seed.Trim();
        var existing = existingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(normalized))
            return normalized;

        for (var index = 2; ; index++)
        {
            var candidate = $"{normalized}-{index}";
            if (!existing.Contains(candidate))
                return candidate;
        }
    }

    private static string CreateProviderId(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var id = new string(chars).Trim('-');
        while (id.Contains("--", StringComparison.Ordinal))
            id = id.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(id) ? "provider" : id;
    }

    private string T(string key)
    {
        return _i18n.Translate(key);
    }

    private string F(string key, params object?[] args)
    {
        return _i18n.Format(key, args);
    }

    private string FormatProxyStatus(ProxyRuntimeState state)
    {
        var status = state.StatusText switch
        {
            "Starting" => T("proxy.starting"),
            "Running" => T("proxy.running"),
            "Stopped" => T("proxy.stopped"),
            "Disabled" => T("proxy.disabled"),
            "No active provider" => T("proxy.noActiveProvider"),
            "Port unavailable" => T("proxy.portUnavailable"),
            "Start failed" => T("proxy.startFailed"),
            _ => state.StatusText
        };

        return state.Error is null ? status : $"{status}: {state.Error}";
    }

    private void RefreshLocalizedText()
    {
        UiLanguage = _i18n.CurrentLanguageCode;
        SelectedLanguage = _i18n.CurrentLanguage;
        ProxyStatus = FormatProxyStatus(_proxyHostService.State);
        if (!LatestVersionTag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            LatestVersionTag = T("update.noReleaseYet");
        if (string.IsNullOrWhiteSpace(LatestReleasePublishedAtText) || !LatestReleasePublishedAtText.Contains('-', StringComparison.Ordinal))
            LatestReleasePublishedAtText = T("update.notPublished");

        if (IsProviderDialogOpen)
            ProviderDialogTitle = string.IsNullOrWhiteSpace(_editingProviderId) ? T("providerDialog.addTitle") : T("providerDialog.editTitle");
        if (IsModelDialogOpen)
            ModelDialogTitle = string.IsNullOrWhiteSpace(_editingModelId) ? T("modelDialog.addTitle") : T("modelDialog.editTitle");

        RefreshUsageDashboard();
        RefreshModelCatalogRows();
        OnPropertyChanged(nameof(SupportedLanguages));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(ServiceToggleText));
        OnPropertyChanged(nameof(ServiceStateText));
        OnPropertyChanged(nameof(UpdateCheckButtonText));
        OnPropertyChanged(nameof(PricingUnitText));
        OnPropertyChanged(nameof(ModelCatalogCountText));
    }

    private void ApplyProxyState(ProxyRuntimeState state)
    {
        ProxyStatus = FormatProxyStatus(state);
        Endpoint = state.Endpoint;
        ActiveProviderId = state.ActiveProviderId;
        OnProxyStateDisplayChanged();
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        RequestCount = snapshot.Requests;
        ErrorCount = snapshot.Errors;
        InputTokens = snapshot.InputTokens;
        CachedInputTokens = snapshot.CachedInputTokens;
        CacheCreationInputTokens = snapshot.CacheCreationInputTokens;
        OutputTokens = snapshot.OutputTokens;
        ReasoningOutputTokens = snapshot.ReasoningOutputTokens;
        EstimatedCost = snapshot.EstimatedCost;
        OnPropertyChanged(nameof(InputTokensText));
        OnPropertyChanged(nameof(CachedInputTokensText));
        OnPropertyChanged(nameof(CacheCreationInputTokensText));
        OnPropertyChanged(nameof(OutputTokensText));
        OnPropertyChanged(nameof(TotalTokensText));
        OnPropertyChanged(nameof(EstimatedCostText));
    }

    private void OnProxyStateDisplayChanged()
    {
        OnPropertyChanged(nameof(IsProxyAlert));
        OnPropertyChanged(nameof(ServiceToggleText));
        OnPropertyChanged(nameof(ServiceStateText));
    }

    partial void OnSelectedApiKeyChanged(string value)
    {
        if (_isLoadingProviderFields || string.IsNullOrWhiteSpace(SelectedProviderId))
            return;

        var provider = FindSelectedProvider();
        if (provider is null || provider.AuthMode == ProviderAuthMode.OAuth)
            return;

        var apiKey = value.Trim();
        if (string.Equals(provider.ApiKey, apiKey, StringComparison.Ordinal))
            return;

        provider.ApiKey = apiKey;
        _store.SaveConfig(_config);
        _proxyHostService.UpdateConfig(_config);
    }

    partial void OnCurrentPageChanged(string value)
    {
        OnPropertyChanged(nameof(IsProvidersPageVisible));
        OnPropertyChanged(nameof(IsAddProviderPageVisible));
        OnPropertyChanged(nameof(IsUsagePageVisible));
        OnPropertyChanged(nameof(IsModelsPageVisible));
        OnPropertyChanged(nameof(IsSettingsPageVisible));
        OnPropertyChanged(nameof(IsClaudePageVisible));
        OnPropertyChanged(nameof(WorkspaceTitle));
    }

    partial void OnSettingsTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralSettingsVisible));
        OnPropertyChanged(nameof(IsRouteSettingsVisible));
        OnPropertyChanged(nameof(IsAuthSettingsVisible));
        OnPropertyChanged(nameof(IsAdvancedSettingsVisible));
        OnPropertyChanged(nameof(IsUsageSettingsVisible));
        OnPropertyChanged(nameof(IsAboutSettingsVisible));
    }

    partial void OnUsageTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsUsageRequestsVisible));
        OnPropertyChanged(nameof(IsUsageProvidersVisible));
        OnPropertyChanged(nameof(IsUsageModelsVisible));
    }

    partial void OnUsageTimeRangeChanged(UsageTimeRange value)
    {
        OnPropertyChanged(nameof(IsUsageRange24HoursSelected));
        OnPropertyChanged(nameof(IsUsageRange7DaysSelected));
        OnPropertyChanged(nameof(IsUsageRange30DaysSelected));
        RefreshUsageDashboard();
    }

    partial void OnIsUsageRefreshingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUsageRefreshIdle));
        OnPropertyChanged(nameof(UsageRefreshButtonText));
    }

    partial void OnSelectedClientAppChanged(ClientAppKind value)
    {
        RefreshClientApps();
    }

    partial void OnUiLanguageChanged(string value)
    {
        var language = _i18n.GetLanguage(value);
        if (!string.Equals(SelectedLanguage?.Code, language.Code, StringComparison.OrdinalIgnoreCase))
            SelectedLanguage = language;
    }

    partial void OnSelectedLanguageChanged(I18nLanguageOption? value)
    {
        if (value is null)
            return;

        UiLanguage = value.Code;
        _config.Ui.Language = value.Code;
        _i18n.SetLanguage(value.Code);
        if (!_isRefreshingSettingsFields)
            _store.SaveConfig(_config);
    }

    partial void OnUiThemeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
        OnPropertyChanged(nameof(IsSystemThemeSelected));
    }

    partial void OnBillingUnitTokensChanged(long value)
    {
        OnPropertyChanged(nameof(PricingUnitText));
    }

    partial void OnPricingCurrencyChanged(string value)
    {
        OnPropertyChanged(nameof(PricingUnitText));
    }

    public bool IsProvidersPageVisible => CurrentPage == "Providers";

    public bool IsAddProviderPageVisible => CurrentPage == "AddProvider";

    public bool IsUsagePageVisible => CurrentPage == "Usage";

    public bool IsModelsPageVisible => CurrentPage == "Models";

    public bool IsSettingsPageVisible => CurrentPage == "Settings";

    public bool IsClaudePageVisible => CurrentPage == "Claude";

    public bool IsGeneralSettingsVisible => SettingsTab == "General";

    public bool IsRouteSettingsVisible => SettingsTab == "Route";

    public bool IsAuthSettingsVisible => SettingsTab == "Auth";

    public bool IsAdvancedSettingsVisible => SettingsTab == "Advanced";

    public bool IsUsageSettingsVisible => SettingsTab == "Usage";

    public bool IsAboutSettingsVisible => SettingsTab == "About";

    public bool IsUsageRequestsVisible => UsageTab == "Requests";

    public bool IsUsageProvidersVisible => UsageTab == "Providers";

    public bool IsUsageModelsVisible => UsageTab == "Models";

    public bool IsUsageRange24HoursSelected => UsageTimeRange == UsageTimeRange.Last24Hours;

    public bool IsUsageRange7DaysSelected => UsageTimeRange == UsageTimeRange.Last7Days;

    public bool IsUsageRange30DaysSelected => UsageTimeRange == UsageTimeRange.Last30Days;

    public bool IsUsageRefreshIdle => !IsUsageRefreshing;

    public UsageTrendGranularity UsageTrendGranularity => UsageTimeRange == UsageTimeRange.Last24Hours
        ? UsageTrendGranularity.Hour
        : UsageTrendGranularity.Day;

    public bool IsLightThemeSelected => string.Equals(UiTheme, "light", StringComparison.OrdinalIgnoreCase);

    public bool IsDarkThemeSelected => string.Equals(UiTheme, "dark", StringComparison.OrdinalIgnoreCase);

    public bool IsSystemThemeSelected => string.Equals(UiTheme, "system", StringComparison.OrdinalIgnoreCase);

    public bool CanOpenLatestRelease => !string.IsNullOrWhiteSpace(LatestReleaseUrl);

    public string CodexIconPath => _iconCacheService.GetIconPath("codex-color");

    public string ClaudeCodeIconPath => _iconCacheService.GetIconPath("claudecode-color");

    public string UpdateCheckButtonText => IsCheckingForUpdates ? T("update.checking") : T("settings.version.checkNow");

    public string RepositoryUrl => AppReleaseInfo.RepositoryUrl;

    public string ReleasesPageUrl => AppReleaseInfo.ReleasesUrl;

    public string AppDataRootPath => _paths.RootDirectory;

    public string CodexConfigFilePath => _paths.CodexConfigPath;

    public string CodexAuthFilePath => _paths.CodexAuthPath;

    public string UsageLogFilePath => _paths.UsageLogPath;

    public bool IsProxyAlert => !_config.Proxy.Enabled || !_proxyHostService.State.IsRunning || _proxyHostService.State.Error is not null;

    public string ServiceToggleText => _proxyHostService.State.IsRunning ? T("common.stop") : T("common.start");

    public string ServiceStateText => _proxyHostService.State.IsRunning ? T("status.proxyRunning") : T("status.proxyStopped");

    public string WorkspaceTitle => CurrentPage switch
    {
        "Usage" => T("usage.title"),
        "Models" => T("models.title"),
        "Settings" => T("settings.title"),
        "Claude" => T("claude.title"),
        _ => T("providers.title")
    };

    public string InputTokensText => DisplayFormatters.FormatTokenCount(InputTokens);

    public string CachedInputTokensText => DisplayFormatters.FormatTokenCount(CachedInputTokens);

    public string CacheCreationInputTokensText => DisplayFormatters.FormatTokenCount(CacheCreationInputTokens);

    public string OutputTokensText => DisplayFormatters.FormatTokenCount(OutputTokens);

    public string TotalTokensText => DisplayFormatters.FormatTokenCount(InputTokens + CachedInputTokens + CacheCreationInputTokens + OutputTokens + ReasoningOutputTokens);

    public string EstimatedCostText => DisplayFormatters.FormatCost(EstimatedCost);

    public string UsageRefreshButtonText => IsUsageRefreshing ? T("usage.refreshing") : T("common.refresh");

    public string UsageRangeCaption => UsageTimeRange switch
    {
        UsageTimeRange.Last7Days => T("usage.rangeCaption.7d"),
        UsageTimeRange.Last30Days => T("usage.rangeCaption.30d"),
        _ => T("usage.rangeCaption.24h")
    };

    public string PricingUnitText => $"{(string.IsNullOrWhiteSpace(PricingCurrency) ? "USD" : PricingCurrency)} / {DisplayFormatters.FormatTokenCount(BillingUnitTokens <= 0 ? 1_000_000 : BillingUnitTokens)} {T("common.tokens")}";

    public string ModelCatalogCountText => F("models.catalogCount", ModelCatalogRows.Count);
}

public sealed partial class ClientAppItem : ObservableObject
{
    public ClientAppKind Kind { get; init; }

    public string Name { get; init; } = "";

    public string IconPath { get; init; } = "";

    public IRelayCommand<ClientAppItem>? SelectCommand { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class ProviderListItem : ObservableObject
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string BaseUrl { get; set; } = "";

    public string IconPath { get; set; } = "";

    public string Protocol { get; set; } = "";

    public string DefaultModel { get; set; } = "";

    public string ModelsText { get; set; } = "";

    public string AuthMode { get; set; } = "";

    public string AccountSummary { get; set; } = "";

    public bool IsOAuth { get; set; }

    public bool IsActive { get; set; }

    public IRelayCommand<ProviderListItem>? SelectCommand { get; init; }

    public IRelayCommand<ProviderListItem>? EditCommand { get; init; }

    public IRelayCommand<ProviderListItem>? DeleteCommand { get; init; }

    public ObservableCollection<OAuthAccountListItem> OAuthAccounts { get; set; } = [];

    public string ActiveText => IsActive ? I18nService.Current.Translate("providers.active") : "";

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class ProviderTemplateItem : ObservableObject
{
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Description { get; init; } = "";

    public string IconPath { get; init; } = "";

    public IRelayCommand<ProviderTemplateItem>? SelectCommand { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed partial class OAuthAccountListItem : ObservableObject
{
    public string ProviderId { get; init; } = "";

    public string AccountId { get; init; } = "";

    [ObservableProperty]
    private string _displayName = "";

    public string Email { get; init; } = "";

    public bool IsActive { get; init; }

    public string ActiveText => IsActive ? I18nService.Current.Translate("providers.current") : "";

    public IRelayCommand<OAuthAccountListItem>? SelectCommand { get; init; }

    public IRelayCommand<OAuthAccountListItem>? RemoveCommand { get; init; }

    public IRelayCommand<OAuthAccountListItem>? SaveNameCommand { get; init; }
}

public sealed partial class ModelEditorItem : ObservableObject
{
    [ObservableProperty]
    private string _id = "";

    [ObservableProperty]
    private string _displayName = "";

    [ObservableProperty]
    private string _upstreamModel = "";

    [ObservableProperty]
    private ProviderProtocol _protocol = ProviderProtocol.OpenAiResponses;

    [ObservableProperty]
    private string _serviceTier = "";

    [ObservableProperty]
    private bool _fastMode;

    public IRelayCommand<ModelEditorItem>? RemoveCommand { get; init; }

    public ProviderProtocol[] ProtocolOptions { get; } = Enum.GetValues<ProviderProtocol>();
}

public sealed partial class ModelPricingEditorItem : ObservableObject
{
    [ObservableProperty]
    private string _id = "";

    [ObservableProperty]
    private string _aliasesText = "";

    [ObservableProperty]
    private long? _inputTierLimit;

    [ObservableProperty]
    private decimal _inputPrice;

    [ObservableProperty]
    private decimal _inputOverflowPrice;

    [ObservableProperty]
    private decimal _cachedInputPrice;

    [ObservableProperty]
    private decimal _cacheCreationInputPrice;

    [ObservableProperty]
    private long? _outputTierLimit;

    [ObservableProperty]
    private decimal _outputPrice;

    [ObservableProperty]
    private decimal _outputOverflowPrice;

    [ObservableProperty]
    private string _fastMultiplierOverride = "";
}

public sealed class ModelCatalogItem
{
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string AliasesText { get; init; } = "";

    public string ProvidersText { get; init; } = "";

    public string IconPath { get; init; } = "";

    public string IconSlug { get; init; } = "";

    public string InputPriceText { get; init; } = "";

    public string InputTierText { get; init; } = "";

    public string CachedInputPriceText { get; init; } = "";

    public string CacheCreationInputPriceText { get; init; } = "";

    public string OutputPriceText { get; init; } = "";

    public string OutputTierText { get; init; } = "";

    public string FastMultiplierText { get; init; } = "";

    public IRelayCommand<ModelCatalogItem>? EditCommand { get; init; }

    public IRelayCommand<ModelCatalogItem>? DeleteCommand { get; init; }
}

public sealed record UsageMetricItem(
    string Label,
    string Value,
    LucideIconKind Icon,
    IBrush IconForeground,
    IBrush IconBackground);

public sealed class UsageLogItem
{
    public string Time { get; init; } = "";

    public string Provider { get; init; } = "";

    public string Model { get; init; } = "";

    public string Input { get; init; } = "";

    public string CachedInput { get; init; } = "";

    public string CacheCreationInput { get; init; } = "";

    public string Output { get; init; } = "";

    public string Cost { get; init; } = "";

    public string Duration { get; init; } = "";

    public string Status { get; init; } = "";

    public IBrush StatusForeground { get; init; } = Brush.Parse("#86EFAC");

    public IBrush StatusBackground { get; init; } = Brush.Parse("#14351F");

    public IBrush StatusBorder { get; init; } = Brush.Parse("#255E35");

    public static UsageLogItem From(UsageLogRecord record)
    {
        var failed = record.StatusCode >= 400;
        return new UsageLogItem
        {
            Time = record.Timestamp.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture),
            Provider = string.IsNullOrWhiteSpace(record.ProviderId) ? "unknown" : record.ProviderId,
            Model = string.IsNullOrWhiteSpace(record.BilledModel) ? record.RequestModel : record.BilledModel,
            Input = DisplayFormatters.FormatTokenCount(record.Usage.InputTokens),
            CachedInput = DisplayFormatters.FormatTokenCount(record.Usage.CachedInputTokens),
            CacheCreationInput = DisplayFormatters.FormatTokenCount(record.Usage.CacheCreationInputTokens),
            Output = DisplayFormatters.FormatTokenCount(record.Usage.OutputTokens),
            Cost = DisplayFormatters.FormatCost(record.EstimatedCost),
            Duration = record.DurationMs + "ms",
            Status = record.StatusCode.ToString(CultureInfo.InvariantCulture),
            StatusForeground = Brush.Parse(failed ? "#FCA5A5" : "#86EFAC"),
            StatusBackground = Brush.Parse(failed ? "#35191C" : "#14351F"),
            StatusBorder = Brush.Parse(failed ? "#71343B" : "#255E35")
        };
    }
}

public sealed class ProviderUsageItem
{
    public string Provider { get; init; } = "";

    public string Requests { get; init; } = "";

    public string Tokens { get; init; } = "";

    public string Cost { get; init; } = "";

    public string SuccessRate { get; init; } = "";

    public string AverageLatency { get; init; } = "";

    public static ProviderUsageItem From(ProviderUsageSummary summary)
    {
        return new ProviderUsageItem
        {
            Provider = summary.ProviderId,
            Requests = summary.Requests.ToString("N0", CultureInfo.InvariantCulture),
            Tokens = DisplayFormatters.FormatTokenCount(summary.Tokens),
            Cost = DisplayFormatters.FormatCost(summary.Cost),
            SuccessRate = summary.SuccessRate.ToString("P1", CultureInfo.InvariantCulture),
            AverageLatency = summary.AverageLatencyMs + "ms"
        };
    }
}

public sealed class ModelUsageItem
{
    public string Model { get; init; } = "";

    public string Requests { get; init; } = "";

    public string Tokens { get; init; } = "";

    public string Cost { get; init; } = "";

    public string AverageCost { get; init; } = "";

    public static ModelUsageItem From(ModelUsageSummary summary)
    {
        return new ModelUsageItem
        {
            Model = summary.Model,
            Requests = summary.Requests.ToString("N0", CultureInfo.InvariantCulture),
            Tokens = DisplayFormatters.FormatTokenCount(summary.Tokens),
            Cost = DisplayFormatters.FormatCost(summary.Cost),
            AverageCost = DisplayFormatters.FormatCost(summary.AverageCost)
        };
    }
}
