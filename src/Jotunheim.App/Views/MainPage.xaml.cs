using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Jotunheim.App.Models;
using Jotunheim.App.Services;
using Jotunheim.App.Utils;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Diagnostics;

namespace Jotunheim.App.Views;

public sealed partial class MainPage : Page, INotifyPropertyChanged
{
    private const string KeyUseUtm = "CoordinateMode.UseUtm";
    private const string KeyShowLatLon = "CoordinateMode.ShowLatLon";

    private readonly Sgp4Service _sgp4Service = new();
    private readonly SpaceTrackClient _spaceTrackClient = new();
    private readonly CredentialStore _credentialStore = new();
    private readonly GroundStationStore _groundStationStore = new();
    private readonly DispatcherTimer _timer;

    private SatelliteInfo? _selectedSatellite;
    private GroundStation? _groundStation;
    private bool _useUtm;
    private bool _mapReady;
    private bool _mapContentReady;
    private bool _showLatLon;
    private bool _showTrack = true;
    private bool _showFutureTrack = true;
    private DateTime _lastTrackUpdateUtc = DateTime.MinValue;

    private const int TrackMinutes = 90;
    private const int TrackStepSeconds = 60;

    private string _selectedName = "No satellite selected";
    private string _selectedMeta = "Import a TLE to begin.";
    private string _groundStationSummary = "Not set";
    private string _coordModeLabel = "Local ENU";
    private string _coordLabel1 = "Easting (km)";
    private string _coordLabel2 = "Northing (km)";
    private string _coordLabel3 = "Up (km)";
    private string _eastingText = "-";
    private string _northingText = "-";
    private string _upText = "-";
    private string _speedText = "-";
    private string _positionText = "-";
    private string _velocityText = "-";
    private string _lastUpdatedText = "Last updated: -";
    private string _statusMessage = string.Empty;
    private string _debugLogText = string.Empty;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private bool _isStatusOpen;
    private bool _isBusy;

    public MainPage()
    {
        InitializeComponent();
        AppLogger.EnsureExists();
        LogInfo("MainPage initialized.");
        try
        {
            _sgp4Service.Initialize();
        }
        catch (Exception ex)
        {
            LogWarn($"SGP4 library initialization failed: {ex.Message}");
            SetStatus($"SGP4 library initialization failed: {ex.Message}", InfoBarSeverity.Error);
        }

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        Loaded += OnPageLoaded;
    }

    public ObservableCollection<SatelliteInfo> Satellites { get; } = new();
    public ObservableCollection<GroundStation> Stations { get; } = new();

    public SatelliteInfo? SelectedSatellite
    {
        get => _selectedSatellite;
        set
        {
            if (_selectedSatellite == value)
            {
                return;
            }

            _selectedSatellite = value;
            OnPropertyChanged();
            UpdateSelectedDisplay();
        }
    }

    public GroundStation? SelectedStation
    {
        get => _groundStation;
        set
        {
            if (_groundStation == value)
            {
                return;
            }

            _groundStation = value;
            OnPropertyChanged();
            _groundStationStore.SaveSelectedName(_groundStation?.Name);
            GroundStationSummary = _groundStation?.Summary ?? "Not set";
            UpdateCoordHeaders();
            ResetLiveFields();
            UpdateMapStation();
        }
    }

    public string SelectedName
    {
        get => _selectedName;
        private set => SetField(ref _selectedName, value);
    }

    public string SelectedMeta
    {
        get => _selectedMeta;
        private set => SetField(ref _selectedMeta, value);
    }

    public string GroundStationSummary
    {
        get => _groundStationSummary;
        private set => SetField(ref _groundStationSummary, value);
    }

    public string CoordModeLabel
    {
        get => _coordModeLabel;
        private set => SetField(ref _coordModeLabel, value);
    }

    public string CoordLabel1
    {
        get => _coordLabel1;
        private set => SetField(ref _coordLabel1, value);
    }

    public string CoordLabel2
    {
        get => _coordLabel2;
        private set => SetField(ref _coordLabel2, value);
    }

