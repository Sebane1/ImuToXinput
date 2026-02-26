using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ImuToXInput.Maui.Services;
using SlimeImuProtocol.SlimeProtocol;

namespace ImuToXInput.Maui;

public partial class TrackerDebugPage : ContentPage
{
    private const int RefreshIntervalMs = 8;
    private IDispatcherTimer? _refreshTimer;

    public ObservableCollection<TrackerEulerRow> Rows { get; } = new();

    public TrackerDebugPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _refreshTimer = Dispatcher.CreateTimer();
        _refreshTimer.Interval = TimeSpan.FromMilliseconds(RefreshIntervalMs);
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();
        RefreshRows();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _refreshTimer?.Stop();
        _refreshTimer = null;
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (Shell.Current != null)
            await Shell.Current.GoToAsync("//MainPage");
    }

    private void OnRefreshTick(object? sender, EventArgs e)
    {
        RefreshRows();
    }

    private void RefreshRows()
    {
        var trackers = ControllerLoopService.GetCurrentTrackers();
        if (trackers == null || trackers.Count == 0)
        {
            Rows.Clear();
            if (TrackerList != null)
            {
                TrackerList.IsVisible = false;
            }
            if (LblEmpty != null)
            {
                LblEmpty.IsVisible = true;
            }
            return;
        }

        if (TrackerList != null)
        {
            TrackerList.IsVisible = true;
        }
        if (LblEmpty != null)
        {
            LblEmpty.IsVisible = false;
        }

        var ordered = trackers.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var kv = ordered[i];
            var euler = kv.Value.Euler;
            var x = euler.X.ToString("F1");
            var y = euler.Y.ToString("F1");
            var z = euler.Z.ToString("F1");
            if (i < Rows.Count)
            {
                Rows[i].Update(kv.Key, x, y, z);
            }
            else
            {
                Rows.Add(new TrackerEulerRow { Name = kv.Key, EulerX = x, EulerY = y, EulerZ = z });
            }
        }
        while (Rows.Count > ordered.Count)
        {
            Rows.RemoveAt(Rows.Count - 1);
        }
    }
}

public class TrackerEulerRow : INotifyPropertyChanged
{
    private string _name = "";
    private string _eulerX = "";
    private string _eulerY = "";
    private string _eulerZ = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get => _name; set { if (_name == value) return; _name = value; OnPropertyChanged(); } }
    public string EulerX { get => _eulerX; set { if (_eulerX == value) return; _eulerX = value; OnPropertyChanged(); } }
    public string EulerY { get => _eulerY; set { if (_eulerY == value) return; _eulerY = value; OnPropertyChanged(); } }
    public string EulerZ { get => _eulerZ; set { if (_eulerZ == value) return; _eulerZ = value; OnPropertyChanged(); } }

    internal void Update(string name, string x, string y, string z)
    {
        Name = name;
        EulerX = x;
        EulerY = y;
        EulerZ = z;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
