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

    /// <summary>Start the SlimeVR client and the mapping timer. Call once when the app is ready.</summary>
    public static void Start()
    {
        if (_running) return;

        var output = GetOutput?.Invoke();
        if (output == null)
            return;

        _configDirectory = Path.Combine(FileSystem.AppDataDirectory, "configs");
        if (!Directory.Exists(_configDirectory))
            Directory.CreateDirectory(_configDirectory);

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

            ControllerMappingRunner.Update(trackers, output, _loadedConfig, () => overrideMode, activeOverride);
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
        _running = false;
    }

    public static bool IsRunning => _running;

    /// <summary>Invalidate the cached active profile so it is reloaded on next tick. Call when the active profile file is saved.</summary>
    public static void InvalidateActiveProfileCache()
    {
        _cachedActiveProfile = null;
    }
}
