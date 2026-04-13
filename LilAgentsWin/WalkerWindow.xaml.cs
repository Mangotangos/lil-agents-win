using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LilAgentsWin.Core;
using LilAgentsWin.UI;
using LilAgentsWin.Win32;

namespace LilAgentsWin;

/// <summary>
/// Transparent overlay window that renders one walking character on the taskbar.
/// </summary>
public partial class WalkerWindow : Window
{
    // ─── Config ───────────────────────────────────────────────────────────────

    private const double CharWidth    = 56;
    private const double CharHeight   = 72;
    private const double WalkSpeed    = 0.9;  // px per tick at 60 fps (~54 px/s)
    private const double ArmSwingDeg  = 28.0;
    private const double LegBobPx     = 6.0;
    private const int    TickMs        = 16;   // ~60 fps

    private static readonly string[] ThinkPhrases =
    [
        "hmm...", "thinking...", "🤔", "...", "loading...",
        "processing...", "calculating...", "on it!", "brb...", "🧠",
    ];

    // ─── State ────────────────────────────────────────────────────────────────

    private double _posX;
    private double _minX, _maxX;
    private int    _direction = 1; // +1 = right, -1 = left
    private double _taskbarY;
    private int    _frameCounter;
    private int    _walkFrame;
    private int    _idleCountdown;
    private bool   _isIdle;
    private bool   _isHovered;
    private double _charTop; // cached for hit-test

