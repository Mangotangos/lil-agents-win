using System.Windows;
using System.Windows.Input;
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
    private const double WalkSpeed    = 1.8;  // px per tick at 60 fps
    private const double ArmSwingDeg  = 20.0;
    private const double LegBobPx     = 4.0;
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
        WindowHelper.SetClickThrough(this);

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

        // Park character at the top of the taskbar strip
        double charTop = (tbH - CharHeight) / 2.0;
        System.Windows.Controls.Canvas.SetTop(CharacterCanvas, charTop);
    }

    // ─── Tick loop ────────────────────────────────────────────────────────────

    private void OnTick(object? sender, EventArgs e)
    {
        _frameCounter++;
        CheckHover();

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

        // Flip sprite direction
        CharacterCanvas.RenderTransform = _direction == 1
            ? System.Windows.Media.Transform.Identity
            : new System.Windows.Media.ScaleTransform(-1, 1, CharWidth / 2.0, 0);

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
        // Arm swing
        double armAngle = frame switch { 0 => -ArmSwingDeg, 1 => 0, 2 => ArmSwingDeg, _ => 0 };
        ArmLRotate.Angle =  armAngle;
        ArmRRotate.Angle = -armAngle;

        // Leg bob: legs alternate up/down
        bool leftUp = frame is 0 or 1;
        LegLTranslate.Y = leftUp  ? -LegBobPx : 0;
        LegRTranslate.Y = !leftUp ? -LegBobPx : 0;
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

    // ─── Hover (click-through toggle) ────────────────────────────────────────

    private void CheckHover()
    {
        var mouse  = System.Windows.Input.Mouse.GetPosition(this);
        var left   = System.Windows.Controls.Canvas.GetLeft(CharacterCanvas);
        var top    = System.Windows.Controls.Canvas.GetTop(CharacterCanvas);
        bool over  = mouse.X >= left && mouse.X <= left + CharWidth &&
                     mouse.Y >= top  && mouse.Y <= top  + CharHeight;

        if (over && !_isHovered)
        {
            _isHovered = true;
            WindowHelper.SetClickable(this);
        }
        else if (!over && _isHovered)
        {
            _isHovered = false;
            WindowHelper.SetClickThrough(this);
        }
    }

    // ─── Mouse events ─────────────────────────────────────────────────────────

    private void Character_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Scale up slightly on hover
        CharacterCanvas.RenderTransform = new System.Windows.Media.ScaleTransform(1.1, 1.1, CharWidth / 2, CharHeight / 2);
    }

    private void Character_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        CharacterCanvas.RenderTransform = null;
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