    public string CoordLabel3
    {
        get => _coordLabel3;
        private set => SetField(ref _coordLabel3, value);
    }

    public string EastingText
    {
        get => _eastingText;
        private set => SetField(ref _eastingText, value);
    }

    public string NorthingText
    {
        get => _northingText;
        private set => SetField(ref _northingText, value);
    }

    public string UpText
    {
        get => _upText;
        private set => SetField(ref _upText, value);
    }

    public string SpeedText
    {
        get => _speedText;
        private set => SetField(ref _speedText, value);
    }

    public string PositionText
    {
        get => _positionText;
        private set => SetField(ref _positionText, value);
    }

    public string VelocityText
    {
        get => _velocityText;
        private set => SetField(ref _velocityText, value);
    }

    public string LastUpdatedText
    {
        get => _lastUpdatedText;
        private set => SetField(ref _lastUpdatedText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    public string DebugLogText
    {
        get => _debugLogText;
        private set => SetField(ref _debugLogText, value);
    }

    public InfoBarSeverity StatusSeverity
    {
        get => _statusSeverity;
        private set => SetField(ref _statusSeverity, value);
    }

    public bool IsStatusOpen
    {
        get => _isStatusOpen;
        private set => SetField(ref _isStatusOpen, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(BusyLabel));
            }
        }
    }

    public string BusyLabel => IsBusy ? "Fetching Space-Track data..." : "Idle";

    public bool ShowLatLon
    {
        get => _showLatLon;
        set
        {
            if (SetField(ref _showLatLon, value))
            {
                LocalSettingsStore.SetBool(KeyShowLatLon, _showLatLon);
                UpdateCoordHeaders();
                ResetLiveFields();
            }
        }
    }

    public bool ShowTrack
    {
        get => _showTrack;
        set
        {
            if (SetField(ref _showTrack, value))
            {
                UpdateMapTrack(DateTime.UtcNow, force: true);
            }
        }
    }

