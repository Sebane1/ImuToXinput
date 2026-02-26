namespace ImuToXInput.Maui.Behaviors;

/// <summary>
/// On Android, ensures the soft keyboard is shown when the user taps the script editor WebView.
/// The WebView's inner textarea may not request the IME on its own.
/// </summary>
public class ScriptEditorKeyboardBehavior : Behavior<WebView>
{
    protected override void OnAttachedTo(WebView bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.HandlerChanged += OnHandlerChanged;
        bindable.HandlerChanging += OnHandlerChanging;
        if (bindable.Handler != null)
            AttachToNative(bindable);
    }

    protected override void OnDetachingFrom(WebView bindable)
    {
        bindable.HandlerChanged -= OnHandlerChanged;
        bindable.HandlerChanging -= OnHandlerChanging;
        base.OnDetachingFrom(bindable);
    }

    private void OnHandlerChanging(object? sender, HandlerChangingEventArgs e)
    {
        if (e.OldHandler != null)
            DetachFromNative();
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is WebView wv && wv.Handler != null)
            AttachToNative(wv);
    }

#if ANDROID
    private global::Android.Views.View? _nativeView;
    private bool _attached;

    private void AttachToNative(WebView bindable)
    {
        if (DeviceInfo.Platform != DevicePlatform.Android) return;
        if (_attached) return;
        var platformView = bindable.Handler?.PlatformView;
        if (platformView is global::Android.Webkit.WebView androidWebView)
        {
            _nativeView = androidWebView;
            _attached = true;
            androidWebView.Touch += OnWebViewTouch;
        }
    }

    private void DetachFromNative()
    {
        if (!_attached || _nativeView is not global::Android.Webkit.WebView wv) return;
        wv.Touch -= OnWebViewTouch;
        _nativeView = null;
        _attached = false;
    }

    private void OnWebViewTouch(object? sender, global::Android.Views.View.TouchEventArgs e)
    {
        if (e?.Event?.Action != global::Android.Views.MotionEventActions.Down) return;
        var view = sender as global::Android.Views.View;
        if (view == null) return;
        var imm = (global::Android.Views.InputMethods.InputMethodManager?)view.Context?.GetSystemService(global::Android.Content.Context.InputMethodService);
        if (imm != null)
            imm.ShowSoftInput(view, global::Android.Views.InputMethods.ShowFlags.Implicit);
    }
#else
    private void AttachToNative(WebView _) { }
    private void DetachFromNative() { }
#endif
}
