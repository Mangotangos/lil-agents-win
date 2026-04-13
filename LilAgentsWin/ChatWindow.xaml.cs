using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using LilAgentsWin.Core;
using LilAgentsWin.Sessions;
using LilAgentsWin.UI;

namespace LilAgentsWin;

public partial class ChatWindow : Window
{
    public const double WindowWidth  = 380;
    public const double WindowHeight = 520;

    // ─── State ────────────────────────────────────────────────────────────────

    private AgentProvider  _provider;
    private PopoverTheme   _theme;
    private IAgentSession? _session;

    private readonly ObservableCollection<MessageItem> _messages = [];
    private MessageItem? _streamingItem;

    // ─── Constructor ──────────────────────────────────────────────────────────

    public ChatWindow(AgentProvider provider, PopoverTheme theme)
    {
        InitializeComponent();
        _provider = provider;
        _theme    = theme;

        MessageList.ItemsSource = _messages;

        ApplyTheme(theme);
        UpdateTitle();

        InputBox.Focus();
    }

    // ─── Theme ────────────────────────────────────────────────────────────────

    public void ApplyTheme(PopoverTheme theme)
    {
        _theme = theme;

        RootBorder.Background   = theme.BackgroundBrush;
        RootBorder.BorderBrush  = theme.BorderBrush;
        TitleLabel.Foreground   = theme.TextBrush;
        Divider.Fill            = theme.BorderBrush;

        TitleBar.Background = theme.SurfaceBrush;

        InputBox.Background  = theme.InputBgBrush;
        InputBox.Foreground  = theme.TextBrush;
        InputBox.BorderBrush = theme.BorderBrush;
        InputBox.CaretBrush  = theme.AccentBrush;

        SendBtn.Background = theme.AccentBrush;
        SendBtn.Foreground = Brushes.White;
    }

    // ─── Provider ─────────────────────────────────────────────────────────────

    public void SetProvider(AgentProvider provider)
    {
        _provider = provider;
        _session?.Dispose();
        _session = null;
        UpdateTitle();
    }

    private void UpdateTitle() => TitleLabel.Text = _provider.ToString();

    // ─── Send ─────────────────────────────────────────────────────────────────

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return && !string.IsNullOrWhiteSpace(InputBox.Text))
            SendMessage();
        else if (e.Key == Key.Escape)
            Close();
    }

    private void SendBtn_Click(object sender, RoutedEventArgs e) => SendMessage();

    private void SendMessage()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // Handle slash commands
        if (HandleSlashCommand(text)) { InputBox.Clear(); return; }

        InputBox.Clear();
        AddMessage(text, isUser: true);

        EnsureSession();
        SetStatusDot(Colors.Orange);

        _ = _session!.SendAsync(text);
    }

    private bool HandleSlashCommand(string text)
    {
        switch (text.ToLowerInvariant())
        {
            case "/clear":
                _messages.Clear();
                _session?.Dispose();
                _session = null;
                return true;

            case "/help":
                AddMessage(
                    "Commands:\n" +
                    "  /clear  — clear chat history\n" +
                    "  /copy   — copy last response\n" +
                    "  /help   — show this help",
                    isUser: false);
                return true;

            case "/copy":
                var last = _messages.LastOrDefault(m => !m.IsUser);
                if (last is not null)
                    System.Windows.Clipboard.SetText(last.Text);
                return true;

            default:
                return false;
        }
    }

    // ─── Session management ───────────────────────────────────────────────────

    private void EnsureSession()
    {
        if (_session is not null) return;

        try
        {
            _session = SessionFactory.Create(_provider);
        }
        catch (Exception ex)
        {
            AddMessage($"Error: {ex.Message}", isUser: false);
            return;
        }

        _session.OnOutput += chunk =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_streamingItem is null)
                {
                    _streamingItem = new MessageItem(string.Empty, isUser: false, _theme);
                    _messages.Add(_streamingItem);
                }
                _streamingItem.Append(chunk);
                ScrollToBottom();
            });
        };

        _session.OnError += err =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                AddMessage($"⚠ {err}", isUser: false);
                SetStatusDot(Colors.OrangeRed);
            });
        };

        _session.OnDone += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _streamingItem = null;
                SetStatusDot(Colors.LimeGreen);
            });
        };
    }

    // ─── UI helpers ───────────────────────────────────────────────────────────

    private void AddMessage(string text, bool isUser)
    {
        _messages.Add(new MessageItem(text, isUser, _theme));
        ScrollToBottom();
    }

    private void ScrollToBottom() =>
        Scroller.ScrollToEnd();

    private void SetStatusDot(Color color) =>
        StatusDot.Color = color;

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _session?.Dispose();
        base.OnClosed(e);
    }
}

// ─── MessageItem ─────────────────────────────────────────────────────────────

public sealed class MessageItem : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    private string _text;

    public string Text
    {
        get => _text;
        private set { _text = value; PropertyChanged?.Invoke(this, new(nameof(Text))); }
    }

    public bool    IsUser    { get; }
    public Brush   BubbleBrush { get; }
    public Brush   TextBrush   { get; }
    public HorizontalAlignment Alignment => IsUser ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    public MessageItem(string text, bool isUser, PopoverTheme theme)
    {
        _text      = text;
        IsUser     = isUser;
        BubbleBrush = isUser ? theme.AccentBrush  : theme.SurfaceBrush;
        TextBrush   = isUser ? Brushes.White       : theme.TextBrush;
    }

    public void Append(string chunk) => Text += chunk;
}