    public bool ShowFutureTrack
    {
        get => _showFutureTrack;
        set
        {
            if (SetField(ref _showFutureTrack, value))
            {
                UpdateMapTrack(DateTime.UtcNow, force: true);
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnTimerTick(object? sender, object e)
    {
        if (SelectedSatellite is null)
        {
            return;
        }

        try
        {
            var state = _sgp4Service.Propagate(SelectedSatellite, DateTime.UtcNow);
            UpdateLiveState(state);
        }
        catch (Exception ex)
        {
            LogWarn($"Propagation error: {ex.Message}");
            SetStatus($"Propagation error: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void UpdateSelectedDisplay()
    {
        if (SelectedSatellite is null)
        {
            SelectedName = "No satellite selected";
            SelectedMeta = "Import a TLE to begin.";
            ResetLiveFields();
            return;
        }

        SelectedName = SelectedSatellite.Name;
        SelectedMeta = SelectedSatellite.DisplayId;
        ResetLiveFields();
        _lastTrackUpdateUtc = DateTime.MinValue;
        UpdateMapTrack(DateTime.UtcNow, force: true);
    }

    private void UpdateLiveState(Sgp4State state)
    {
        var speed = Math.Sqrt(
            state.VelX * state.VelX +
            state.VelY * state.VelY +
            state.VelZ * state.VelZ);
        SpeedText = $"{speed:0.000} km/s";

        PositionText = $"{state.PosX:0.000}, {state.PosY:0.000}, {state.PosZ:0.000}";
        VelocityText = $"{state.VelX:0.000}, {state.VelY:0.000}, {state.VelZ:0.000}";
        LastUpdatedText = $"Last updated: {state.TimestampUtc:yyyy-MM-dd HH:mm:ss} UTC";

        UpdateMapPosition(state);
        UpdateMapTrack(state.TimestampUtc);

        if (_showLatLon)
        {
            EastingText = $"{state.LatDeg:0.0000}°";
            NorthingText = $"{state.LonDeg:0.0000}°";
            UpText = $"{state.AltKm:0.000} km";
            CoordModeLabel = "Geodetic (lat/lon)";
        }
        else if (_useUtm)
        {
            if (GeoMath.TryLatLonToUtm(state.LatDeg, state.LonDeg, out var utm))
            {
                var altMeters = state.AltKm * 1000.0;
                EastingText = $"{utm.EastingMeters:0.0} m";
                NorthingText = $"{utm.NorthingMeters:0.0} m";
                UpText = $"{altMeters:0.0} m";
                CoordModeLabel = $"UTM Zone {utm.Zone}{utm.Hemisphere}";
            }
            else
            {
                EastingText = "-";
                NorthingText = "-";
                UpText = "-";
                CoordModeLabel = "UTM (out of range)";
            }
        }
        else
        {
            if (_groundStation is null)
            {
                EastingText = "-";
                NorthingText = "-";
                UpText = "-";
                return;
            }

            var stationAltKm = _groundStation.AltitudeMeters / 1000.0;
            var stationEcef = GeoMath.GeodeticToEcef(_groundStation.LatitudeDeg, _groundStation.LongitudeDeg, stationAltKm);
            var satEcef = GeoMath.GeodeticToEcef(state.LatDeg, state.LonDeg, state.AltKm);

            var dx = satEcef.X - stationEcef.X;
            var dy = satEcef.Y - stationEcef.Y;
            var dz = satEcef.Z - stationEcef.Z;

            var enu = GeoMath.EcefToEnu(dx, dy, dz, _groundStation.LatitudeDeg, _groundStation.LongitudeDeg);

            EastingText = $"{enu.East:0.000} km";
            NorthingText = $"{enu.North:0.000} km";
            UpText = $"{enu.Up:0.000} km";
            CoordModeLabel = $"Local ENU • {_groundStation.Name}";
        }
    }

    private void ResetLiveFields()
    {
        EastingText = "-";
        NorthingText = "-";
        UpText = "-";
        SpeedText = "-";
        PositionText = "-";
        VelocityText = "-";
        LastUpdatedText = "Last updated: -";
    }

    private void UpdateCoordHeaders()
    {
        if (_showLatLon)
        {
            CoordLabel1 = "Latitude (deg)";
            CoordLabel2 = "Longitude (deg)";
            CoordLabel3 = "Altitude (km)";
            CoordModeLabel = "Geodetic (lat/lon)";
        }
        else if (_useUtm)
        {
            CoordLabel1 = "Easting (m)";
            CoordLabel2 = "Northing (m)";
            CoordLabel3 = "Altitude (m)";
            CoordModeLabel = "UTM (global)";
        }
        else
        {
            CoordLabel1 = "Easting (km)";
            CoordLabel2 = "Northing (km)";
            CoordLabel3 = "Up (km)";
            CoordModeLabel = _groundStation is null ? "Local ENU (station not set)" : $"Local ENU • {_groundStation.Name}";
        }

        UpdateMapMode();
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }

    private void OnImportTles(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        => ImportTlesFromText(TleInputTextBox.Text, "Manual import");

    private void OnClearSatellites(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Satellites.Clear();
        _sgp4Service.Clear();
        SelectedSatellite = null;
        SetStatus("Cleared satellites.", InfoBarSeverity.Informational);
    }

    private async void OnFetchFromSpaceTrack(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var username = SpaceTrackUserTextBox.Text.Trim();
        var password = SpaceTrackPasswordBox.Password;
        var idText = SpaceTrackNoradTextBox.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Space-Track credentials are required.", InfoBarSeverity.Warning);
            return;
        }

        var ids = ParseNoradIds(idText);
        if (ids.Count == 0)
        {
            SetStatus("Enter one or more NORAD IDs (comma or space separated).", InfoBarSeverity.Warning);
            return;
        }

        IsBusy = true;
        try
        {
            await _spaceTrackClient.LoginAsync(username, password, CancellationToken.None);
            if (RememberCredentialsToggle.IsOn)
            {
                _credentialStore.Save(username, password);
            }

            var tleData = await _spaceTrackClient.FetchLatestTlesByNoradAsync(ids, CancellationToken.None);
            ImportTlesFromText(tleData, "Space-Track");
        }
        catch (Exception ex)
        {
            LogWarn($"Space-Track error: {ex.Message}");
            SetStatus($"Space-Track error: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void OnPickTleFile(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add(".tle");
            picker.FileTypeFilter.Add(".dat");
            InitializeWithWindow.Initialize(picker, WindowHelper.GetMainWindowHandle());

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var content = await FileIO.ReadTextAsync(file);
            ImportTlesFromText(content, $"File {file.Name}");
        }
        catch (Exception ex)
        {
            LogWarn($"File import failed: {ex.Message}");
            SetStatus($"File import failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void OnRememberCredentialsToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!RememberCredentialsToggle.IsOn)
        {
            _credentialStore.Clear();
        }
    }

    private void OnForgetCredentials(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _credentialStore.Clear();
        RememberCredentialsToggle.IsOn = false;
        SpaceTrackPasswordBox.Password = string.Empty;
        SetStatus("Saved credentials removed.", InfoBarSeverity.Informational);
    }

    private async void OnAddOrEditStation(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var existing = SelectedStation;
        var station = await PromptForStationAsync(existing);
        if (station is null)
        {
            return;
        }

        if (existing is not null && !string.Equals(existing.Name, station.Name, StringComparison.OrdinalIgnoreCase))
        {
            Stations.Remove(existing);
        }

        var duplicate = Stations.FirstOrDefault(item => string.Equals(item.Name, station.Name, StringComparison.OrdinalIgnoreCase));
        if (duplicate is not null)
        {
            Stations.Remove(duplicate);
        }

        Stations.Add(station);
        SelectedStation = station;
        _groundStationStore.SaveAll(Stations);
        SetStatus("Ground station saved.", InfoBarSeverity.Success);
    }

    private void OnRemoveStation(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (SelectedStation is null)
        {
            return;
        }

        var toRemove = SelectedStation;
        Stations.Remove(toRemove);
        _groundStationStore.SaveAll(Stations);
        SelectedStation = Stations.FirstOrDefault();
        SetStatus("Ground station removed.", InfoBarSeverity.Informational);
    }

    private void OnCoordinateModeToggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _useUtm = UtmToggle.IsOn;
        LocalSettingsStore.SetBool(KeyUseUtm, _useUtm);
        UpdateCoordHeaders();
        ResetLiveFields();
    }

    private void ImportTlesFromText(string raw, string source)
    {
        var entries = TleParser.Parse(raw);
        if (entries.Count == 0)
        {
            SetStatus("No valid TLEs were found.", InfoBarSeverity.Warning);
            return;
        }

        var added = 0;
        foreach (var entry in entries)
        {
            try
            {
                var info = _sgp4Service.Upsert(entry);
                AddOrReplaceSatellite(info);
                added++;
            }
            catch (Exception ex)
            {
                LogWarn($"TLE import failed: {ex.Message}");
                SetStatus($"TLE import failed: {ex.Message}", InfoBarSeverity.Error);
            }
        }

        if (added > 0)
        {
            SetStatus($"Imported {added} satellite(s) from {source}.", InfoBarSeverity.Success);
        }
    }

    private void AddOrReplaceSatellite(SatelliteInfo info)
    {
        var existing = Satellites.FirstOrDefault(sat => sat.NoradId.HasValue && sat.NoradId == info.NoradId);
        if (existing is not null)
        {
            Satellites.Remove(existing);
        }

        Satellites.Add(info);
    }

    private static List<int> ParseNoradIds(string raw)
    {
        var ids = new List<int>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ids;
        }

        var tokens = raw
            .Split(new[] { ',', ' ', ';', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (int.TryParse(token.Trim(), out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private async void OnPageLoaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            AppLogger.EnsureExists();
            LogInfo("App started.");
            if (_credentialStore.TryLoad(out var username, out var password))
            {
                SpaceTrackUserTextBox.Text = username;
                SpaceTrackPasswordBox.Password = password;
                RememberCredentialsToggle.IsOn = true;
            }

            _useUtm = LoadUseUtmPreference();
            _showLatLon = LocalSettingsStore.GetBool(KeyShowLatLon, false);
            UtmToggle.IsOn = _useUtm;
            UpdateCoordHeaders();

            RefreshLog();
            LogInfo("Initializing map...");
            await InitializeMapAsync();
            LogInfo("Map initialization complete.");
            await LoadStationsAsync();
            LogInfo("Stations loaded.");
        }
        catch (Exception ex)
        {
            LogWarn($"OnPageLoaded failed: {ex}");
            SetStatus($"Startup error: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async Task LoadStationsAsync()
    {
        Stations.Clear();
        foreach (var station in _groundStationStore.LoadAll())
        {
            Stations.Add(station);
        }

        var selectedName = _groundStationStore.LoadSelectedName();
        if (!string.IsNullOrWhiteSpace(selectedName))
        {
            SelectedStation = Stations.FirstOrDefault(item => string.Equals(item.Name, selectedName, StringComparison.OrdinalIgnoreCase));
        }

        if (SelectedStation is null && Stations.Count > 0)
        {
            SelectedStation = Stations[0];
        }

        if (Stations.Count == 0)
        {
            var station = await PromptForStationAsync(null, forceCreate: true);
            if (station is not null)
            {
                Stations.Add(station);
                _groundStationStore.SaveAll(Stations);
                SelectedStation = station;
            }
        }
    }

    private async Task<GroundStation?> PromptForStationAsync(GroundStation? existing, bool forceCreate = false)
    {
        var errorText = new TextBlock
        {
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed),
            Text = string.Empty,
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
        };

        var nameBox = new TextBox { Header = "Name", PlaceholderText = "e.g. Home" };
        var latBox = new TextBox { Header = "Latitude (deg)", PlaceholderText = "e.g. 34.0500" };
        var lonBox = new TextBox { Header = "Longitude (deg)", PlaceholderText = "e.g. -118.2500" };
        var altBox = new TextBox { Header = "Altitude (meters)", PlaceholderText = "e.g. 120" };

        if (existing is not null)
        {
            nameBox.Text = existing.Name;
            latBox.Text = existing.LatitudeDeg.ToString("0.####", CultureInfo.InvariantCulture);
            lonBox.Text = existing.LongitudeDeg.ToString("0.####", CultureInfo.InvariantCulture);
            altBox.Text = existing.AltitudeMeters.ToString("0.##", CultureInfo.InvariantCulture);
        }

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(nameBox);
        panel.Children.Add(latBox);
        panel.Children.Add(lonBox);
        panel.Children.Add(altBox);
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = existing is null ? "Add Ground Station" : "Edit Ground Station",
            Content = panel,
            PrimaryButtonText = "Save",
            CloseButtonText = forceCreate ? string.Empty : "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        while (true)
        {
            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            var name = nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                errorText.Text = "Name is required.";
                continue;
            }

            if (!TryParseDouble(latBox.Text, out var lat) || lat < -90 || lat > 90)
            {
                errorText.Text = "Latitude must be between -90 and 90 degrees.";
                continue;
            }

            if (!TryParseDouble(lonBox.Text, out var lon) || lon < -180 || lon > 180)
            {
                errorText.Text = "Longitude must be between -180 and 180 degrees.";
                continue;
            }

            if (!TryParseDouble(altBox.Text, out var alt))
            {
                errorText.Text = "Altitude must be a number (meters).";
                continue;
            }

            return new GroundStation(name, lat, lon, alt);
        }
    }

    private static bool TryParseDouble(string? text, out double value)
    {
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private bool LoadUseUtmPreference()
    {
        return LocalSettingsStore.GetBool(KeyUseUtm, false);
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            LogInfo("Initializing WebView2...");
            await MapView.EnsureCoreWebView2Async();
            var core = MapView.CoreWebView2;
            if (core is null)
            {
                _mapReady = false;
                _mapContentReady = false;
                LogWarn("WebView2 runtime not available.");
                SetStatus("WebView2 runtime not available. Install Microsoft Edge WebView2 Runtime.", InfoBarSeverity.Warning);
                return;
            }

            LogInfo($"WebView2 runtime: {core.Environment.BrowserVersionString}");
            _mapReady = true;
            _mapContentReady = false;
            MapView.NavigationCompleted += OnMapNavigationCompleted;
            core.NavigationStarting += OnMapNavigationStarting;
            core.DOMContentLoaded += OnMapDomContentLoaded;
            core.ProcessFailed += OnMapProcessFailed;
            core.WebResourceResponseReceived += OnMapWebResourceResponseReceived;
            core.WebMessageReceived += OnMapWebMessage;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDevToolsEnabled = true;

            await LoadMapContentAsync();
            UpdateMapStation();
            UpdateMapMode();
            UpdateMapTrack(DateTime.UtcNow, force: true);
        }
        catch (Exception ex)
        {
            LogWarn($"Map initialization failed: {ex.Message}");
            SetStatus($"Map initialization failed: {ex.Message}", InfoBarSeverity.Warning);
        }
    }

    private async Task LoadMapContentAsync()
    {
        try
        {
            var mapPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Map", "map.html");
            LogInfo($"Loading map HTML from: {mapPath}");
            if (!File.Exists(mapPath))
            {
                LogWarn("Map HTML not found in output.");
                SetStatus("Map HTML not found in output. Rebuild to copy Assets/Map/map.html.", InfoBarSeverity.Warning);
                return;
            }

            var html = await File.ReadAllTextAsync(mapPath);
            LogInfo($"Map HTML length: {html.Length}");
            MapView.NavigateToString(html);
        }
        catch (Exception ex)
        {
            LogWarn($"Map load failed: {ex.Message}");
            SetStatus($"Map load failed: {ex.Message}", InfoBarSeverity.Warning);
        }
    }

    private void OnMapNavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            LogWarn($"Map failed to load (status: {args.WebErrorStatus}).");
            SetStatus($"Map failed to load (status: {args.WebErrorStatus}).", InfoBarSeverity.Warning);
            return;
        }

        LogInfo("Map navigation completed.");
    }

    private void OnMapNavigationStarting(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
    {
        LogInfo($"Map navigation starting: {args.Uri}");
    }

    private void OnMapDomContentLoaded(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs args)
    {
        LogInfo("Map DOM content loaded.");
    }

    private void OnMapProcessFailed(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2ProcessFailedEventArgs args)
    {
        LogWarn($"Map process failed: {args.ProcessFailedKind}");
        SetStatus($"Map process failed: {args.ProcessFailedKind}", InfoBarSeverity.Warning);
    }

    private void OnMapWebMessage(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        var json = args.WebMessageAsJson;
        LogInfo($"Map message: {json}");
        TryHandleMapReady(json);
    }

    private void OnMapWebResourceResponseReceived(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2WebResourceResponseReceivedEventArgs args)
    {
        var uri = args.Request?.Uri ?? string.Empty;
        if (!ShouldLogResource(uri))
        {
            return;
        }

        try
        {
            var status = args.Response?.StatusCode ?? 0;
            if (status >= 400 || status == 0)
            {
                LogWarn($"Map resource error: {status} {args.Response?.ReasonPhrase} ({uri})");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Map resource log failed: {ex.Message}");
        }
    }

    private void UpdateMapStation()
    {
        if (!_mapReady || !_mapContentReady || _groundStation is null)
        {
            return;
        }

        PostMapMessage(new
        {
            type = "station",
            lat = _groundStation.LatitudeDeg,
            lon = _groundStation.LongitudeDeg,
            label = _groundStation.Name
        });
    }

    private void UpdateMapMode()
    {
        if (!_mapReady || !_mapContentReady)
        {
            return;
        }

        PostMapMessage(new { type = "mode", label = CoordModeLabel });
    }

    private void UpdateMapPosition(Sgp4State state)
    {
        if (!_mapReady || !_mapContentReady || SelectedSatellite is null)
        {
            return;
        }

        PostMapMessage(new
        {
            type = "sat",
            lat = state.LatDeg,
            lon = state.LonDeg,
            label = SelectedSatellite.Name
        });
    }

    private void UpdateMapTrack(DateTime timestampUtc, bool force = false)
    {
        if (!_mapReady || !_mapContentReady || SelectedSatellite is null)
        {
            return;
        }

        var utc = timestampUtc.ToUniversalTime();
        if (!force && (utc - _lastTrackUpdateUtc) < TimeSpan.FromMinutes(1))
        {
            return;
        }

        _lastTrackUpdateUtc = utc;

        if (!ShowTrack)
        {
            PostMapMessage(new { type = "track", kind = "past", points = Array.Empty<object>() });
            PostMapMessage(new { type = "track", kind = "future", points = Array.Empty<object>() });
            return;
        }

        var pastPoints = BuildTrackPoints(utc.AddMinutes(-TrackMinutes), utc);
        PostMapMessage(new { type = "track", kind = "past", points = pastPoints });

        if (ShowFutureTrack)
        {
            var futurePoints = BuildTrackPoints(utc, utc.AddMinutes(TrackMinutes));
            PostMapMessage(new { type = "track", kind = "future", points = futurePoints });
        }
        else
        {
            PostMapMessage(new { type = "track", kind = "future", points = Array.Empty<object>() });
        }
    }

    private void PostMapMessage(object payload)
    {
        if (!_mapReady || !_mapContentReady || MapView?.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            MapView.CoreWebView2.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"PostMapMessage failed: {ex.Message}");
        }
    }

    private void LogWarn(string message)
    {
        AppLogger.Warn(message);
        RefreshLog();
    }

    private void LogInfo(string message)
    {
        AppLogger.Info(message);
        RefreshLog();
    }

    private void RefreshLog()
    {
        DebugLogText = AppLogger.ReadTail();
        DebugLogTextBox.Text = DebugLogText;
    }

    private async void OnOpenLog(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            AppLogger.EnsureExists();
            Process.Start(new ProcessStartInfo
            {
                FileName = AppLogger.LogPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogWarn($"Unable to open log: {ex}");
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = AppStorage.BasePath,
                    UseShellExecute = true
                });
                SetStatus("Opened log folder instead of file.", InfoBarSeverity.Informational);
            }
            catch (Exception ex2)
            {
                SetStatus($"Unable to open log folder: {ex2.Message}", InfoBarSeverity.Warning);
            }
        }
    }

    private void OnClearLog(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        AppLogger.Clear();
        RefreshLog();
    }

    private void OnRefreshLog(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        RefreshLog();
    }

    private List<object> BuildTrackPoints(DateTime startUtc, DateTime endUtc)
    {
        var points = new List<object>();
        for (var t = startUtc; t <= endUtc; t = t.AddSeconds(TrackStepSeconds))
        {
            try
            {
                var state = _sgp4Service.Propagate(SelectedSatellite!, t);
                points.Add(new { lat = state.LatDeg, lon = state.LonDeg });
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Track point failed: {ex.Message}");
            }
        }

        return points;
    }

    private void TryHandleMapReady(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            if (!string.Equals(typeElement.GetString(), "ready", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _mapContentReady = true;
            UpdateMapStation();
            UpdateMapMode();
            UpdateMapTrack(DateTime.UtcNow, force: true);
        }
        catch
        {
        }
    }

    private static bool ShouldLogResource(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return false;
        }

        return uri.Contains("leaflet", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("unpkg", StringComparison.OrdinalIgnoreCase) ||
               uri.Contains("openstreetmap", StringComparison.OrdinalIgnoreCase);
    }
}
