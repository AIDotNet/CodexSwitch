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
    private const string UsageFilterAllValue = "__all__";

    private readonly AppPaths _paths;
    private readonly ConfigurationStore _store;
    private readonly PriceCalculator _priceCalculator;
    private readonly UsageMeter _usageMeter;
    private readonly UsageLogWriter _usageLogWriter;
    private readonly UsageLogReader _usageLogReader;
    private readonly CodexConfigWriter _codexConfigWriter;
    private readonly ClaudeCodeConfigWriter _claudeCodeConfigWriter;
    private readonly I18nService _i18n;
    private HttpClient _sharedHttpClient = null!;
    private IconCacheService _iconCacheService = null!;
    private ProviderAuthService _providerAuthService = null!;
    private ProviderUsageQueryService _providerUsageQueryService = null!;
    private CodexOAuthLoginService _codexOAuthLoginService = null!;
    private readonly StartupRegistrationService _startupRegistrationService;
    private ProxyHostService _proxyHostService = null!;
    private UpdateCheckService _updateCheckService = null!;
    private readonly DispatcherTimer _usageQueryTimer;
    private readonly DispatcherTimer _miniStatusTimer;
    private readonly Dictionary<string, ProviderUsageQueryResult> _providerUsageResults = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _refreshingUsageProviders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ProviderUsageFailureState> _providerUsageFailures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object BrushCacheSync = new();
    private static readonly Dictionary<string, IBrush> BrushCache = new(StringComparer.OrdinalIgnoreCase);
    private AppConfig _config;
    private ModelPricingCatalog _pricing;
    private string _returnPage = "Providers";
    private string? _editingProviderId;
    private string? _editingModelId;
    private ModelCatalogItem? _modelPendingDelete;
    private string? _providerPendingDeleteId;
    private bool _isRefreshingSettingsFields;
    private bool _isLoadingProviderFields;
    private bool _isLoadingClaudeCodeFields;
    private bool _isUpdatingUsageFilterOptions;
    private bool _hasUsageDashboardSnapshot;
    private UsageTimeRange _lastUsageDashboardRange;
    private DateTimeOffset _lastUsageWindowAnchor;
    private UsageLogSourceSnapshot _lastUsageSourceSnapshot;
    private UpdateReleaseAsset? _latestUpdateAsset;

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
    private string _selectedUsageFilterProvider = UsageFilterAllValue;

    [ObservableProperty]
    private string _selectedUsageFilterModel = UsageFilterAllValue;

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
    private bool _selectedSupportsCodex = true;

    [ObservableProperty]
    private bool _selectedSupportsClaudeCode;

    [ObservableProperty]
    private string _claudeCodeModel = "";

    [ObservableProperty]
    private bool _claudeCodeThinkEnabled = true;

    [ObservableProperty]
    private bool _claudeCodeSkipDangerousModePermissionPrompt = true;

    [ObservableProperty]
    private bool _claudeCodeOneMillionContextEnabled;

    [ObservableProperty]
    private bool _selectedUsageQueryEnabled;

    [ObservableProperty]
    private string _selectedUsageQueryTemplateId = UsageQueryTemplateCatalog.CustomTemplateId;

    [ObservableProperty]
    private string _selectedUsageQueryMethod = "GET";

    [ObservableProperty]
    private string _selectedUsageQueryUrl = "";

    [ObservableProperty]
    private string _selectedUsageQueryHeaders = "";

    [ObservableProperty]
    private string _selectedUsageQueryBody = "";

    [ObservableProperty]
    private int _selectedUsageQueryTimeoutSeconds = 20;

    [ObservableProperty]
    private string _selectedUsageQuerySuccessPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryErrorPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryErrorMessagePath = "";

    [ObservableProperty]
    private string _selectedUsageQueryRemainingPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryUnitPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryUnit = "";

    [ObservableProperty]
    private string _selectedUsageQueryTotalPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryUsedPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryUnlimitedPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryPlanNamePath = "";

    [ObservableProperty]
    private string _selectedUsageQueryDailyResetPath = "";

    [ObservableProperty]
    private string _selectedUsageQueryWeeklyResetPath = "";

    [ObservableProperty]
    private string _usageQueryTestResult = "";

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
    private OutboundProxyMode _networkProxyMode = OutboundProxyMode.System;

    [ObservableProperty]
    private string _networkProxyUrl = "";

    [ObservableProperty]
    private bool _networkProxyBypassOnLocal = true;

    [ObservableProperty]
    private bool _preserveCodexAppAuth;

    [ObservableProperty]
    private bool _useFakeCodexAppAuth;

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
    private bool _miniStatusEnabled = true;

    [ObservableProperty]
    private bool _isMiniStatusExpanded;

    [ObservableProperty]
    private string _miniStatusProviderName = "";

    [ObservableProperty]
    private string _miniStatusProviderIconPath = "";

    [ObservableProperty]
    private string _miniStatusRpmText = "0";

    [ObservableProperty]
    private string _miniStatusInputTokensText = "0";

    [ObservableProperty]
    private string _miniStatusOutputTokensText = "0";

    [ObservableProperty]
    private string _miniStatusDailyQuotaText = "--";

    [ObservableProperty]
    private string _miniStatusWeeklyQuotaText = "--";

    [ObservableProperty]
    private string _miniStatusPackageQuotaText = "--";

    [ObservableProperty]
    private bool _miniStatusHasDailyQuota;

    [ObservableProperty]
    private bool _miniStatusHasWeeklyQuota;

    [ObservableProperty]
    private bool _miniStatusHasPackageQuota;

    [ObservableProperty]
    private bool _miniStatusHasQuotaRow;

    [ObservableProperty]
    private bool _miniStatusHasDetails;

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
    private bool _autoUpdateCheckEnabled = true;

    [ObservableProperty]
    private bool _isDownloadingUpdate;

    [ObservableProperty]
    private double _updateDownloadProgress;

    [ObservableProperty]
    private string _updateDownloadProgressText = "";

    [ObservableProperty]
    private string _updatePackageName = "";

    [ObservableProperty]
    private string _downloadedUpdatePath = "";

    [ObservableProperty]
    private bool _hasDownloadedUpdate;

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
        _claudeCodeConfigWriter = new ClaudeCodeConfigWriter(_paths);
        _startupRegistrationService = new StartupRegistrationService();
        SyncStartupRegistrationFromConfig();
        CreateNetworkServices();
        _usageQueryTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
        _usageQueryTimer.Tick += (_, _) => _ = RefreshProviderUsageQueriesAsync();
        _miniStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _miniStatusTimer.Tick += (_, _) => RefreshMiniStatus();
        ProxyStatus = T("proxy.starting");
        LatestVersionTag = T("update.noReleaseYet");
        LatestReleasePublishedAtText = T("update.notPublished");
        UpdateStatusDetails = T("update.checking");

        ClientApps = [];
        ProviderTemplates = [];
        UsageQueryTemplates = [];
        ProviderRows = [];
        ClaudeProviderRows = [];
        ClaudeCodeModelOptions = [];
        ModelRows = [];
        ModelConversionRows = [];
        PricingRows = [];
        ModelCatalogRows = [];
        UsageMetrics = [];
        UsageLogRows = [];
        ProviderUsageRows = [];
        ModelUsageRows = [];
        TrendPoints = [];
        UsageFilterProviderOptions = [];
        UsageFilterModelOptions = [];
        MiniStatusDetails = [];
        MiniStatusMetricCards = [];
        MiniStatusQuotaCards = [];
        ProtocolOptions = Enum.GetValues<ProviderProtocol>();
        UsageQueryMethods = ["GET", "POST"];

        SelectClientAppCommand = new RelayCommand<ClientAppItem>(SelectClientApp);
        ShowProvidersCommand = new RelayCommand(() => CurrentPage = "Providers");
        ShowUsageCommand = new RelayCommand(() => CurrentPage = "Usage");
        ShowModelsCommand = new RelayCommand(() => CurrentPage = "Models");
        OpenSettingsCommand = new RelayCommand(OpenSettings);
        BackFromSettingsCommand = new RelayCommand(BackFromSettings);
        SelectSettingsTabCommand = new RelayCommand<string>(tab => SettingsTab = tab ?? "General");
        SelectUsageTabCommand = new RelayCommand<string>(tab => UsageTab = tab ?? "Requests");
        SelectUsageRangeCommand = new RelayCommand<string>(SelectUsageRange);
        SelectUsageFilterProviderCommand = new RelayCommand<string>(filter => SelectedUsageFilterProvider = NormalizeUsageFilterValue(filter));
        SelectUsageFilterModelCommand = new RelayCommand<string>(filter => SelectedUsageFilterModel = NormalizeUsageFilterValue(filter));
        SelectThemeCommand = new RelayCommand<string>(SelectTheme);
        SelectNetworkProxyModeCommand = new RelayCommand<string>(SelectNetworkProxyMode);
        ToggleProxyCommand = new AsyncRelayCommand(ToggleProxyAsync);
        RestartProxyCommand = new AsyncRelayCommand(RestartProxyAsync);
        StopProxyCommand = new AsyncRelayCommand(StopProxyAsync);
        SelectProviderCommand = new RelayCommand<ProviderListItem>(row => _ = ActivateProviderAsync(row));
        SelectClaudeCodeModelCommand = new RelayCommand<string>(SelectClaudeCodeModel);
        SaveClaudeCodeSettingsCommand = new AsyncRelayCommand(SaveClaudeCodeSettingsAsync);
        EditProviderCommand = new RelayCommand<ProviderListItem>(OpenEditProvider);
        AddProviderCommand = new RelayCommand(OpenAddProvider);
        SelectProviderTemplateCommand = new RelayCommand<ProviderTemplateItem>(SelectProviderTemplate);
        SelectUsageQueryTemplateCommand = new RelayCommand<UsageQueryTemplateItem>(SelectUsageQueryTemplate);
        TestProviderUsageQueryCommand = new AsyncRelayCommand(TestProviderUsageQueryAsync);
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
        AddModelConversionCommand = new RelayCommand(AddModelConversion);
        RemoveModelConversionCommand = new RelayCommand<ModelConversionEditorItem>(RemoveModelConversion);
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
        OpenDownloadedUpdateCommand = new RelayCommand(OpenDownloadedUpdate);

        _usageMeter.Changed += (_, snapshot) => Dispatcher.UIThread.Post(() => ApplySnapshot(snapshot));

        RefreshProviderTemplates();
        RefreshUsageQueryTemplates();
        RefreshClientApps();
        RefreshProviderRows();
        RefreshSettingsFields();
        RefreshPricingRows();
        RefreshModelCatalogRows();
        SelectProvider(ProviderRows.FirstOrDefault(row => row.IsActive) ?? ProviderRows.FirstOrDefault());
        _ = EnsureIconsAsync();
        if (_config.Ui.AutoUpdateCheckEnabled)
            _ = CheckForUpdatesAsync(true);
        else
            UpdateStatusDetails = T("update.autoDisabled");
        _usageQueryTimer.Start();
        _miniStatusTimer.Start();
        RefreshMiniStatus();
        _ = RefreshProviderUsageQueriesAsync();
        _ = _config.Proxy.Enabled
            ? RestartProxyAsync()
            : _proxyHostService.StartAsync(_config);
    }

    public ObservableCollection<ClientAppItem> ClientApps { get; }

    public ObservableCollection<ProviderTemplateItem> ProviderTemplates { get; }

    public ObservableCollection<UsageQueryTemplateItem> UsageQueryTemplates { get; }

    public ObservableCollection<ProviderListItem> ProviderRows { get; }

    public ObservableCollection<ProviderListItem> ClaudeProviderRows { get; }

    public ObservableCollection<string> ClaudeCodeModelOptions { get; }

    public ObservableCollection<ModelEditorItem> ModelRows { get; }

    public ObservableCollection<ModelConversionEditorItem> ModelConversionRows { get; }

    public ObservableCollection<ModelPricingEditorItem> PricingRows { get; }

    public ObservableCollection<ModelCatalogItem> ModelCatalogRows { get; }

    public ObservableCollection<UsageMetricItem> UsageMetrics { get; }

    public ObservableCollection<UsageLogItem> UsageLogRows { get; }

    public ObservableCollection<ProviderUsageItem> ProviderUsageRows { get; }

    public ObservableCollection<ModelUsageItem> ModelUsageRows { get; }

    public ObservableCollection<UsageTrendPoint> TrendPoints { get; }

    public ObservableCollection<UsageFilterOption> UsageFilterProviderOptions { get; }

    public ObservableCollection<UsageFilterOption> UsageFilterModelOptions { get; }

    public ObservableCollection<MiniStatusDetailItem> MiniStatusDetails { get; }

    public ObservableCollection<MiniStatusMetricCardItem> MiniStatusMetricCards { get; }

    public ObservableCollection<MiniStatusQuotaCardItem> MiniStatusQuotaCards { get; }

    public ProviderProtocol[] ProtocolOptions { get; }

    public string[] UsageQueryMethods { get; }

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

    public IRelayCommand<string> SelectUsageFilterProviderCommand { get; }

    public IRelayCommand<string> SelectUsageFilterModelCommand { get; }

    public IRelayCommand<string> SelectThemeCommand { get; }

    public IRelayCommand<string> SelectNetworkProxyModeCommand { get; }

    public IAsyncRelayCommand ToggleProxyCommand { get; }

    public IAsyncRelayCommand RestartProxyCommand { get; }

    public IAsyncRelayCommand StopProxyCommand { get; }

    public IRelayCommand<ProviderListItem> SelectProviderCommand { get; }

    public IRelayCommand<string> SelectClaudeCodeModelCommand { get; }

    public IAsyncRelayCommand SaveClaudeCodeSettingsCommand { get; }

    public IRelayCommand<ProviderListItem> EditProviderCommand { get; }

    public IRelayCommand AddProviderCommand { get; }

    public IRelayCommand<ProviderTemplateItem> SelectProviderTemplateCommand { get; }

    public IRelayCommand<UsageQueryTemplateItem> SelectUsageQueryTemplateCommand { get; }

    public IAsyncRelayCommand TestProviderUsageQueryCommand { get; }

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

    public IRelayCommand AddModelConversionCommand { get; }

    public IRelayCommand<ModelConversionEditorItem> RemoveModelConversionCommand { get; }

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

    public IRelayCommand OpenDownloadedUpdateCommand { get; }

    public async ValueTask DisposeAsync()
    {
        _usageQueryTimer.Stop();
        _miniStatusTimer.Stop();
        _proxyHostService.StateChanged -= OnProxyHostStateChanged;
        await _proxyHostService.DisposeAsync();
        await _usageLogWriter.DisposeAsync();
        _sharedHttpClient.Dispose();
    }

    private void CreateNetworkServices()
    {
        _sharedHttpClient = AppHttpClientFactory.Create(_config.Network);
        _iconCacheService = new IconCacheService(_paths, _sharedHttpClient);
        _providerAuthService = new ProviderAuthService(_store, _config, _sharedHttpClient);
        _providerUsageQueryService = new ProviderUsageQueryService(_sharedHttpClient, _providerAuthService);
        _codexOAuthLoginService = new CodexOAuthLoginService(_sharedHttpClient);
        _updateCheckService = new UpdateCheckService(_sharedHttpClient);
        _proxyHostService = new ProxyHostService(
            _usageMeter,
            _priceCalculator,
            _usageLogWriter,
            _codexConfigWriter,
            _claudeCodeConfigWriter,
            _providerAuthService,
            [
                new OpenAiResponsesAdapter(_sharedHttpClient),
                new OpenAiChatAdapter(_sharedHttpClient),
                new AnthropicMessagesAdapter(_sharedHttpClient)
            ]);
        _proxyHostService.StateChanged += OnProxyHostStateChanged;
    }

    private async Task RecreateNetworkServicesAsync()
    {
        _proxyHostService.StateChanged -= OnProxyHostStateChanged;
        await _proxyHostService.DisposeAsync();
        _sharedHttpClient.Dispose();
        CreateNetworkServices();
    }

    private void OnProxyHostStateChanged(object? sender, ProxyRuntimeState state)
    {
        Dispatcher.UIThread.Post(() => ApplyProxyState(state));
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
        if (!IsProxyAlert)
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

    private void SelectNetworkProxyMode(string? mode)
    {
        if (Enum.TryParse<OutboundProxyMode>(mode, ignoreCase: true, out var parsed))
            NetworkProxyMode = parsed;
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
        if (!IsUsagePageVisible)
            return;

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

            if (result.Status == UpdateCheckStatus.UpdateAvailable)
            {
                if (result.Asset is not null)
                {
                    await DownloadLatestUpdateAsync(result.Asset, silent);
                }
                else
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateStatusDetails = T("update.noCompatibleInstaller");
                        if (!silent)
                            StatusMessage = T("update.noCompatibleInstaller");
                    });
                }
            }
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

    private async Task DownloadLatestUpdateAsync(UpdateReleaseAsset asset, bool silent)
    {
        var started = false;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (IsDownloadingUpdate)
                return;

            started = true;
            _latestUpdateAsset = asset;
            IsDownloadingUpdate = true;
            HasDownloadedUpdate = false;
            DownloadedUpdatePath = "";
            UpdatePackageName = asset.Name;
            UpdateDownloadProgress = 0d;
            UpdateDownloadProgressText = F("update.downloading", asset.Name);
            UpdateStatusDetails = F("update.downloading", asset.Name);
            OnUpdateDownloadDisplayChanged();
        });

        if (!started)
            return;

        try
        {
            var progress = new Progress<UpdateDownloadProgress>(value =>
            {
                Dispatcher.UIThread.Post(() => ApplyUpdateDownloadProgress(value));
            });
            var result = await _updateCheckService.DownloadUpdateAsync(asset, _paths.UpdateDirectory, progress);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                DownloadedUpdatePath = result.FilePath;
                HasDownloadedUpdate = true;
                IsDownloadingUpdate = false;
                UpdateDownloadProgress = 100d;
                UpdateDownloadProgressText = F("update.downloaded", result.FilePath);
                UpdateStatusDetails = F("update.downloaded", result.FilePath);
                if (!silent)
                    StatusMessage = F("update.downloaded", result.FilePath);
                OnUpdateDownloadDisplayChanged();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsDownloadingUpdate = false;
                UpdateDownloadProgressText = F("update.downloadFailed", ex.Message);
                UpdateStatusDetails = F("update.downloadFailed", ex.Message);
                if (!silent)
                    StatusMessage = F("update.downloadFailed", ex.Message);
                OnUpdateDownloadDisplayChanged();
            });
        }
    }

    private void ApplyUpdateDownloadProgress(UpdateDownloadProgress progress)
    {
        UpdateDownloadProgress = progress.Percent;
        var downloaded = DisplayFormatters.FormatByteCount(progress.DownloadedBytes);
        var total = progress.TotalBytes > 0 ? DisplayFormatters.FormatByteCount(progress.TotalBytes) : "-";
        UpdateDownloadProgressText = F("update.downloadProgress", UpdateDownloadProgress.ToString("0", CultureInfo.InvariantCulture), downloaded, total);
        OnUpdateDownloadDisplayChanged();
    }

    private async Task PersistSettingsAsync(string successMessage)
    {
        var networkProxyUrl = NetworkProxyUrl.Trim();
        var networkChanged =
            _config.Network.ProxyMode != NetworkProxyMode ||
            !string.Equals(_config.Network.CustomProxyUrl?.Trim() ?? "", networkProxyUrl, StringComparison.Ordinal) ||
            _config.Network.BypassProxyOnLocal != NetworkProxyBypassOnLocal;

        _config.Proxy.Host = string.IsNullOrWhiteSpace(ProxyListenHost) ? "127.0.0.1" : ProxyListenHost.Trim();
        _config.Proxy.Port = ProxyPort <= 0 ? 12785 : ProxyPort;
        _config.Proxy.InboundApiKey = InboundApiKey.Trim();
        _config.Proxy.Enabled = ProxyEnabled;
        _config.Proxy.PreserveCodexAppAuth = PreserveCodexAppAuth;
        _config.Proxy.UseFakeCodexAppAuth = UseFakeCodexAppAuth;
        _config.Network.ProxyMode = NetworkProxyMode;
        _config.Network.CustomProxyUrl = networkProxyUrl;
        _config.Network.BypassProxyOnLocal = NetworkProxyBypassOnLocal;
        _config.Ui.Language = string.IsNullOrWhiteSpace(UiLanguage) ? _i18n.DefaultLanguageCode : UiLanguage.Trim();
        _config.Ui.Theme = AppThemeService.Normalize(UiTheme);
        UiTheme = _config.Ui.Theme;
        var startupStatusMessage = ApplyStartupRegistrationSetting();
        _config.Ui.StartWithWindows = StartWithWindows;
        _config.Ui.MiniStatusEnabled = MiniStatusEnabled;
        _config.Ui.AutoUpdateCheckEnabled = AutoUpdateCheckEnabled;
        _config.Ui.DefaultApp = DefaultClientAppIsCodex ? ClientAppKind.Codex : ClientAppKind.ClaudeCode;

        _pricing.BillingUnitTokens = BillingUnitTokens <= 0 ? 1_000_000 : BillingUnitTokens;
        _pricing.FastMode.DefaultMultiplier = DefaultFastMultiplier <= 0 ? 1m : DefaultFastMultiplier;
        _pricing.FastMode.ModelOverrides["gpt-5.5*"] = Gpt55FastMultiplier <= 0 ? _pricing.FastMode.DefaultMultiplier : Gpt55FastMultiplier;

        _store.SaveConfig(_config);
        _store.SavePricing(_pricing);
        AppThemeService.Apply(_config.Ui.Theme);
        if (networkChanged)
            await RecreateNetworkServicesAsync();
        RefreshSettingsFields();
        RefreshModelCatalogRows();
        if (_config.Proxy.Enabled)
            await RestartProxyAsync();
        else
            await StopProxyAsync();
        StatusMessage = startupStatusMessage ?? successMessage;
    }

    private void SyncStartupRegistrationFromConfig()
    {
        if (!_startupRegistrationService.IsSupported)
        {
            _config.Ui.StartWithWindows = false;
            return;
        }

        try
        {
            _startupRegistrationService.SetEnabled(_config.Ui.StartWithWindows);
        }
        catch
        {
            _config.Ui.StartWithWindows = ReadStartupRegistrationSetting();
        }
    }

    private bool ReadStartupRegistrationSetting()
    {
        if (!_startupRegistrationService.IsSupported)
            return false;

        try
        {
            return _startupRegistrationService.IsEnabled();
        }
        catch (Exception ex)
        {
            StatusMessage = F("status.startupRegistrationFailed", ex.Message);
            return false;
        }
    }

    private string? ApplyStartupRegistrationSetting()
    {
        if (!_startupRegistrationService.IsSupported)
        {
            if (!StartWithWindows)
                return null;

            StartWithWindows = false;
            return T("status.startupUnsupported");
        }

        try
        {
            _startupRegistrationService.SetEnabled(StartWithWindows);
            return null;
        }
        catch (Exception ex)
        {
            StartWithWindows = ReadStartupRegistrationSetting();
            return F("status.startupRegistrationFailed", ex.Message);
        }
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
        if (row.ClientApp == ClientAppKind.ClaudeCode)
            _config.ActiveClaudeCodeProviderId = row.Id;
        else
        {
            _config.ActiveCodexProviderId = row.Id;
            _config.ActiveProviderId = row.Id;
        }

        _store.SaveConfig(_config);
        RefreshProviderRows();
        SelectProvider(FindProviderRow(row.ClientApp, row.Id));
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
        foreach (var providerRow in ClaudeProviderRows)
            providerRow.IsSelected = string.Equals(providerRow.Id, SelectedProviderId, StringComparison.OrdinalIgnoreCase);

        var provider = FindSelectedProvider();
        if (provider is null)
            return;

        LoadProviderFields(provider);
        RefreshClaudeCodeFields(provider);
    }

    private ProviderListItem? FindProviderRow(ClientAppKind kind, string providerId)
    {
        var rows = kind == ClientAppKind.ClaudeCode ? ClaudeProviderRows : ProviderRows;
        return rows.FirstOrDefault(row => string.Equals(row.Id, providerId, StringComparison.OrdinalIgnoreCase));
    }

    public bool MoveProvider(string providerId, int targetIndex)
    {
        if (string.IsNullOrWhiteSpace(providerId) || _config.Providers.Count < 2)
            return false;

        var currentIndex = IndexOfProvider(providerId);
        if (currentIndex < 0)
            return false;

        targetIndex = Math.Clamp(targetIndex, 0, _config.Providers.Count - 1);
        if (targetIndex == currentIndex)
            return false;

        var provider = _config.Providers[currentIndex];
        var selectedProviderId = SelectedProviderId;
        _config.Providers.RemoveAt(currentIndex);
        if (targetIndex > _config.Providers.Count)
            targetIndex = _config.Providers.Count;

        _config.Providers.Insert(targetIndex, provider);
        _store.SaveConfig(_config);
        _proxyHostService.UpdateConfig(_config);
        RefreshProviderRows();
        SelectProvider(
            ProviderRows.FirstOrDefault(row => string.Equals(row.Id, selectedProviderId, StringComparison.OrdinalIgnoreCase)) ??
            ProviderRows.FirstOrDefault(row => string.Equals(row.Id, _config.ActiveCodexProviderId, StringComparison.OrdinalIgnoreCase)) ??
            ProviderRows.FirstOrDefault());
        return true;
    }

    private int IndexOfProvider(string providerId)
    {
        for (var index = 0; index < _config.Providers.Count; index++)
        {
            if (string.Equals(_config.Providers[index].Id, providerId, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
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

    private void SelectUsageQueryTemplate(UsageQueryTemplateItem? template)
    {
        if (template is null)
            return;

        SelectedUsageQueryTemplateId = template.Id;
        foreach (var item in UsageQueryTemplates)
            item.IsSelected = string.Equals(item.Id, template.Id, StringComparison.OrdinalIgnoreCase);

        LoadUsageQueryFields(UsageQueryTemplateCatalog.CreateQuery(template.Id));
        UsageQueryTestResult = "";
        StatusMessage = template.Id == UsageQueryTemplateCatalog.CustomTemplateId
            ? T("status.usageQueryTemplateCustom")
            : F("status.usageQueryTemplateApplied", template.DisplayName);
    }

    private async Task TestProviderUsageQueryAsync()
    {
        var provider = BuildProviderForUsageQueryTest();
        var result = await _providerUsageQueryService.QueryAsync(provider, CancellationToken.None);
        UsageQueryTestResult = FormatUsageQueryTestResult(result);
        StatusMessage = result.IsSuccess
            ? T("status.usageQueryTestSucceeded")
            : F("status.usageQueryTestFailed", result.Message ?? T("usageQuery.status.invalid"));
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
        provider.SupportsCodex = SelectedSupportsCodex;
        provider.SupportsClaudeCode = SelectedSupportsClaudeCode;
        if (!provider.SupportsCodex && !provider.SupportsClaudeCode)
            provider.SupportsCodex = true;
        provider.OverrideRequestModel = SelectedOverrideModel;
        provider.ServiceTier = string.IsNullOrWhiteSpace(SelectedServiceTier) ? null : SelectedServiceTier.Trim();
        provider.ClaudeCode ??= new ClaudeCodeProviderSettings();
        provider.ClaudeCode.Model = ResolveClaudeCodeModel(provider, ClaudeCodeModel);
        provider.ClaudeCode.AlwaysThinkingEnabled = ClaudeCodeThinkEnabled;
        provider.ClaudeCode.SkipDangerousModePermissionPrompt = ClaudeCodeSkipDangerousModePermissionPrompt;
        provider.ClaudeCode.EnableOneMillionContext = ClaudeCodeOneMillionContextEnabled &&
            ClaudeCodeConfigWriter.IsOneMillionContextModel(provider.ClaudeCode.Model);
        provider.UsageQuery = BuildSelectedUsageQuery();
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

        provider.ModelConversions.Clear();
        foreach (var row in ModelConversionRows)
        {
            if (string.IsNullOrWhiteSpace(row.SourceModel))
                continue;

            var useDefaultModel = row.IsDefault || row.UseDefaultModel;
            provider.ModelConversions.Add(new ModelConversionConfig
            {
                SourceModel = row.IsDefault ? CodexSwitchDefaults.ManagedCodexModel : row.SourceModel.Trim(),
                TargetModel = useDefaultModel || string.IsNullOrWhiteSpace(row.TargetModel) ? null : row.TargetModel.Trim(),
                UseDefaultModel = useDefaultModel,
                Enabled = row.Enabled
            });
        }

        if (isNew)
        {
            _config.Providers.Add(provider);
            if (provider.SupportsCodex && string.IsNullOrWhiteSpace(_config.ActiveCodexProviderId))
            {
                _config.ActiveCodexProviderId = provider.Id;
                _config.ActiveProviderId = provider.Id;
            }
            if (provider.SupportsClaudeCode && string.IsNullOrWhiteSpace(_config.ActiveClaudeCodeProviderId))
                _config.ActiveClaudeCodeProviderId = provider.Id;
        }

        _store.SaveConfig(_config);
        RefreshProviderRows();
        SelectProvider(ProviderRows.FirstOrDefault(row => row.Id == provider.Id) ??
            ClaudeProviderRows.FirstOrDefault(row => row.Id == provider.Id));
        IsProviderDialogOpen = false;
        StatusMessage = isNew ? T("status.providerAdded") : T("status.providerSaved");
        await RefreshProviderUsageQueryAsync(provider.Id);
        if (_config.Proxy.Enabled &&
            (string.Equals(_config.ActiveCodexProviderId, provider.Id, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(_config.ActiveClaudeCodeProviderId, provider.Id, StringComparison.OrdinalIgnoreCase)))
        {
            await RestartProxyAsync();
        }
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
            SelectedSupportsCodex = provider.SupportsCodex;
            SelectedSupportsClaudeCode = provider.SupportsClaudeCode;
            SelectedOverrideModel = provider.OverrideRequestModel;
            SelectedServiceTier = provider.ServiceTier ?? "";
            SelectedFastMode = provider.Cost?.FastMode ?? _config.GlobalCost.FastMode;
            RefreshClaudeCodeFields(provider);
            LoadUsageQueryFields(provider.UsageQuery ?? UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.CustomTemplateId));
            ModelRows.Clear();
            ModelConversionRows.Clear();

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

            var conversions = provider.ModelConversions.ToList();
            if (!conversions.Any(ProviderTemplateCatalog.IsDefaultModelConversion))
            {
                conversions.Insert(0, new ModelConversionConfig
                {
                    SourceModel = CodexSwitchDefaults.ManagedCodexModel,
                    UseDefaultModel = true,
                    Enabled = true
                });
            }

            foreach (var conversion in conversions)
            {
                ModelConversionRows.Add(new ModelConversionEditorItem
                {
                    SourceModel = conversion.SourceModel,
                    TargetModel = conversion.TargetModel ?? "",
                    UseDefaultModel = conversion.UseDefaultModel,
                    Enabled = conversion.Enabled,
                    IsDefault = ProviderTemplateCatalog.IsDefaultModelConversion(conversion),
                    RemoveCommand = RemoveModelConversionCommand
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

    private void RefreshClaudeCodeFields(ProviderConfig? provider = null)
    {
        provider ??= _config.Providers.FirstOrDefault(item =>
            item.SupportsClaudeCode &&
            string.Equals(item.Id, _config.ActiveClaudeCodeProviderId, StringComparison.OrdinalIgnoreCase));

        _isLoadingClaudeCodeFields = true;
        try
        {
            ClaudeCodeModelOptions.Clear();
            if (provider is null)
            {
                ClaudeCodeModel = "";
                ClaudeCodeThinkEnabled = true;
                ClaudeCodeSkipDangerousModePermissionPrompt = true;
                ClaudeCodeOneMillionContextEnabled = false;
                return;
            }

            AddClaudeCodeModelOptions(provider);
            var model = ResolveClaudeCodeModel(provider, provider.ClaudeCode.Model);
            if (provider.ClaudeCode.EnableOneMillionContext &&
                ClaudeCodeConfigWriter.IsOneMillionContextModel(model))
            {
                model += "[1m]";
            }

            if (!ClaudeCodeModelOptions.Contains(model, StringComparer.OrdinalIgnoreCase))
                ClaudeCodeModelOptions.Add(model);

            ClaudeCodeModel = model;
            ClaudeCodeThinkEnabled = provider.ClaudeCode.AlwaysThinkingEnabled;
            ClaudeCodeSkipDangerousModePermissionPrompt = provider.ClaudeCode.SkipDangerousModePermissionPrompt;
            ClaudeCodeOneMillionContextEnabled = provider.ClaudeCode.EnableOneMillionContext &&
                ClaudeCodeConfigWriter.IsOneMillionContextModel(model);
        }
        finally
        {
            _isLoadingClaudeCodeFields = false;
            OnPropertyChanged(nameof(IsClaudeOneMillionContextAvailable));
        }
    }

    private void AddClaudeCodeModelOptions(ProviderConfig provider)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var model in provider.Models)
        {
            AddClaudeCodeModelOption(model.Id, seen);
            if (ClaudeCodeConfigWriter.IsOneMillionContextModel(model.Id))
                AddClaudeCodeModelOption(ClaudeCodeConfigWriter.StripOneMillionSuffix(model.Id) + "[1m]", seen);
        }

        AddClaudeCodeModelOption(provider.DefaultModel, seen);
        AddClaudeCodeModelOption(provider.ClaudeCode.Model, seen);
    }

    private void AddClaudeCodeModelOption(string? model, HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(model))
            return;

        var normalized = model.Trim();
        if (seen.Add(normalized))
            ClaudeCodeModelOptions.Add(normalized);
    }

    private static string ResolveClaudeCodeModel(ProviderConfig provider, string? model)
    {
        var candidate = string.IsNullOrWhiteSpace(model)
            ? provider.DefaultModel
            : model.Trim();
        candidate = ClaudeCodeConfigWriter.StripOneMillionSuffix(candidate);
        if (!string.IsNullOrWhiteSpace(candidate))
            return candidate;

        return provider.Models.FirstOrDefault(route => route.Protocol == ProviderProtocol.AnthropicMessages)?.Id ??
            provider.Models.FirstOrDefault()?.Id ??
            "claude-sonnet-4-5";
    }

    private void SelectClaudeCodeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return;

        ClaudeCodeModel = model.Trim();
    }

    private async Task SaveClaudeCodeSettingsAsync()
    {
        var provider = _config.Providers.FirstOrDefault(item =>
            item.SupportsClaudeCode &&
            string.Equals(item.Id, _config.ActiveClaudeCodeProviderId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return;

        var selectedModel = string.IsNullOrWhiteSpace(ClaudeCodeModel)
            ? provider.ClaudeCode.Model
            : ClaudeCodeModel.Trim();
        var modelRequestedOneMillion = selectedModel.EndsWith("[1m]", StringComparison.OrdinalIgnoreCase);
        var model = ResolveClaudeCodeModel(provider, selectedModel);

        provider.ClaudeCode.Model = model;
        provider.ClaudeCode.AlwaysThinkingEnabled = ClaudeCodeThinkEnabled;
        provider.ClaudeCode.SkipDangerousModePermissionPrompt = ClaudeCodeSkipDangerousModePermissionPrompt;
        provider.ClaudeCode.EnableOneMillionContext =
            (ClaudeCodeOneMillionContextEnabled || modelRequestedOneMillion) &&
            ClaudeCodeConfigWriter.IsOneMillionContextModel(model);

        _store.SaveConfig(_config);
        RefreshProviderRows();
        StatusMessage = T("status.claudeSettingsSaved");
        if (_config.Proxy.Enabled)
            await RestartProxyAsync();
    }

    private void LoadUsageQueryFields(ProviderUsageQueryConfig query)
    {
        var normalized = UsageQueryTemplateCatalog.CloneQuery(query);
        SelectedUsageQueryEnabled = normalized.Enabled;
        SelectedUsageQueryTemplateId = normalized.TemplateId;
        SelectedUsageQueryMethod = normalized.Method;
        SelectedUsageQueryUrl = normalized.Url;
        SelectedUsageQueryHeaders = FormatHeaders(normalized.Headers);
        SelectedUsageQueryBody = normalized.JsonBody ?? "";
        SelectedUsageQueryTimeoutSeconds = normalized.TimeoutSeconds;
        SelectedUsageQuerySuccessPath = normalized.Extractor.SuccessPath ?? "";
        SelectedUsageQueryErrorPath = normalized.Extractor.ErrorPath ?? "";
        SelectedUsageQueryErrorMessagePath = normalized.Extractor.ErrorMessagePath ?? "";
        SelectedUsageQueryRemainingPath = normalized.Extractor.RemainingPath ?? "";
        SelectedUsageQueryUnitPath = normalized.Extractor.UnitPath ?? "";
        SelectedUsageQueryUnit = normalized.Extractor.Unit ?? "";
        SelectedUsageQueryTotalPath = normalized.Extractor.TotalPath ?? "";
        SelectedUsageQueryUsedPath = normalized.Extractor.UsedPath ?? "";
        SelectedUsageQueryUnlimitedPath = normalized.Extractor.UnlimitedPath ?? "";
        SelectedUsageQueryPlanNamePath = normalized.Extractor.PlanNamePath ?? "";
        SelectedUsageQueryDailyResetPath = normalized.Extractor.DailyResetPath ?? "";
        SelectedUsageQueryWeeklyResetPath = normalized.Extractor.WeeklyResetPath ?? "";
        UsageQueryTestResult = "";

        foreach (var item in UsageQueryTemplates)
            item.IsSelected = string.Equals(item.Id, SelectedUsageQueryTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    private ProviderUsageQueryConfig BuildSelectedUsageQuery()
    {
        return new ProviderUsageQueryConfig
        {
            Enabled = SelectedUsageQueryEnabled,
            TemplateId = string.IsNullOrWhiteSpace(SelectedUsageQueryTemplateId)
                ? UsageQueryTemplateCatalog.CustomTemplateId
                : SelectedUsageQueryTemplateId.Trim(),
            Method = string.IsNullOrWhiteSpace(SelectedUsageQueryMethod) ? "GET" : SelectedUsageQueryMethod.Trim().ToUpperInvariant(),
            Url = SelectedUsageQueryUrl.Trim(),
            Headers = ParseHeaders(SelectedUsageQueryHeaders),
            JsonBody = string.IsNullOrWhiteSpace(SelectedUsageQueryBody) ? null : SelectedUsageQueryBody.Trim(),
            TimeoutSeconds = SelectedUsageQueryTimeoutSeconds <= 0 ? 20 : SelectedUsageQueryTimeoutSeconds,
            Extractor = new ProviderUsageExtractorConfig
            {
                SuccessPath = NullIfWhiteSpace(SelectedUsageQuerySuccessPath),
                ErrorPath = NullIfWhiteSpace(SelectedUsageQueryErrorPath),
                ErrorMessagePath = NullIfWhiteSpace(SelectedUsageQueryErrorMessagePath),
                RemainingPath = NullIfWhiteSpace(SelectedUsageQueryRemainingPath),
                UnitPath = NullIfWhiteSpace(SelectedUsageQueryUnitPath),
                Unit = NullIfWhiteSpace(SelectedUsageQueryUnit),
                TotalPath = NullIfWhiteSpace(SelectedUsageQueryTotalPath),
                UsedPath = NullIfWhiteSpace(SelectedUsageQueryUsedPath),
                UnlimitedPath = NullIfWhiteSpace(SelectedUsageQueryUnlimitedPath),
                PlanNamePath = NullIfWhiteSpace(SelectedUsageQueryPlanNamePath),
                DailyResetPath = NullIfWhiteSpace(SelectedUsageQueryDailyResetPath),
                WeeklyResetPath = NullIfWhiteSpace(SelectedUsageQueryWeeklyResetPath)
            }
        };
    }

    private ProviderConfig BuildProviderForUsageQueryTest()
    {
        var provider = FindSelectedProvider() ?? new ProviderConfig();
        return new ProviderConfig
        {
            Id = string.IsNullOrWhiteSpace(provider.Id) ? "preview" : provider.Id,
            BuiltinId = provider.BuiltinId,
            DisplayName = string.IsNullOrWhiteSpace(SelectedProviderName) ? provider.DisplayName : SelectedProviderName.Trim(),
            BaseUrl = SelectedBaseUrl.Trim(),
            ApiKey = SelectedApiKey.Trim(),
            AuthMode = provider.AuthMode,
            ActiveAccountId = provider.ActiveAccountId,
            OAuth = provider.OAuth,
            OAuthAccounts = provider.OAuthAccounts,
            UsageQuery = BuildSelectedUsageQuery()
        };
    }

    private string FormatUsageQueryTestResult(ProviderUsageQueryResult result)
    {
        if (!result.IsSuccess)
            return result.Message ?? T("usageQuery.status.invalid");

        var amount = result.IsUnlimited
            ? T("usageQuery.unlimited")
            : DisplayFormatters.FormatUsageAmount(result.Remaining ?? 0m, result.Unit);
        var lines = new List<string> { F("usageQuery.remaining", amount) };
        var detail = FormatUsageDetail(result);
        if (!string.IsNullOrWhiteSpace(detail))
            lines.Add(detail);
        var reset = FormatResetText(result);
        if (!string.IsNullOrWhiteSpace(reset))
            lines.Add(reset);
        lines.Add(FormatCheckedAt(result.CheckedAt));
        return string.Join(Environment.NewLine, lines);
    }

    private async Task RefreshProviderUsageQueriesAsync()
    {
        var providerIds = _config.Providers
            .Where(provider => provider.UsageQuery?.Enabled == true && HasUsageQueryCredential(provider))
            .Select(provider => provider.Id)
            .ToArray();

        foreach (var providerId in providerIds)
            await RefreshProviderUsageQueryAsync(providerId);
    }

    private async Task RefreshProviderUsageQueryAsync(string providerId)
    {
        var provider = _config.Providers.FirstOrDefault(item =>
            string.Equals(item.Id, providerId, StringComparison.OrdinalIgnoreCase));
        if (provider is null || provider.UsageQuery?.Enabled != true)
        {
            _providerUsageResults.Remove(providerId);
            _refreshingUsageProviders.Remove(providerId);
            _providerUsageFailures.Remove(providerId);
            RefreshProviderRows();
            return;
        }

        if (!HasUsageQueryCredential(provider))
        {
            _providerUsageResults.Remove(providerId);
            _refreshingUsageProviders.Remove(providerId);
            _providerUsageFailures.Remove(providerId);
            RefreshProviderRows();
            return;
        }

        if (ShouldSkipProviderUsageQuery(providerId))
        {
            RefreshProviderRows();
            return;
        }

        if (!_refreshingUsageProviders.Add(providerId))
            return;

        await Dispatcher.UIThread.InvokeAsync(RefreshProviderRows);
        try
        {
            var result = await _providerUsageQueryService.QueryAsync(provider, CancellationToken.None);
            _providerUsageResults[providerId] = result;
            RecordProviderUsageQueryResult(providerId, result);
        }
        finally
        {
            _refreshingUsageProviders.Remove(providerId);
            await Dispatcher.UIThread.InvokeAsync(RefreshProviderRows);
        }
    }

    private bool ShouldSkipProviderUsageQuery(string providerId)
    {
        if (!_providerUsageFailures.TryGetValue(providerId, out var failure))
            return false;

        if (failure.IsSuspended)
            return true;

        return failure.ShouldSkip(DateTimeOffset.Now);
    }

    private void RecordProviderUsageQueryResult(string providerId, ProviderUsageQueryResult result)
    {
        if (result.IsSuccess)
        {
            _providerUsageFailures.Remove(providerId);
            RefreshMiniStatus();
            return;
        }

        if (result.Status is not (ProviderUsageQueryStatus.InvalidResponse or ProviderUsageQueryStatus.RequestFailed))
            return;

        if (!_providerUsageFailures.TryGetValue(providerId, out var failure))
        {
            failure = new ProviderUsageFailureState();
            _providerUsageFailures[providerId] = failure;
        }

        failure.RecordFailure(result.CheckedAt);
        RefreshMiniStatus();
    }

    private static bool HasUsageQueryCredential(ProviderConfig provider)
    {
        var query = provider.UsageQuery;
        if (query is null)
            return false;

        if (provider.AuthMode != ProviderAuthMode.OAuth)
            return !string.IsNullOrWhiteSpace(provider.ApiKey);

        if (!ProviderUsageQueryService.UsesApiKeyPlaceholder(query))
            return true;

        return provider.OAuthAccounts.Any(account =>
            account.IsEnabled &&
            !string.IsNullOrWhiteSpace(account.AccessToken) &&
            (string.IsNullOrWhiteSpace(provider.ActiveAccountId) ||
                string.Equals(account.Id, provider.ActiveAccountId, StringComparison.OrdinalIgnoreCase)));
    }

    private static string FormatHeaders(Dictionary<string, string> headers)
    {
        return string.Join(Environment.NewLine, headers.Select(header => $"{header.Key}: {header.Value}"));
    }

    private static Dictionary<string, string> ParseHeaders(string value)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
                continue;

            var name = line[..separator].Trim();
            var headerValue = line[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(name))
                headers[name] = headerValue;
        }

        return headers;
    }

    private static string? NullIfWhiteSpace(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private void AddModelConversion()
    {
        ModelConversionRows.Add(new ModelConversionEditorItem
        {
            SourceModel = MakeUniqueId("codex-model", ModelConversionRows.Select(row => row.SourceModel)),
            TargetModel = SelectedDefaultModel.Trim(),
            UseDefaultModel = false,
            Enabled = true,
            RemoveCommand = RemoveModelConversionCommand
        });
        StatusMessage = T("status.modelConversionAdded");
    }

    private void RemoveModelConversion(ModelConversionEditorItem? row)
    {
        if (row is null || row.IsDefault)
            return;

        ModelConversionRows.Remove(row);
        StatusMessage = T("status.modelConversionRemoved");
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
            provider.SupportsCodex = true;
            _config.ActiveProviderId = provider.Id;
            _config.ActiveCodexProviderId = provider.Id;
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

        var wasCodexActive = string.Equals(provider.Id, _config.ActiveCodexProviderId, StringComparison.OrdinalIgnoreCase);
        var wasClaudeActive = string.Equals(provider.Id, _config.ActiveClaudeCodeProviderId, StringComparison.OrdinalIgnoreCase);
        _config.Providers.Remove(provider);
        _providerUsageResults.Remove(provider.Id);
        _refreshingUsageProviders.Remove(provider.Id);
        _providerUsageFailures.Remove(provider.Id);
        if (wasCodexActive)
            _config.ActiveCodexProviderId = _config.Providers.FirstOrDefault(item => item.SupportsCodex)?.Id ?? "";
        if (wasClaudeActive)
            _config.ActiveClaudeCodeProviderId = _config.Providers.FirstOrDefault(item => item.SupportsClaudeCode)?.Id ?? "";
        _config.ActiveProviderId = _config.ActiveCodexProviderId;

        _providerPendingDeleteId = null;
        ProviderPendingDeleteName = "";
        IsDeleteProviderDialogOpen = false;
        _store.SaveConfig(_config);
        RefreshProviderRows();
        SelectProvider(ProviderRows.FirstOrDefault(row => row.Id == _config.ActiveCodexProviderId) ?? ProviderRows.FirstOrDefault());
        StatusMessage = T("status.providerRemoved");
        if ((wasCodexActive || wasClaudeActive) && _config.Proxy.Enabled)
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
        _latestUpdateAsset = result.Asset;
        UpdatePackageName = result.Asset?.Name ?? (result.Status == UpdateCheckStatus.UpdateAvailable
            ? T("update.noCompatibleInstaller")
            : "");
        UpdateStatusDetails = result.Status switch
        {
            UpdateCheckStatus.NoRelease => T("update.noRelease"),
            UpdateCheckStatus.UpToDate => T("update.upToDate"),
            UpdateCheckStatus.UpdateAvailable => result.Asset is null ? T("update.noCompatibleInstaller") : T("update.available"),
            UpdateCheckStatus.Failed => F("update.failed", result.Message),
            _ => result.Message ?? T("update.unavailable")
        };

        OnPropertyChanged(nameof(CanOpenLatestRelease));
        OnUpdateDownloadDisplayChanged();
    }

    private void OpenLatestRelease()
    {
        OpenExternalUrl(LatestReleaseUrl);
    }

    private void OpenDownloadedUpdate()
    {
        if (string.IsNullOrWhiteSpace(DownloadedUpdatePath) || !File.Exists(DownloadedUpdatePath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(DownloadedUpdatePath)
            {
                UseShellExecute = true
            });
        }
        catch
        {
        }
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

    private void RefreshUsageQueryTemplates()
    {
        UsageQueryTemplates.Clear();
        foreach (var template in UsageQueryTemplateCatalog.VisibleTemplates)
        {
            UsageQueryTemplates.Add(new UsageQueryTemplateItem
            {
                Id = template.Id,
                DisplayName = template.DisplayName,
                Description = template.Description,
                IsSelected = string.Equals(template.Id, SelectedUsageQueryTemplateId, StringComparison.OrdinalIgnoreCase),
                SelectCommand = SelectUsageQueryTemplateCommand
            });
        }
    }

    private void RefreshProviderRows()
    {
        ConfigurationStore.EnsureValidDefaults(_config);
        ProviderRows.Clear();
        ClaudeProviderRows.Clear();
        foreach (var provider in _config.Providers)
        {
            if (provider.SupportsCodex)
                ProviderRows.Add(CreateProviderRow(provider, ClientAppKind.Codex));
            if (provider.SupportsClaudeCode)
                ClaudeProviderRows.Add(CreateProviderRow(provider, ClientAppKind.ClaudeCode));
        }

        ActiveProviderId = SelectedClientApp == ClientAppKind.ClaudeCode
            ? _config.ActiveClaudeCodeProviderId
            : _config.ActiveCodexProviderId;
        RefreshClaudeCodeFields();
        RefreshMiniStatus();
    }

    private ProviderListItem CreateProviderRow(ProviderConfig provider, ClientAppKind kind)
    {
        var iconSlug = provider.IconSlug ?? (provider.Protocol == ProviderProtocol.AnthropicMessages ? "claude" : "openai");
        var activeAccount = provider.OAuthAccounts.FirstOrDefault(account =>
            string.Equals(account.Id, provider.ActiveAccountId, StringComparison.OrdinalIgnoreCase));
        var usage = CreateProviderUsageDisplay(provider);
        var activeId = kind == ClientAppKind.Codex ? _config.ActiveCodexProviderId : _config.ActiveClaudeCodeProviderId;
        var row = new ProviderListItem
        {
            Id = provider.Id,
            ClientApp = kind,
            DisplayName = string.IsNullOrWhiteSpace(provider.DisplayName) ? provider.Id : provider.DisplayName,
            BaseUrl = provider.BaseUrl,
            IconPath = _iconCacheService.GetIconPath(iconSlug),
            Protocol = provider.Protocol.ToString(),
            DefaultModel = kind == ClientAppKind.ClaudeCode ? provider.ClaudeCode.Model : provider.DefaultModel,
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
            UsageSummary = usage.Summary,
            UsageMeta = usage.Meta,
            UsageResetText = usage.ResetText,
            UsageToolTip = usage.ToolTip,
            HasUsageInfo = usage.HasUsageInfo,
            HasUsageResetText = usage.HasResetText,
            IsUsageRefreshing = usage.IsRefreshing,
            IsUsageError = usage.IsError,
            IsUsageValid = usage.IsValid,
            IsActive = string.Equals(provider.Id, activeId, StringComparison.OrdinalIgnoreCase),
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

        return row;
    }

    private ProviderUsageDisplay CreateProviderUsageDisplay(ProviderConfig provider)
    {
        var enabled = provider.UsageQuery?.Enabled == true;
        var refreshing = _refreshingUsageProviders.Contains(provider.Id);
        if (!enabled || !HasUsageQueryCredential(provider))
            return ProviderUsageDisplay.Hidden;

        if (refreshing)
        {
            return new ProviderUsageDisplay(
                T("usageQuery.status.refreshing"),
                T("usageQuery.status.refreshing"),
                "",
                T("usageQuery.status.refreshing"),
                true,
                false,
                true,
                false,
                false);
        }

        if (!_providerUsageResults.TryGetValue(provider.Id, out var result))
        {
            return new ProviderUsageDisplay(
                T("usageQuery.status.pending"),
                T("usageQuery.status.pending"),
                "",
                T("usageQuery.status.pending"),
                true,
                false,
                false,
                false,
                false);
        }

        if (result.IsSuccess)
        {
            var amount = result.IsUnlimited
                ? T("usageQuery.unlimited")
                : DisplayFormatters.FormatUsageAmount(result.Remaining ?? 0m, result.Unit);
            var summary = F("usageQuery.remaining", amount);
            var metaParts = new List<string> { T("usageQuery.status.valid"), FormatCheckedAt(result.CheckedAt) };
            if (!string.IsNullOrWhiteSpace(result.PlanName))
                metaParts.Insert(0, result.PlanName!);

            var resetText = FormatResetText(result);
            var toolTip = string.Join(Environment.NewLine, new[]
            {
                summary,
                FormatUsageDetail(result),
                resetText,
                FormatCheckedAt(result.CheckedAt)
            }.Where(text => !string.IsNullOrWhiteSpace(text)));

            return new ProviderUsageDisplay(
                summary,
                string.Join(" · ", metaParts),
                resetText,
                toolTip,
                true,
                !string.IsNullOrWhiteSpace(resetText),
                false,
                false,
                true);
        }

        if (result.Status == ProviderUsageQueryStatus.NoSubscription)
        {
            var summary = T("usageQuery.status.noSubscription");
            return new ProviderUsageDisplay(
                summary,
                FormatCheckedAt(result.CheckedAt),
                "",
                result.Message ?? summary,
                true,
                false,
                false,
                false,
                false);
        }

        var status = result.Status == ProviderUsageQueryStatus.RequestFailed
            ? T("usageQuery.status.failed")
            : T("usageQuery.status.invalid");
        var meta = FormatUsageFailureMeta(provider.Id, result);
        var error = string.IsNullOrWhiteSpace(result.Message) ? status : result.Message!;
        return new ProviderUsageDisplay(
            status,
            meta,
            "",
            error,
            true,
            false,
            false,
            true,
            false);
    }

    private string FormatUsageFailureMeta(string providerId, ProviderUsageQueryResult result)
    {
        if (!_providerUsageFailures.TryGetValue(providerId, out var failure))
            return FormatCheckedAt(result.CheckedAt);

        if (failure.IsSuspended)
            return F("usageQuery.status.paused", failure.ConsecutiveFailures);

        var next = failure.NextAttemptAt.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        return F("usageQuery.status.backoff", next, failure.ConsecutiveFailures);
    }

    private void RefreshMiniStatus()
    {
        var activeProviderId = SelectedClientApp == ClientAppKind.ClaudeCode
            ? _config.ActiveClaudeCodeProviderId
            : _config.ActiveCodexProviderId;
        var activeProvider = _config.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Id, activeProviderId, StringComparison.OrdinalIgnoreCase));
        var iconSlug = activeProvider?.IconSlug ??
            (activeProvider?.Protocol == ProviderProtocol.AnthropicMessages ? "claude" : "openai");
        MiniStatusProviderName = activeProvider is null
            ? "CodexSwitch"
            : string.IsNullOrWhiteSpace(activeProvider.DisplayName) ? activeProvider.Id : activeProvider.DisplayName;
        MiniStatusProviderIconPath = _iconCacheService.GetIconPath(iconSlug);

        var realtime = _usageMeter.GetRecentSnapshot(TimeSpan.FromMinutes(1));
        MiniStatusRpmText = realtime.Requests.ToString("N0", CultureInfo.InvariantCulture);
        MiniStatusInputTokensText = DisplayFormatters.FormatTokenCount(realtime.TotalInputTokens);
        MiniStatusOutputTokensText = DisplayFormatters.FormatTokenCount(realtime.TotalOutputTokens);

        var result = activeProvider is null
            ? null
            : _providerUsageResults.GetValueOrDefault(activeProvider.Id);
        var dailyQuota = result?.DailyQuota;
        var weeklyQuota = result?.WeeklyQuota;
        var packageQuota = result?.ResourcePackageQuota;
        MiniStatusHasDailyQuota = HasQuotaDisplay(dailyQuota);
        MiniStatusHasWeeklyQuota = HasQuotaDisplay(weeklyQuota);
        MiniStatusHasPackageQuota = HasQuotaDisplay(packageQuota);
        MiniStatusHasQuotaRow = MiniStatusHasDailyQuota || MiniStatusHasWeeklyQuota || MiniStatusHasPackageQuota;
        MiniStatusDailyQuotaText = dailyQuota is not null && MiniStatusHasDailyQuota ? FormatQuotaCompact(dailyQuota) : "";
        MiniStatusWeeklyQuotaText = weeklyQuota is not null && MiniStatusHasWeeklyQuota ? FormatQuotaCompact(weeklyQuota) : "";
        MiniStatusPackageQuotaText = packageQuota is not null && MiniStatusHasPackageQuota ? FormatQuotaCompact(packageQuota) : "";

        UpdateMiniStatusItems(MiniStatusMetricCards, new[]
        {
            new MiniStatusMetricCardItem("RPM", MiniStatusRpmText, "\u6700\u8fd1 1 \u5206\u949f"),
            new MiniStatusMetricCardItem("\u8f93\u5165", DisplayFormatters.FormatTokenCount(realtime.TotalInputTokens), "Input tokens"),
            new MiniStatusMetricCardItem("\u8f93\u51fa", DisplayFormatters.FormatTokenCount(realtime.TotalOutputTokens), "Output tokens")
        });

        var quotaCards = new List<MiniStatusQuotaCardItem>(3);
        if (dailyQuota is not null && MiniStatusHasDailyQuota)
            quotaCards.Add(CreateQuotaCard("\u4eca\u65e5\u989d\u5ea6", dailyQuota));
        if (weeklyQuota is not null && MiniStatusHasWeeklyQuota)
            quotaCards.Add(CreateQuotaCard("\u672c\u5468\u989d\u5ea6", weeklyQuota));
        if (packageQuota is not null && MiniStatusHasPackageQuota)
            quotaCards.Add(CreateQuotaCard("\u8d44\u6e90\u5305 / Token", packageQuota));
        UpdateMiniStatusItems(MiniStatusQuotaCards, quotaCards);

        var details = new List<MiniStatusDetailItem>(6);
        if (!string.IsNullOrWhiteSpace(result?.PlanName))
            details.Add(new MiniStatusDetailItem("\u5957\u9910", result.PlanName!));

        if (MiniStatusHasDailyQuota && !string.IsNullOrWhiteSpace(result?.DailyReset))
            details.Add(new MiniStatusDetailItem("\u65e5\u91cd\u7f6e", FormatExternalTimeText(result.DailyReset!)));
        if (MiniStatusHasWeeklyQuota && !string.IsNullOrWhiteSpace(result?.WeeklyReset))
            details.Add(new MiniStatusDetailItem("\u5468\u91cd\u7f6e", FormatExternalTimeText(result.WeeklyReset!)));
        if (activeProvider?.Models.Count > 0)
            details.Add(new MiniStatusDetailItem("\u53ef\u7528\u6a21\u578b", FormatMiniStatusModels(activeProvider)));
        if (result?.IsSuccess == true && MiniStatusHasQuotaRow)
            details.Add(new MiniStatusDetailItem("\u66f4\u65b0", FormatFullTime(result.CheckedAt)));
        if (result is { IsSuccess: false } && !string.IsNullOrWhiteSpace(result.Message))
            details.Add(new MiniStatusDetailItem("\u9519\u8bef\u8be6\u60c5", result.Message!));

        UpdateMiniStatusItems(MiniStatusDetails, details);
        MiniStatusHasDetails = details.Count > 0;
    }

    private static void UpdateMiniStatusItems<T>(ObservableCollection<T> collection, IReadOnlyList<T> items)
    {
        var commonCount = Math.Min(collection.Count, items.Count);
        for (var i = 0; i < commonCount; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(collection[i], items[i]))
                collection[i] = items[i];
        }

        while (collection.Count > items.Count)
            collection.RemoveAt(collection.Count - 1);

        for (var i = collection.Count; i < items.Count; i++)
            collection.Add(items[i]);
    }

    public void SaveMiniStatusPosition(double left, double top)
    {
        if (double.IsNaN(left) || double.IsInfinity(left) ||
            double.IsNaN(top) || double.IsInfinity(top))
        {
            return;
        }

        if (_config.Ui.MiniStatusLeft == left && _config.Ui.MiniStatusTop == top)
            return;

        _config.Ui.MiniStatusLeft = left;
        _config.Ui.MiniStatusTop = top;
        _store.SaveConfig(_config);
    }

    public (double? Left, double? Top) GetMiniStatusPosition()
    {
        return (_config.Ui.MiniStatusLeft, _config.Ui.MiniStatusTop);
    }

    private static string FormatQuotaCompact(UsageQuotaSnapshot quota)
    {
        if (quota.IsUnlimited)
            return "\u221e";

        return IsUsd(quota.Unit)
            ? quota.Remaining!.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : FormatCompactAmount(quota.Remaining!.Value);
    }

    private static string FormatQuotaDetail(UsageQuotaSnapshot quota)
    {
        if (quota.IsUnlimited)
            return "\u4e0d\u9650\u91cf";

        var remaining = quota.Remaining is null ? "--" : FormatQuotaAmount(quota.Remaining.Value, quota.Unit);
        var total = quota.Total is null ? "--" : FormatQuotaAmount(quota.Total.Value, quota.Unit);
        var used = quota.Used is null ? null : FormatQuotaAmount(quota.Used.Value, quota.Unit);
        return string.IsNullOrWhiteSpace(used)
            ? $"{remaining} / {total}"
            : $"{remaining} / {total} (\u5df2\u7528 {used})";
    }

    private static bool HasQuotaDisplay(UsageQuotaSnapshot? quota)
    {
        return quota is not null && (quota.IsUnlimited || quota.Remaining is not null);
    }

    private static MiniStatusQuotaCardItem CreateQuotaCard(string title, UsageQuotaSnapshot quota)
    {
        var total = quota.Total;
        var used = quota.Used ?? (quota.Total is not null && quota.Remaining is not null
            ? Math.Max(0m, quota.Total.Value - quota.Remaining.Value)
            : null);
        var percent = total is > 0m && used is not null
            ? (double)Math.Clamp(used.Value / total.Value * 100m, 0m, 100m)
            : 0d;

        return new MiniStatusQuotaCardItem(
            title,
            FormatQuotaCardValue(quota),
            total is null || quota.IsUnlimited ? "" : "/ " + FormatQuotaCardAmount(total.Value, quota.Unit),
            quota.IsUnlimited ? "\u221e" : percent.ToString("0.#", CultureInfo.InvariantCulture) + "%",
            quota.IsUnlimited ? 100d : percent,
            quota.IsUnlimited);
    }

    private static string FormatQuotaCardValue(UsageQuotaSnapshot quota)
    {
        if (quota.IsUnlimited)
            return "\u221e";

        return quota.Remaining is null
            ? "--"
            : FormatQuotaCardAmount(quota.Remaining.Value, quota.Unit);
    }

    private static string FormatQuotaCardAmount(decimal value, string? unit)
    {
        return IsUsd(unit) || IsTokenUnit(unit)
            ? value.ToString(value == decimal.Truncate(value) ? "0.0" : "0.##", CultureInfo.InvariantCulture)
            : FormatQuotaAmount(value, unit);
    }

    private static string FormatMiniStatusModels(ProviderConfig provider)
    {
        var models = provider.Models
            .Select(model => string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName!)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();

        return models.Length == 0
            ? provider.DefaultModel
            : string.Join(", ", models);
    }

    private static string FormatQuotaAmount(decimal value, string? unit)
    {
        return IsUsd(unit)
            ? value.ToString("0.00", CultureInfo.InvariantCulture)
            : DisplayFormatters.FormatUsageAmount(value, unit);
    }

    private static string FormatCompactAmount(decimal value)
    {
        var absolute = Math.Abs(value);
        if (absolute < 1_000m)
            return value == decimal.Truncate(value)
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);

        if (absolute < 1_000_000m)
            return (value / 1_000m).ToString(Math.Abs(value / 1_000m) >= 100m ? "0" : "0.0", CultureInfo.InvariantCulture) + "K";

        if (absolute < 1_000_000_000m)
            return (value / 1_000_000m).ToString(Math.Abs(value / 1_000_000m) >= 100m ? "0" : "0.0", CultureInfo.InvariantCulture) + "M";

        return (value / 1_000_000_000m).ToString(Math.Abs(value / 1_000_000_000m) >= 100m ? "0" : "0.0", CultureInfo.InvariantCulture) + "B";
    }

    private static bool IsUsd(string? unit)
    {
        return string.Equals(unit, "USD", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTokenUnit(string? unit)
    {
        return string.Equals(unit, "tokens", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unit, "token", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatExternalTimeText(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? FormatFullTime(parsed)
            : value;
    }

    private static string FormatFullTime(DateTimeOffset value)
    {
        return value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string FormatCheckedAt(DateTimeOffset checkedAt)
    {
        return checkedAt.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
    }

    private string FormatUsageDetail(ProviderUsageQueryResult result)
    {
        var parts = new List<string>();
        if (result.Used is not null)
            parts.Add(F("usageQuery.used", DisplayFormatters.FormatUsageAmount(result.Used.Value, result.Unit)));
        if (result.Total is not null)
            parts.Add(F("usageQuery.total", DisplayFormatters.FormatUsageAmount(result.Total.Value, result.Unit)));
        if (!string.IsNullOrWhiteSpace(result.Extra))
            parts.Add(result.Extra!);
        return string.Join(" · ", parts);
    }

    private string FormatResetText(ProviderUsageQueryResult result)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(result.DailyReset))
            parts.Add(F("usageQuery.dailyReset", result.DailyReset));
        if (!string.IsNullOrWhiteSpace(result.WeeklyReset))
            parts.Add(F("usageQuery.weeklyReset", result.WeeklyReset));
        return string.Join(" · ", parts);
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
            NetworkProxyMode = _config.Network.ProxyMode;
            NetworkProxyUrl = _config.Network.CustomProxyUrl;
            NetworkProxyBypassOnLocal = _config.Network.BypassProxyOnLocal;
            PreserveCodexAppAuth = _config.Proxy.PreserveCodexAppAuth;
            UseFakeCodexAppAuth = _config.Proxy.UseFakeCodexAppAuth;
            Endpoint = _config.Proxy.Endpoint;
            UiLanguage = _i18n.GetLanguage(_config.Ui.Language).Code;
            SelectedLanguage = _i18n.GetLanguage(UiLanguage);
            UiTheme = AppThemeService.Normalize(_config.Ui.Theme);
            _config.Ui.Theme = UiTheme;
            StartWithWindows = ReadStartupRegistrationSetting();
            _config.Ui.StartWithWindows = StartWithWindows;
            MiniStatusEnabled = _config.Ui.MiniStatusEnabled;
            AutoUpdateCheckEnabled = _config.Ui.AutoUpdateCheckEnabled;
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

    private void RefreshUsageDashboard(bool force = false)
    {
        if (!IsUsagePageVisible)
        {
            UnloadUsageDashboard();
            return;
        }

        var now = DateTimeOffset.Now;
        var sourceSnapshot = _usageLogReader.GetSourceSnapshot(UsageTimeRange, now);
        var windowAnchor = GetUsageWindowAnchor(UsageTimeRange, now);
        if (!force &&
            _hasUsageDashboardSnapshot &&
            _lastUsageDashboardRange == UsageTimeRange &&
            _lastUsageWindowAnchor == windowAnchor &&
            _lastUsageSourceSnapshot == sourceSnapshot)
        {
            OnPropertyChanged(nameof(UsageRangeCaption));
            OnPropertyChanged(nameof(UsageTrendGranularity));
            ApplySnapshot(_usageMeter.Snapshot);
            return;
        }

        var dashboard = _usageLogReader.Read(UsageTimeRange, now);
        _hasUsageDashboardSnapshot = true;
        _lastUsageDashboardRange = UsageTimeRange;
        _lastUsageWindowAnchor = windowAnchor;
        _lastUsageSourceSnapshot = dashboard.SourceSnapshot;

        PopulateUsageFilterOptions(dashboard);

        var providerFilter = IsAllUsageFilter(SelectedUsageFilterProvider) ? null : SelectedUsageFilterProvider;
        var modelFilter = IsAllUsageFilter(SelectedUsageFilterModel) ? null : SelectedUsageFilterModel;
        var filteredDashboard = providerFilter is null && modelFilter is null
            ? dashboard
            : _usageLogReader.Read(UsageTimeRange, now, providerFilter, modelFilter);

        var totalTokens = filteredDashboard.InputTokens +
            filteredDashboard.CachedInputTokens +
            filteredDashboard.CacheCreationInputTokens +
            filteredDashboard.OutputTokens +
            filteredDashboard.ReasoningOutputTokens;
        var cachedTokens = filteredDashboard.CachedInputTokens + filteredDashboard.CacheCreationInputTokens;
        var cacheHitRate = DisplayFormatters.CalculateCacheHitRate(
            filteredDashboard.InputTokens,
            filteredDashboard.CachedInputTokens,
            filteredDashboard.CacheCreationInputTokens);

        UsageMetrics.Clear();
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.requests"),
            filteredDashboard.Requests.ToString("N0", CultureInfo.InvariantCulture),
            LucideIconKind.ChartNoAxesColumnIncreasing,
            "#60A5FA",
            "#1D3B5F"));
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.cost"),
            DisplayFormatters.FormatCost(filteredDashboard.EstimatedCost),
            LucideIconKind.BadgeDollarSign,
            "#34D399",
            "#153B2D"));
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.tokens"),
            DisplayFormatters.FormatTokenCount(totalTokens),
            LucideIconKind.Layers2,
            "#A78BFA",
            "#31254D"));
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.cachedTokens"),
            DisplayFormatters.FormatTokenCount(cachedTokens),
            LucideIconKind.DatabaseZap,
            "#FBBF24",
            "#453516"));
        UsageMetrics.Add(CreateUsageMetric(
            T("usage.metric.cacheHitRate"),
            DisplayFormatters.FormatPercentage(cacheHitRate),
            LucideIconKind.BadgePercent,
            "#22D3EE",
            "#123C46"));

        UsageLogRows.Clear();
        foreach (var record in filteredDashboard.Logs)
            UsageLogRows.Add(UsageLogItem.From(record));

        ProviderUsageRows.Clear();
        foreach (var summary in filteredDashboard.ProviderSummaries)
            ProviderUsageRows.Add(ProviderUsageItem.From(summary));

        ModelUsageRows.Clear();
        foreach (var summary in filteredDashboard.ModelSummaries)
            ModelUsageRows.Add(ModelUsageItem.From(summary));

        TrendPoints.Clear();
        foreach (var point in filteredDashboard.TrendPoints)
            TrendPoints.Add(point);

        OnPropertyChanged(nameof(UsageRangeCaption));
        OnPropertyChanged(nameof(UsageTrendGranularity));
        ApplySnapshot(_usageMeter.Snapshot);
    }
    private void UnloadUsageDashboard()
    {
        if (!_hasUsageDashboardSnapshot &&
            UsageMetrics.Count == 0 &&
            UsageLogRows.Count == 0 &&
            ProviderUsageRows.Count == 0 &&
            ModelUsageRows.Count == 0 &&
            TrendPoints.Count == 0)
        {
            return;
        }

        UsageMetrics.Clear();
        UsageLogRows.Clear();
        ProviderUsageRows.Clear();
        ModelUsageRows.Clear();
        TrendPoints.Clear();
        _hasUsageDashboardSnapshot = false;
        _lastUsageDashboardRange = default;
        _lastUsageWindowAnchor = default;
        _lastUsageSourceSnapshot = default;
    }

    private void PopulateUsageFilterOptions(UsageDashboard dashboard)
    {
        var providerOptions = new List<UsageFilterOption> { CreateAllUsageFilterOption() };
        providerOptions.AddRange(dashboard.ProviderSummaries
            .Select(summary => summary.ProviderId)
            .Where(providerId => !string.IsNullOrWhiteSpace(providerId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(providerId => providerId, StringComparer.OrdinalIgnoreCase)
            .Select(value => new UsageFilterOption(value, value)));

        var modelOptions = new List<UsageFilterOption> { CreateAllUsageFilterOption() };
        modelOptions.AddRange(dashboard.ModelSummaries
            .Select(summary => summary.Model)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
            .Select(value => new UsageFilterOption(value, value)));

        _isUpdatingUsageFilterOptions = true;
        try
        {
            SyncCollection(UsageFilterProviderOptions, providerOptions);
            SyncCollection(UsageFilterModelOptions, modelOptions);

            if (!ContainsUsageFilterValue(providerOptions, SelectedUsageFilterProvider))
                SelectedUsageFilterProvider = UsageFilterAllValue;
            if (!ContainsUsageFilterValue(modelOptions, SelectedUsageFilterModel))
                SelectedUsageFilterModel = UsageFilterAllValue;
        }
        finally
        {
            _isUpdatingUsageFilterOptions = false;
        }
    }

    private UsageFilterOption CreateAllUsageFilterOption()
    {
        return new UsageFilterOption(UsageFilterAllValue, T("usage.filter.all"));
    }

    private static bool ContainsUsageFilterValue(IEnumerable<UsageFilterOption> options, string? value)
    {
        var normalized = NormalizeUsageFilterValue(value);
        return options.Any(option => string.Equals(option.Value, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllUsageFilter(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, UsageFilterAllValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeUsageFilterValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? UsageFilterAllValue : value;
    }

    private static void SyncCollection<T>(ObservableCollection<T> collection, IReadOnlyList<T> desired)
    {
        if (collection.SequenceEqual(desired))
            return;

        collection.Clear();
        foreach (var item in desired)
            collection.Add(item);
    }

    private void RefreshUsageDashboardAfterFilterChange()
    {
        if (_isUpdatingUsageFilterOptions)
            return;

        if (IsUsagePageVisible)
            RefreshUsageDashboard(force: true);
        else
            _hasUsageDashboardSnapshot = false;
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
            GetCachedBrush(foreground),
            GetCachedBrush(background));
    }

    private static IBrush GetCachedBrush(string value)
    {
        lock (BrushCacheSync)
        {
            if (BrushCache.TryGetValue(value, out var brush))
                return brush;

            brush = Brush.Parse(value);
            BrushCache[value] = brush;
            return brush;
        }
    }

    private static DateTimeOffset GetUsageWindowAnchor(UsageTimeRange range, DateTimeOffset now)
    {
        var localNow = now.ToLocalTime();
        return range == UsageTimeRange.Last24Hours
            ? new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0, localNow.Offset)
            : new DateTimeOffset(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, localNow.Offset);
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
        if (!AutoUpdateCheckEnabled)
            UpdateStatusDetails = T("update.autoDisabled");

        if (IsProviderDialogOpen)
            ProviderDialogTitle = string.IsNullOrWhiteSpace(_editingProviderId) ? T("providerDialog.addTitle") : T("providerDialog.editTitle");
        if (IsModelDialogOpen)
            ModelDialogTitle = string.IsNullOrWhiteSpace(_editingModelId) ? T("modelDialog.addTitle") : T("modelDialog.editTitle");

        if (IsUsagePageVisible)
            RefreshUsageDashboard(force: true);
        else
            UnloadUsageDashboard();
        RefreshUsageQueryTemplates();
        RefreshProviderRows();
        RefreshModelCatalogRows();
        OnPropertyChanged(nameof(SupportedLanguages));
        OnPropertyChanged(nameof(WorkspaceTitle));
        OnPropertyChanged(nameof(ServiceToggleText));
        OnPropertyChanged(nameof(ServiceStateText));
        OnPropertyChanged(nameof(UpdateCheckButtonText));
        OnUpdateDownloadDisplayChanged();
        OnPropertyChanged(nameof(PricingUnitText));
        OnPropertyChanged(nameof(ModelCatalogCountText));
    }

    private void ApplyProxyState(ProxyRuntimeState state)
    {
        ProxyStatus = FormatProxyStatus(state);
        Endpoint = state.Endpoint;
        ActiveProviderId = SelectedClientApp == ClientAppKind.ClaudeCode
            ? _config.ActiveClaudeCodeProviderId
            : state.ActiveProviderId;
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
        RefreshMiniStatus();
    }

    private void OnProxyStateDisplayChanged()
    {
        OnPropertyChanged(nameof(IsProxyAlert));
        OnPropertyChanged(nameof(ServiceToggleText));
        OnPropertyChanged(nameof(ServiceStateText));
    }

    private void OnUpdateDownloadDisplayChanged()
    {
        OnPropertyChanged(nameof(IsUpdateDownloadVisible));
        OnPropertyChanged(nameof(CanOpenDownloadedUpdate));
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

    partial void OnSelectedProtocolChanged(ProviderProtocol value)
    {
        if (_isLoadingProviderFields)
            return;

        if (value == ProviderProtocol.AnthropicMessages)
            SelectedSupportsClaudeCode = true;
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

        if (value == "Usage")
            RefreshUsageDashboard();
        else
            UnloadUsageDashboard();
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
        if (IsUsagePageVisible)
            RefreshUsageDashboard();
        else
            _hasUsageDashboardSnapshot = false;
    }

    partial void OnSelectedUsageFilterProviderChanged(string value)
    {
        RefreshUsageDashboardAfterFilterChange();
    }

    partial void OnSelectedUsageFilterModelChanged(string value)
    {
        RefreshUsageDashboardAfterFilterChange();
    }

    partial void OnIsUsageRefreshingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsUsageRefreshIdle));
        OnPropertyChanged(nameof(UsageRefreshButtonText));
    }

    partial void OnIsDownloadingUpdateChanged(bool value)
    {
        OnUpdateDownloadDisplayChanged();
    }

    partial void OnHasDownloadedUpdateChanged(bool value)
    {
        OnUpdateDownloadDisplayChanged();
    }

    partial void OnDownloadedUpdatePathChanged(string value)
    {
        OnUpdateDownloadDisplayChanged();
    }

    partial void OnSelectedClientAppChanged(ClientAppKind value)
    {
        RefreshClientApps();
        ActiveProviderId = value == ClientAppKind.ClaudeCode
            ? _config.ActiveClaudeCodeProviderId
            : _config.ActiveCodexProviderId;
    }

    partial void OnClaudeCodeModelChanged(string value)
    {
        OnPropertyChanged(nameof(IsClaudeOneMillionContextAvailable));
        if (!_isLoadingClaudeCodeFields && !IsClaudeOneMillionContextAvailable)
            ClaudeCodeOneMillionContextEnabled = false;
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
        if (IsUsagePageVisible)
            RefreshUsageDashboard(force: true);
        if (!_isRefreshingSettingsFields)
            _store.SaveConfig(_config);
    }

    partial void OnUiThemeChanged(string value)
    {
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
        OnPropertyChanged(nameof(IsSystemThemeSelected));
    }

    partial void OnNetworkProxyModeChanged(OutboundProxyMode value)
    {
        OnPropertyChanged(nameof(IsSystemNetworkProxySelected));
        OnPropertyChanged(nameof(IsCustomNetworkProxySelected));
        OnPropertyChanged(nameof(IsDisabledNetworkProxySelected));
    }

    partial void OnBillingUnitTokensChanged(long value)
    {
        OnPropertyChanged(nameof(PricingUnitText));
    }

    partial void OnPricingCurrencyChanged(string value)
    {
        OnPropertyChanged(nameof(PricingUnitText));
    }

    partial void OnPreserveCodexAppAuthChanged(bool value)
    {
        if (!_isRefreshingSettingsFields && value)
            UseFakeCodexAppAuth = false;
    }

    partial void OnUseFakeCodexAppAuthChanged(bool value)
    {
        if (!_isRefreshingSettingsFields && value)
            PreserveCodexAppAuth = false;
    }

    partial void OnMiniStatusEnabledChanged(bool value)
    {
        if (_isRefreshingSettingsFields)
            return;

        _config.Ui.MiniStatusEnabled = value;
        _store.SaveConfig(_config);
    }

    partial void OnAutoUpdateCheckEnabledChanged(bool value)
    {
        if (_isRefreshingSettingsFields)
            return;

        _config.Ui.AutoUpdateCheckEnabled = value;
        _store.SaveConfig(_config);
        if (value)
            _ = CheckForUpdatesAsync(true);
        else
            UpdateStatusDetails = T("update.autoDisabled");
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

    public bool IsSystemNetworkProxySelected => NetworkProxyMode == OutboundProxyMode.System;

    public bool IsCustomNetworkProxySelected => NetworkProxyMode == OutboundProxyMode.Custom;

    public bool IsDisabledNetworkProxySelected => NetworkProxyMode == OutboundProxyMode.Disabled;

    public bool CanOpenLatestRelease => !string.IsNullOrWhiteSpace(LatestReleaseUrl);

    public bool IsUpdateDownloadVisible => IsDownloadingUpdate ||
        HasDownloadedUpdate ||
        _latestUpdateAsset is not null ||
        !string.IsNullOrWhiteSpace(UpdatePackageName);

    public bool CanOpenDownloadedUpdate => HasDownloadedUpdate &&
        !string.IsNullOrWhiteSpace(DownloadedUpdatePath) &&
        File.Exists(DownloadedUpdatePath);

    public bool IsStartWithWindowsSupported => _startupRegistrationService.IsSupported;

    public string CodexIconPath => _iconCacheService.GetIconPath("codex-color");

    public string ClaudeCodeIconPath => _iconCacheService.GetIconPath("claudecode-color");

    public bool IsClaudeOneMillionContextAvailable => ClaudeCodeConfigWriter.IsOneMillionContextModel(ClaudeCodeModel);

    public string UpdateCheckButtonText => IsCheckingForUpdates ? T("update.checking") : T("settings.version.checkNow");

    public string RepositoryUrl => AppReleaseInfo.RepositoryUrl;

    public string ReleasesPageUrl => AppReleaseInfo.ReleasesUrl;

    public string AppDataRootPath => _paths.RootDirectory;

    public string CodexConfigFilePath => _paths.CodexConfigPath;

    public string CodexAuthFilePath => _paths.CodexAuthPath;

    public string ClaudeSettingsFilePath => _paths.ClaudeSettingsPath;

    public string UsageLogFilePath => _paths.UsageLogDirectory;

    public bool IsProxyAlert => !_config.Proxy.Enabled ||
        _proxyHostService.State.Error is not null ||
        (!_proxyHostService.State.IsRunning &&
            !string.Equals(_proxyHostService.State.StatusText, "Starting", StringComparison.Ordinal));

    public string ServiceToggleText => IsProxyAlert ? T("common.start") : T("common.stop");

    public string ServiceStateText => _proxyHostService.State.StatusText switch
    {
        "Starting" => T("status.proxyStarting"),
        "Running" => T("status.proxyRunning"),
        _ => T("status.proxyStopped")
    };

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

public sealed record UsageFilterOption(string Value, string DisplayName);

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

    public ClientAppKind ClientApp { get; set; } = ClientAppKind.Codex;

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

    public string UsageSummary { get; set; } = "";

    public string UsageMeta { get; set; } = "";

    public string UsageResetText { get; set; } = "";

    public string UsageToolTip { get; set; } = "";

    public bool HasUsageInfo { get; set; }

    public bool HasUsageResetText { get; set; }

    public bool IsUsageRefreshing { get; set; }

    public bool IsUsageError { get; set; }

    public bool IsUsageValid { get; set; }

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

public sealed partial class UsageQueryTemplateItem : ObservableObject
{
    public string Id { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Description { get; init; } = "";

    public IRelayCommand<UsageQueryTemplateItem>? SelectCommand { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed record ProviderUsageDisplay(
    string Summary,
    string Meta,
    string ResetText,
    string ToolTip,
    bool HasUsageInfo,
    bool HasResetText,
    bool IsRefreshing,
    bool IsError,
    bool IsValid)
{
    public static ProviderUsageDisplay Hidden { get; } = new("", "", "", "", false, false, false, false, false);
}

public sealed class ProviderUsageFailureState
{
    public const int MaxFailuresBeforeSuspend = 5;

    public int ConsecutiveFailures { get; set; }

    public DateTimeOffset LastFailureAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public bool IsSuspended { get; set; }

    public bool ShouldSkip(DateTimeOffset now)
    {
        return IsSuspended || now < NextAttemptAt;
    }

    public void RecordFailure(DateTimeOffset checkedAt)
    {
        ConsecutiveFailures++;
        LastFailureAt = checkedAt;
        IsSuspended = ConsecutiveFailures >= MaxFailuresBeforeSuspend;
        NextAttemptAt = IsSuspended
            ? DateTimeOffset.MaxValue
            : checkedAt.AddMinutes(10 * (ConsecutiveFailures + 1));
    }
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

public sealed partial class ModelConversionEditorItem : ObservableObject
{
    [ObservableProperty]
    private string _sourceModel = "";

    [ObservableProperty]
    private string _targetModel = "";

    [ObservableProperty]
    private bool _useDefaultModel;

    [ObservableProperty]
    private bool _enabled = true;

    public bool IsDefault { get; init; }

    public bool CanRemove => !IsDefault;

    public bool CanEditSource => !IsDefault;

    public bool CanEditUseDefaultModel => !IsDefault;

    public bool CanEditTarget => !UseDefaultModel;

    public IRelayCommand<ModelConversionEditorItem>? RemoveCommand { get; init; }

    partial void OnUseDefaultModelChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditTarget));
    }
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

public sealed record MiniStatusDetailItem(string Label, string Value);

public sealed record MiniStatusMetricCardItem(string Label, string Value, string Caption);

public sealed record MiniStatusQuotaCardItem(
    string Title,
    string Remaining,
    string Total,
    string PercentText,
    double Percent,
    bool IsUnlimited);

public sealed class UsageLogItem
{
    private static readonly IBrush SuccessStatusForeground = Brush.Parse("#86EFAC");
    private static readonly IBrush SuccessStatusBackground = Brush.Parse("#14351F");
    private static readonly IBrush SuccessStatusBorder = Brush.Parse("#255E35");
    private static readonly IBrush FailedStatusForeground = Brush.Parse("#FCA5A5");
    private static readonly IBrush FailedStatusBackground = Brush.Parse("#35191C");
    private static readonly IBrush FailedStatusBorder = Brush.Parse("#71343B");

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

    public IBrush StatusForeground { get; init; } = SuccessStatusForeground;

    public IBrush StatusBackground { get; init; } = SuccessStatusBackground;

    public IBrush StatusBorder { get; init; } = SuccessStatusBorder;

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
            StatusForeground = failed ? FailedStatusForeground : SuccessStatusForeground,
            StatusBackground = failed ? FailedStatusBackground : SuccessStatusBackground,
            StatusBorder = failed ? FailedStatusBorder : SuccessStatusBorder
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
