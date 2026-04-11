using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace C.Compiler.Controls
{
    /// <summary>
    /// View model for a single chat message bubble.
    /// </summary>
    public class ChatMessageViewModel
    {
        public string RoleLabel { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public SolidColorBrush RoleColor { get; set; } = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        public SolidColorBrush TextColor { get; set; } = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        public SolidColorBrush BubbleBackground { get; set; } = new(Windows.UI.Color.FromArgb(255, 0, 0, 85));

        private static readonly SolidColorBrush UserRoleColor = new(Windows.UI.Color.FromArgb(255, 255, 255, 85)); // yellow
        private static readonly SolidColorBrush AiRoleColor = new(Windows.UI.Color.FromArgb(255, 0, 255, 0));      // green
        private static readonly SolidColorBrush UserBubbleBg = new(Windows.UI.Color.FromArgb(255, 0, 0, 68));      // dark blue
        private static readonly SolidColorBrush AiBubbleBg = new(Windows.UI.Color.FromArgb(255, 0, 0, 100));       // slightly lighter blue
        private static readonly SolidColorBrush WhiteText = new(Windows.UI.Color.FromArgb(255, 255, 255, 255));
        private static readonly SolidColorBrush YellowText = new(Windows.UI.Color.FromArgb(255, 255, 255, 85));

        public static ChatMessageViewModel CreateUser(string content) => new()
        {
            RoleLabel = "You",
            Content = content,
            RoleColor = UserRoleColor,
            TextColor = YellowText,
            BubbleBackground = UserBubbleBg,
        };

        public static ChatMessageViewModel CreateAssistant(string content) => new()
        {
            RoleLabel = "AI",
            Content = content,
            RoleColor = AiRoleColor,
            TextColor = WhiteText,
            BubbleBackground = AiBubbleBg,
        };
    }

    /// <summary>
    /// Context information to send with a chat message.
    /// </summary>
    public class AIChatContext
    {
        public string? FileName { get; set; }
        public string? FileContent { get; set; }
        public string? SelectedText { get; set; }
        public int CursorLine { get; set; }
        public int CursorColumn { get; set; }
        public List<string> CompilerErrors { get; set; } = new();
    }

    public sealed partial class AIChatPanel : UserControl
    {
        private readonly ObservableCollection<ChatMessageViewModel> _chatMessages = new();
        private bool _isAgentMode;
        private bool _attachContext = true;
        private CancellationTokenSource? _currentCts;

        /// <summary>
        /// Raised when the user clicks the close [X] button.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Raised when a message should be sent. The host wires this to the AI service.
        /// Args: (userMessage, isAgentMode, context)
        /// </summary>
        public event Func<string, bool, AIChatContext?, Task>? MessageSubmitted;

        /// <summary>
        /// Callback the host sets so the panel can request current editor context.
        /// </summary>
        public Func<AIChatContext?>? GetEditorContext { get; set; }

        /// <summary>
        /// Raised when a code block "Copy" button is clicked (text to clipboard).
        /// </summary>
        public event Action<string>? CopyCodeRequested;

        /// <summary>
        /// Raised when a code block "Insert" button is clicked (insert at cursor).
        /// </summary>
        public event Action<string>? InsertCodeRequested;

        /// <summary>
        /// Raised when an Agent mode "Apply" button is clicked.
        /// Args: (fileName, originalText, replacementText, editIndex).
        /// </summary>
        public event Action<string, string, string, int>? ApplyEditRequested;

        /// <summary>
        /// Raised when an Agent mode "Reject" button is clicked.
        /// </summary>
        public event Action<int>? RejectEditRequested;

        public AIChatPanel()
        {
            InitializeComponent();

            // Add welcome message
            AddAssistantMessage(
                "Hello! I'm your AI assistant.\nAsk me about your C code, explain errors, or switch to **Agent** mode to let me suggest edits.\n\n`Ctrl+Enter` to send.");
        }

        public bool IsAgentMode => _isAgentMode;

        /// <summary>
        /// Append a user message bubble.
        /// </summary>
        public void AddUserMessage(string text)
        {
            var bubble = CreateBubble("You",
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 85)),
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 68)));

            // User messages: plain text, no markdown
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 85)),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            ((StackPanel)((Border)bubble.Children[1]).Child).Children.Add(tb);

            ChatMessagesPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        /// <summary>
        /// Append an AI message bubble with markdown.
        /// </summary>
        public void AddAssistantMessage(string text)
        {
            var bubble = CreateBubble("AI",
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 100)));

            var rendered = MarkdownRenderer.Render(text,
                onCopyCode: code => CopyCodeRequested?.Invoke(code),
                onInsertCode: code => InsertCodeRequested?.Invoke(code),
                onApplyEdit: (fn, orig, repl, idx) => ApplyEditRequested?.Invoke(fn, orig, repl, idx),
                onRejectEdit: idx => RejectEditRequested?.Invoke(idx));

            ((StackPanel)((Border)bubble.Children[1]).Child).Children.Add(rendered);

            ChatMessagesPanel.Children.Add(bubble);
            ScrollToBottom();
        }

        /// <summary>
        /// Wraps a streaming TextBlock with a StringBuilder to avoid O(n²) string
        /// concatenation when appending tokens one by one.
        /// </summary>
        private sealed class StreamHandle
        {
            public TextBlock TextBlock { get; }
            public StringBuilder Buffer { get; } = new();
            public int TokenCount { get; set; }

            public StreamHandle(TextBlock tb) => TextBlock = tb;
        }

        /// <summary>
        /// Start a streaming AI response — returns the tag so tokens can be appended.
        /// The streaming bubble uses plain text until finalized.
        /// </summary>
        public object BeginAssistantStream()
        {
            var bubble = CreateBubble("AI",
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0)),
                new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 100)));

            var streamText = new TextBlock
            {
                Text = string.Empty,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            ((StackPanel)((Border)bubble.Children[1]).Child).Children.Add(streamText);

            ChatMessagesPanel.Children.Add(bubble);
            ScrollToBottom();
            return new StreamHandle(streamText);
        }

        /// <summary>
        /// Append a token to an in-progress streaming message.
        /// TextBlock is updated every 8 tokens to avoid O(n²) string re-allocation.
        /// </summary>
        public void AppendToStream(object streamHandle, string token)
        {
            if (streamHandle is StreamHandle handle)
            {
                handle.Buffer.Append(token);
                handle.TokenCount++;
                if (handle.TokenCount % 8 == 0)
                {
                    handle.TextBlock.Text = handle.Buffer.ToString();
                    ScrollToBottom();
                }
            }
        }

        /// <summary>
        /// Finalize a streaming message — replace the plain text with rendered markdown.
        /// </summary>
        public void FinalizeStream(object streamHandle, string fullText)
        {
            var tb = (streamHandle is StreamHandle h) ? h.TextBlock : streamHandle as TextBlock;
            if (tb?.Parent is StackPanel sp)
            {
                sp.Children.Remove(tb);
                var rendered = MarkdownRenderer.Render(fullText,
                    onCopyCode: code => CopyCodeRequested?.Invoke(code),
                    onInsertCode: code => InsertCodeRequested?.Invoke(code),
                    onApplyEdit: (fn, orig, repl, idx) => ApplyEditRequested?.Invoke(fn, orig, repl, idx),
                    onRejectEdit: idx => RejectEditRequested?.Invoke(idx));
                sp.Children.Add(rendered);
                ScrollToBottom();
            }
        }

        private static Grid CreateBubble(string roleLabel, SolidColorBrush roleColor, SolidColorBrush bgColor)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 4) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = roleLabel,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = roleColor,
                Margin = new Thickness(0, 0, 0, 1),
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var border = new Border
            {
                Background = bgColor,
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 85, 136)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4),
                CornerRadius = new CornerRadius(0),
                Child = new StackPanel { Spacing = 0 },
            };
            Grid.SetRow(border, 1);
            grid.Children.Add(border);

            return grid;
        }

        public void SetStatus(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Visibility = Visibility.Collapsed;
            }
            else
            {
                StatusText.Text = text;
                StatusText.Visibility = Visibility.Visible;
            }
        }

        public void ClearChat()
        {
            ChatMessagesPanel.Children.Clear();
        }

        public CancellationToken GetCancellationToken()
        {
            _currentCts?.Cancel();
            _currentCts = new CancellationTokenSource();
            return _currentCts.Token;
        }

        public void CancelCurrentRequest()
        {
            _currentCts?.Cancel();
        }

        /// <summary>
        /// Pre-fill the input box and optionally auto-submit.
        /// Used by "Explain Error" and "Ask AI" context actions.
        /// </summary>
        public async void SendPrefilled(string text)
        {
            InputBox.Text = text;
            await SubmitMessage();
        }

        private void ScrollToBottom()
        {
            // Dispatch to let layout complete
            DispatcherQueue.TryEnqueue(() =>
            {
                ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null);
            });
        }

        // ── Event handlers ──

        private void ClosePanel_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void AskMode_Click(object sender, RoutedEventArgs e)
        {
            _isAgentMode = false;
            AskModeBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 170, 170)); // cyan
            AgentModeBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 85));  // dark
            AgentModeBtnText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 170, 170));
            InputBox.PlaceholderText = "Ask about your code...";
        }

        private void AgentMode_Click(object sender, RoutedEventArgs e)
        {
            _isAgentMode = true;
            AgentModeBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 170, 170)); // cyan
            AskModeBtn.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 85));      // dark
            AgentModeBtnText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 0));
            InputBox.PlaceholderText = "Describe what to change...";
        }

        private void NewChat_Click(object sender, RoutedEventArgs e)
        {
            CancelCurrentRequest();
            ClearChat();
            AddAssistantMessage("New conversation started. How can I help?");
            SetStatus(string.Empty);
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            _attachContext = !_attachContext;
            ContextChips.Visibility = _attachContext ? Visibility.Visible : Visibility.Collapsed;
            AttachBtn.Background = _attachContext
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 85, 136))
                : new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            await SubmitMessage();
        }

        private async void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
                if (ctrl.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    e.Handled = true;
                    await SubmitMessage();
                }
            }
        }

        private async Task SubmitMessage()
        {
            var text = InputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text)) return;

            InputBox.Text = string.Empty;

            AIChatContext? context = null;
            if (_attachContext || _isAgentMode)
            {
                context = GetEditorContext?.Invoke();
            }

            if (MessageSubmitted != null)
            {
                AddUserMessage(text);
                await MessageSubmitted(text, _isAgentMode, context);
            }
        }
    }
}
