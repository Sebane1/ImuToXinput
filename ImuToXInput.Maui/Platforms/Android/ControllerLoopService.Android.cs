using System.Diagnostics;
using Android.Content;
using Android.OS;
using ImuToXInput.Core.Output;

namespace ImuToXInput.Maui.Services;

public static partial class ControllerLoopService
{
    static ControllerLoopService()
    {
        GetOutput = () => Platforms.Android.BleGamepadOutput.Instance;
        // Run the mapping loop on a background thread and keep a foreground service so input continues when app is backgrounded.
        CreateLoopTimer = (intervalMs, tick) => new BackgroundLoopRunner(intervalMs, tick);
    }

    private sealed class BackgroundLoopRunner : IDisposable
    {
        private readonly Thread _thread;
        private volatile bool _stop;
        private PowerManager.WakeLock? _wakeLock;

        public BackgroundLoopRunner(int intervalMs, Action tick)
        {
            StartForegroundService();
            AcquireWakeLock();
            _thread = new Thread(() =>
            {
                var sw = Stopwatch.StartNew();
                while (!_stop)
                {
                    try
                    {
                        tick();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ControllerLoop] Tick error: {ex.Message}");
                    }
                    var elapsed = (int)sw.ElapsedMilliseconds;
                    var delay = intervalMs - elapsed;
                    if (delay > 0)
                        Thread.Sleep(delay);
                    sw.Restart();
                }
            })
            { IsBackground = true, Name = "ControllerLoop" };
            _thread.Start();
        }

        private void AcquireWakeLock()
        {
            try
            {
                var ctx = global::Android.App.Application.Context;
                if (ctx == null) return;
                var pm = (PowerManager?)ctx.GetSystemService(Context.PowerService);
                if (pm == null) return;
                _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "ImuToXInput::ControllerLoop");
                _wakeLock.SetReferenceCounted(false);
                _wakeLock.Acquire();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ControllerLoop] WakeLock acquire failed: {ex.Message}");
            }
        }

        private void ReleaseWakeLock()
        {
            try
            {
                if (_wakeLock?.IsHeld == true)
                    _wakeLock.Release();
                _wakeLock = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ControllerLoop] WakeLock release failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _stop = true;
            if (_thread.IsAlive)
            {
                try { _thread.Join(500); } catch { }
            }
            ReleaseWakeLock();
            StopForegroundService();
        }

        private static void StartForegroundService()
        {
            try
            {
                var ctx = global::Android.App.Application.Context;
                if (ctx == null) return;
                var intent = new Intent(ctx, typeof(Platforms.Android.ControllerForegroundService));
                if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
                    ctx.StartForegroundService(intent);
                else
                    ctx.StartService(intent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ControllerLoop] StartForegroundService failed: {ex.Message}");
            }
        }

        private static void StopForegroundService()
        {
            try
            {
                var ctx = global::Android.App.Application.Context;
                if (ctx == null) return;
                var intent = new Intent(ctx, typeof(Platforms.Android.ControllerForegroundService));
                ctx.StopService(intent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ControllerLoop] StopForegroundService failed: {ex.Message}");
            }
        }
    }
}