    // TransformGroup[0] = flip, TransformGroup[1] = hover scale — never overwrite each other
    private ScaleTransform _flipTransform  = new(1, 1, CharWidth / 2, 0);
    private ScaleTransform _hoverTransform = new(1, 1, CharWidth / 2, CharHeight / 2);

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(TickMs) };
    private readonly Random          _rng   = new();

    private PopoverTheme  _theme    = PopoverTheme.Midnight;
    private AgentProvider _provider = AgentProvider.Claude;
    private ChatWindow?   _chatWindow;

    // ─── Constructor ──────────────────────────────────────────────────────────

    public WalkerWindow(int characterIndex)
    {
        InitializeComponent();
        Loaded += OnLoaded;

        // Offset second character so they don't overlap
        _posX = characterIndex == 0 ? 120 : 300;
    }

    // ─── Startup ──────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionOnTaskbar();
        WindowHelper.SetWalkerStyle(this);

        // Combine flip + hover into one TransformGroup so they never overwrite each other
        var tg = new TransformGroup();
        tg.Children.Add(_flipTransform);
        tg.Children.Add(_hoverTransform);
        CharacterCanvas.RenderTransform = tg;

        // Hook WM_NCHITTEST — transparent outside character, clickable over it
        var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        src.AddHook(WndProc);

        _timer.Tick += OnTick;
        _timer.Start();
    }

    private void PositionOnTaskbar()
    {
        var tb = TaskbarHelper.GetTaskbar();

        // Convert physical pixels → WPF DIPs
        var dpi = VisualTreeHelper.GetDpi(this);
        double scaleX = 1.0 / (dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0);
        double scaleY = 1.0 / (dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0);

        _taskbarY = tb.Bounds.Top    * scaleY;
        double tbW = tb.Bounds.Width  * scaleX;
        double tbH = tb.Bounds.Height * scaleY;

        _minX = 0;
        _maxX = Math.Max(0, tbW - CharWidth);

        // Size the window to cover the whole bottom strip
        Left   = 0;
        Top    = _taskbarY;
        Width  = tbW;
        Height = tbH;

        RootCanvas.Width  = tbW;
        RootCanvas.Height = tbH;

        // Park character vertically centred in the taskbar strip
        _charTop = (tbH - CharHeight) / 2.0;
        System.Windows.Controls.Canvas.SetTop(CharacterCanvas, _charTop);
    }

    // ─── Tick loop ────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        _frameCounter++;

        if (_isIdle)
        {
            _idleCountdown--;
            if (_idleCountdown <= 0)
            {
                _isIdle = false;
                HideThinkBubble();
            }
            return;
        }

        // Move
        _posX += WalkSpeed * _direction;
        if (_posX >= _maxX) { _posX = _maxX; _direction = -1; MaybeGoIdle(); }
        if (_posX <= _minX) { _posX = _minX; _direction =  1; MaybeGoIdle(); }

        System.Windows.Controls.Canvas.SetLeft(CharacterCanvas, _posX);
        System.Windows.Controls.Canvas.SetLeft(ThinkBubble,    _posX);

        // Flip sprite — only update the flip transform, leave hover transform untouched
        _flipTransform.ScaleX = _direction == 1 ? 1 : -1;

        // Walk animation every 8 ticks
        if (_frameCounter % 8 == 0)
        {
            _walkFrame = (_walkFrame + 1) % 4;
            AnimateWalk(_walkFrame);
        }
    }

    // ─── Walk animation ───────────────────────────────────────────────────────

    private void AnimateWalk(int frame)
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(120));

        // Arms swing opposite each other
        double armAngle = frame switch { 0 => -ArmSwingDeg, 1 => 0, 2 => ArmSwingDeg, _ => 0 };
        ArmLRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation( armAngle, dur));
        ArmRRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-armAngle, dur));

        // Legs alternate up/down
        bool leftUp = frame is 0 or 1;
        LegLTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(leftUp  ? -LegBobPx : 0, dur));
        LegRTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(!leftUp ? -LegBobPx : 0, dur));
    }

    // ─── Idle ─────────────────────────────────────────────────────────────────

    private void MaybeGoIdle()
    {
        if (_rng.NextDouble() < 0.3)
        {
            _isIdle         = true;
            _idleCountdown  = _rng.Next(120, 360); // 2–6 seconds @ 60 fps
            ShowThinkBubble();
        }
    }

    private void ShowThinkBubble()
    {
        ThinkText.Text = ThinkPhrases[_rng.Next(ThinkPhrases.Length)];
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        ThinkBubble.BeginAnimation(OpacityProperty, fade);
    }

    private void HideThinkBubble()
    {
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        ThinkBubble.BeginAnimation(OpacityProperty, fade);
    }

    // ─── WM_NCHITTEST — per-pixel hit testing ────────────────────────────────

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam,
                           ref bool handled)
    {
        if (msg != HitTestHelper.WM_NCHITTEST) return IntPtr.Zero;

        // Convert screen coords → window-local WPF DIPs
        var screenPt  = HitTestHelper.DecodeScreenPoint(lParam);
        var windowPt  = PointFromScreen(screenPt);

        bool overChar = windowPt.X >= _posX              && windowPt.X <= _posX + CharWidth &&
                        windowPt.Y >= _charTop            && windowPt.Y <= _charTop + CharHeight;

        handled = true;
        if (overChar)
        {
            if (!_isHovered) { _isHovered = true;  AnimateHoverScale(1.15); }
            return new IntPtr(HitTestHelper.HTCLIENT);
        }
        else
        {
            if (_isHovered)  { _isHovered = false; AnimateHoverScale(1.0);  }
            return new IntPtr(HitTestHelper.HTTRANSPARENT);
        }
    }

    // ─── Mouse events ─────────────────────────────────────────────────────────

    private void Character_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateHoverScale(to: 1.15);
    }

    private void Character_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateHoverScale(to: 1.0);
    }

    private void AnimateHoverScale(double to)
    {
        var dur = new Duration(TimeSpan.FromMilliseconds(120));
        _hoverTransform.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, dur));
        _hoverTransform.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, dur));
    }

    private void Character_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_chatWindow is { IsVisible: true })
        {
            _chatWindow.Close();
            _chatWindow = null;
            return;
        }

        _chatWindow = new ChatWindow(_provider, _theme);

        // Position chat window above the character
        double charScreenX = Left + _posX;
        double charScreenY = Top;
        _chatWindow.Left = charScreenX - (ChatWindow.WindowWidth  / 2) + (CharWidth  / 2);
        _chatWindow.Top  = charScreenY - ChatWindow.WindowHeight - 8;

        // Keep on screen
        var screen = SystemParameters.WorkArea;
        if (_chatWindow.Left < screen.Left) _chatWindow.Left = screen.Left + 4;
        if (_chatWindow.Left + ChatWindow.WindowWidth > screen.Right)
            _chatWindow.Left = screen.Right - ChatWindow.WindowWidth - 4;
        if (_chatWindow.Top < screen.Top)
            _chatWindow.Top = charScreenY + CharHeight + 8;

        _chatWindow.Closed += (_, _) => _chatWindow = null;
        _chatWindow.Show();
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public void SetProvider(AgentProvider provider)
    {
        _provider = provider;
        _chatWindow?.SetProvider(provider);
    }

    public void SetTheme(PopoverTheme theme)
    {
        _theme = theme;
        _chatWindow?.ApplyTheme(theme);
    }

    public void Stop()
    {
        _timer.Stop();
        _chatWindow?.Close();
        Close();
    }
}
