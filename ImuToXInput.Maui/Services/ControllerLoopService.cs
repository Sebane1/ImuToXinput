using ImuToXInput.Config;
using ImuToXInput.Core.Output;
using SlimeImuProtocol.SlimeProtocol;

namespace ImuToXInput.Maui.Services;

/// <summary>
/// Runs the controller mapping loop: SlimeVR trackers → config/profile → gamepad output (BLE on Android, ViGEm on Windows).
/// Set <see cref="GetOutput"/> from platform code; then call <see cref="Start"/> (e.g. from App).
/// </summary>
public static partial class ControllerLoopService
{
    private const int UpdateIntervalMs = 8; // ~125 Hz

    /// <summary>Platform provides the gamepad output (BleGamepadOutput on Android, ViGEm on Windows). Set in platform partial.</summary>
    public static Func<IGamepadOutput?>? GetOutput { get; set; }

    private static SlimeVRClient? _slimeVRClient;
    private static LoadedConfig? _loadedConfig;
    private static string _configDirectory = "";
    private static IDispatcherTimer? _timer;
    private static bool _running;
    private static string? _cachedActiveFileName;
    private static GameProfile? _cachedActiveProfile;
    private static readonly ThumbstickMenuModeState _menuModeState = new();
    private static readonly MenuModeToggleState _menuModeToggleState = new();

    private static string? _currentProfileFileName;
    private static string? _currentProcessName;
    private static bool _currentIsOverride;
    private static bool _currentIsStepMania;

    /// <summary>When set (e.g. on Windows), used to detect the running game process for auto profile and display. Receives loaded config, returns process name or null.</summary>
    public static Func<LoadedConfig?, string?>? GetProcessName { get; set; }

    /// <summary>File name of the profile currently in use (e.g. "default.json"); null when using legacy game mode (StepMania).</summary>
    public static string? CurrentProfileFileName => _currentProfileFileName;
    /// <summary>Process name that triggered the current profile (e.g. "ffxiv_dx11"); null when using active override or default with no process.</summary>
    public static string? CurrentProcessName => _currentProcessName;
    /// <summary>True when the profile in use is the user-selected active profile (not auto-matched).</summary>
    public static bool CurrentIsOverride => _currentIsOverride;
    /// <summary>True when mapping is in StepMania / dance pad mode.</summary>
    public static bool CurrentIsStepMania => _currentIsStepMania;

    /// <summary>Start the SlimeVR client and the mapping timer. Call once when the app is ready.</summary>
    public static void Start()
    {
        if (_running) return;

        var output = GetOutput?.Invoke();
        if (output == null)
        {
            return;
        }

        _configDirectory = Path.Combine(FileSystem.AppDataDirectory, "configs");
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        _loadedConfig = ConfigLoader.LoadFromDirectory(_configDirectory);

        _slimeVRClient = new SlimeVRClient();
        _slimeVRClient.Start();
        var trackers = _slimeVRClient.Trackers;

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        _timer = dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(UpdateIntervalMs);
        _timer.Tick += OnTick;
        _timer.Start();
        _running = true;

        void OnTick(object? sender, EventArgs e)
        {
            if (trackers == null || output == null) return;

            var overrideMode = Preferences.Default.Get(ImuToXInput.Maui.MainPage.OverrideGameModePreferenceKey, (string?)null);
            GameProfile? activeOverride = null;
            // When dance pad / StepMania mode is on, don't use active profile; runner will use StepMania mapping.
            if (string.IsNullOrEmpty(overrideMode) || !string.Equals(overrideMode, "stepmania", StringComparison.OrdinalIgnoreCase))
            {
                var activeFileName = Preferences.Default.Get(ImuToXInput.Maui.MainPage.ActiveProfilePreferenceKey, (string?)null);
                if (!string.IsNullOrEmpty(activeFileName))
                {
                    var path = Path.Combine(_configDirectory, activeFileName);
                    var needsLoad = activeFileName != _cachedActiveFileName || (_cachedActiveProfile == null && File.Exists(path));
                    if (needsLoad)
                    {
                        _cachedActiveFileName = activeFileName;
                        _cachedActiveProfile = null;
                        if (File.Exists(path))
                        {
                            try
                            {
                                var json = File.ReadAllText(path);
                                _cachedActiveProfile = Newtonsoft.Json.JsonConvert.DeserializeObject<GameProfile>(json);
                            }
                            catch { /* keep null */ }
                        }
                    }
                    activeOverride = _cachedActiveProfile;
                }
                else
                {
                    _cachedActiveFileName = null;
                    _cachedActiveProfile = null;
                }
            }

            // Resolve running game for display and for runner: StepMania override, or detected process
            string? runningGame = string.Equals(overrideMode, "stepmania", StringComparison.OrdinalIgnoreCase)
                ? "stepmania"
                : (GetProcessName?.Invoke(_loadedConfig) ?? null);

            if (activeOverride != null)
            {
                _currentProfileFileName = _cachedActiveFileName;
                _currentProcessName = null;
                _currentIsOverride = true;
                _currentIsStepMania = false;
            }
            else if (string.Equals(runningGame, "stepmania", StringComparison.OrdinalIgnoreCase))
            {
                _currentProfileFileName = null;
                _currentProcessName = "stepmania";
                _currentIsOverride = false;
                _currentIsStepMania = true;
            }
            else
            {
                var (_, fileName) = ConfigLoader.GetProfileAndFileNameForProcess(_loadedConfig, runningGame);
                _currentProfileFileName = fileName;
                _currentProcessName = runningGame;
                _currentIsOverride = false;
                _currentIsStepMania = false;
            }

            var menuMode = Preferences.Default.Get(ImuToXInput.Maui.MainPage.MenuModePreferenceKey, false);
            void OnMenuModeToggleRequested()
            {
                var current = Preferences.Default.Get(ImuToXInput.Maui.MainPage.MenuModePreferenceKey, false);
                Preferences.Default.Set(ImuToXInput.Maui.MainPage.MenuModePreferenceKey, !current);
            }
            ControllerMappingRunner.Update(trackers, output, _loadedConfig, () => runningGame, activeOverride, menuMode, menuMode ? _menuModeState : null, OnMenuModeToggleRequested, _menuModeToggleState);
        }
    }

    /// <summary>Stop the timer and release resources.</summary>
    public static void Stop()
    {
        if (!_running) return;
        _timer?.Stop();
        _timer = null;
        _slimeVRClient = null;
        _loadedConfig = null;
        _cachedActiveFileName = null;
        _cachedActiveProfile = null;
        _currentProfileFileName = null;
        _currentProcessName = null;
        _currentIsOverride = false;
        _currentIsStepMania = false;
        _running = false;
    }

    public static bool IsRunning => _running;

    /// <summary>Current tracker states (name → state) for UI debugging. Returns null if the loop is not running.</summary>
    public static Dictionary<string, TrackerState>? GetCurrentTrackers() => _slimeVRClient?.Trackers;

    /// <summary>Invalidate the cached active profile so it is reloaded on next tick. Call when the active profile file is saved.</summary>
    public static void InvalidateActiveProfileCache()
    {
        _cachedActiveProfile = null;
    }

    /// <summary>Reload all profiles from disk. Call when any profile is saved so process-matched profiles pick up changes.</summary>
    public static void ReloadConfig()
    {
        if (!string.IsNullOrEmpty(_configDirectory) && Directory.Exists(_configDirectory))
        {
            _loadedConfig = ConfigLoader.LoadFromDirectory(_configDirectory);
        }
    }
}
