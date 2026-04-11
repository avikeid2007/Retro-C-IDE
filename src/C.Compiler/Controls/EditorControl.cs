using System;
using System.Collections.Generic;
using System.Text;

using C.Compiler.Models;
using C.Compiler.Services;

using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Windows.Foundation;
using Windows.UI;

namespace C.Compiler.Controls
{
    public sealed partial class EditorControl : UserControl
    {
        private readonly SyntaxHighlighter _highlighter = new();
        private EditorDocument _document = new();
        private bool _isUpdating;
        private bool _isHighlighting;
        private bool _isLoaded;
        private int _windowNumber = 1;
        private DispatcherTimer? _highlightTimer;
        private DispatcherTimer? _cursorBlinkTimer;
        private bool _cursorVisible = true;

        public event EventHandler? CloseRequested;
        public event EventHandler? ContentChanged;
        public event EventHandler<(int Line, int Column)>? CursorMoved;
        public event EventHandler<string>? AskAIRequested;

        public EditorDocument Document
        {
            get => _document;
            set
            {
                _document = value;
                LoadDocument();
            }
        }

        public int WindowNumber
        {
            get => _windowNumber;
            set
            {
                _windowNumber = value;
            }
        }

        public EditorControl()
        {
            InitializeComponent();
            SetEditorDefaults();
            StartCursorBlink();

            Loaded += (_, _) => { _isLoaded = true; ApplySyntaxHighlighting(); CodeEditor.Focus(FocusState.Programmatic); UpdateBlockCursorPosition(); };
            Unloaded += (_, _) => { _isLoaded = false; Cleanup(); };
        }

        public void Cleanup()
        {
            _highlightTimer?.Stop();
            _highlightTimer = null;
            _cursorBlinkTimer?.Stop();
            _cursorBlinkTimer = null;
        }

        private void SetEditorDefaults()
        {
            CodeEditor.Document.SetText(TextSetOptions.None, string.Empty);

            var format = CodeEditor.Document.GetDefaultCharacterFormat();
            format.ForegroundColor = Color.FromArgb(255, 255, 255, 85); // Yellow
            format.Size = 14;
            format.Name = "Consolas";
            CodeEditor.Document.SetDefaultCharacterFormat(format);
        }

        private void LoadDocument()
        {
            _isUpdating = true;
            TitleRun.Text = _document.DisplayTitle;

            CodeEditor.Document.SetText(TextSetOptions.None, _document.Content);
            ApplySyntaxHighlighting();

            _isUpdating = false;
        }

        public string GetText()
        {
            CodeEditor.Document.GetText(TextGetOptions.None, out string text);
            return text.TrimEnd('\r', '\n');
        }

        public string GetSelectedText()
        {
            CodeEditor.Document.Selection.GetText(TextGetOptions.None, out string text);
            return text ?? string.Empty;
        }

        public void SetText(string text)
        {
            _isUpdating = true;
            CodeEditor.Document.SetText(TextSetOptions.None, text);
            _document.Content = text;
            ApplySyntaxHighlighting();
            _isUpdating = false;
        }

        public void SetReadOnly(bool readOnly)
        {
            IsReadOnly = readOnly;
            CodeEditor.IsReadOnly = readOnly;
        }

        public bool IsReadOnly { get; private set; }

        public void GoToLine(int lineNumber)
        {
            string text = GetText();
            int pos = 0;
            int currentLine = 1;

            foreach (char c in text)
            {
                if (currentLine >= lineNumber) break;
                if (c == '\r') currentLine++;
                pos++;
            }

            var range = CodeEditor.Document.GetRange(pos, pos);
            range.ScrollIntoView(PointOptions.Start);
            CodeEditor.Document.Selection.SetRange(pos, pos);
            CodeEditor.Focus(FocusState.Programmatic);
        }

        public void FocusEditor()
        {
            CodeEditor.Focus(FocusState.Programmatic);
        }

        public void Undo() => CodeEditor.Document.Undo();
        public void Redo() => CodeEditor.Document.Redo();

        public void Cut()
        {
            CodeEditor.Document.Selection.Cut();
        }

        public void Copy()
        {
            CodeEditor.Document.Selection.Copy();
        }

        public async void Paste()
        {
            var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
            {
                string text = await dataPackageView.GetTextAsync();
                CodeEditor.Document.Selection.TypeText(text);
            }
        }

        public void ClearSelection()
        {
            CodeEditor.Document.Selection.TypeText(string.Empty);
        }

        public void InsertText(string text)
        {
            CodeEditor.Document.Selection.TypeText(text);
        }

        public bool Find(string searchText, bool caseSensitive, bool wholeWord)
        {
            if (string.IsNullOrEmpty(searchText)) return false;

            string text = GetText();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            int startFrom = CodeEditor.Document.Selection.EndPosition;
            int index = text.IndexOf(searchText, startFrom, comparison);

            if (index < 0)
                index = text.IndexOf(searchText, 0, comparison);

            if (index >= 0)
            {
                CodeEditor.Document.Selection.SetRange(index, index + searchText.Length);
                return true;
            }

            return false;
        }

        public int ReplaceAll(string searchText, string replaceText, bool caseSensitive)
        {
            if (string.IsNullOrEmpty(searchText)) return 0;

            string text = GetText();
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            int count = 0;
            int searchFrom = 0;
            int index;

            while ((index = text.IndexOf(searchText, searchFrom, comparison)) >= 0)
            {
                count++;
                searchFrom = index + searchText.Length;
            }

            if (count > 0)
            {
                string newText = caseSensitive
                    ? text.Replace(searchText, replaceText, StringComparison.Ordinal)
                    : text.Replace(searchText, replaceText, StringComparison.OrdinalIgnoreCase);
                SetText(newText);
                _document.IsDirty = true;
                _document.Content = newText;
                TitleRun.Text = _document.DisplayTitle;
            }

            return count;
        }

        private static readonly Color DefaultTokenColor = Color.FromArgb(255, 255, 255, 85);

        private void ApplySyntaxHighlighting()
        {
            if (_isHighlighting || !_isLoaded) return;
            _isHighlighting = true;
            _isUpdating = true;

            try
            {
                // Save cursor position
                int selStart = CodeEditor.Document.Selection.StartPosition;
                int selEnd = CodeEditor.Document.Selection.EndPosition;

                string text = GetText();
                if (string.IsNullOrEmpty(text)) return;

                // GetText returns \r as paragraph separators; normalize to \n so the
                // tokenizer's line-ending checks work correctly. The replacement is 1-for-1
                // so all token Start/Length positions remain valid for GetRange().
                var tokens = _highlighter.Tokenize(text.Replace('\r', '\n'));

                // Reset all text to default yellow
                var fullRange = CodeEditor.Document.GetRange(0, text.Length);
                var defaultFormat = fullRange.CharacterFormat;
                defaultFormat.ForegroundColor = DefaultTokenColor;
                defaultFormat.Bold = FormatEffect.Off;
                defaultFormat.Name = "Consolas";
                defaultFormat.Size = 14;

                // Only apply formatting to tokens that differ from the default.
                // Skipping default-color tokens avoids flooding the native RichEdit
                // with hundreds of redundant GetRange/SetCharacterFormat COM calls,
                // which can corrupt its internal state and cause AccessViolationException.
                foreach (var token in tokens)
                {
                    if (!token.Bold && token.Color.Equals(DefaultTokenColor))
                        continue;

                    if (token.Start + token.Length > text.Length) continue;

                    var range = CodeEditor.Document.GetRange(token.Start, token.Start + token.Length);
                    var format = range.CharacterFormat;
                    format.ForegroundColor = token.Color;
                    format.Bold = token.Bold ? FormatEffect.On : FormatEffect.Off;
                    format.Name = "Consolas";
                    format.Size = 14;
                }

                // Restore cursor position
                CodeEditor.Document.Selection.SetRange(selStart, selEnd);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Guard against COM interop / native RichEdit failures
                System.Diagnostics.Debug.WriteLine($"Syntax highlighting failed: {ex.Message}");
            }
            finally
            {
                _isUpdating = false;
                _isHighlighting = false;
            }
        }

        private (int Line, int Column) GetCursorPosition()
        {
            string text = GetText();
            int pos = CodeEditor.Document.Selection.StartPosition;
            if (pos > text.Length) pos = text.Length;

            int line = 1, col = 1;
            for (int i = 0; i < pos && i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
            return (line, col);
        }

        private void CodeEditor_TextChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating || _isHighlighting) return;

            string text = GetText();
            _document.Content = text;
            _document.IsDirty = true;
            TitleRun.Text = _document.DisplayTitle;

            // Debounce syntax highlighting — only run after 400ms idle
            _highlightTimer?.Stop();
            _highlightTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _highlightTimer.Tick -= HighlightTimer_Tick;
            _highlightTimer.Tick += HighlightTimer_Tick;
            _highlightTimer.Start();

            ContentChanged?.Invoke(this, EventArgs.Empty);
        }

        private void HighlightTimer_Tick(object? sender, object e)
        {
            _highlightTimer?.Stop();
            if (_isLoaded)
                ApplySyntaxHighlighting();
        }

        private void CodeEditor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdating) return;

            var (line, col) = GetCursorPosition();
            _document.CursorLine = line;
            _document.CursorColumn = col;

            // Update the bottom border line:col display
            BottomLineColRun.Text = $"{line}:{col}";

            CursorMoved?.Invoke(this, (line, col));

            UpdateBlockCursorPosition();
        }

        private void StartCursorBlink()
        {
            _cursorBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
            _cursorBlinkTimer.Tick += CursorBlink_Tick;
            _cursorBlinkTimer.Start();
        }

        private void CursorBlink_Tick(object? sender, object e)
        {
            _cursorVisible = !_cursorVisible;
            BlockCursor.Opacity = _cursorVisible ? 1 : 0;
        }

        private void UpdateBlockCursorPosition()
        {
            try
            {
                var sel = CodeEditor.Document.Selection;
                // Only show block cursor when there's no selection range (caret mode)
                if (sel.StartPosition != sel.EndPosition)
                {
                    BlockCursor.Opacity = 0;
                    return;
                }

                var range = CodeEditor.Document.GetRange(sel.StartPosition, sel.StartPosition);
                range.GetRect(PointOptions.ClientCoordinates, out Rect rect, out _);

                // Dynamic cursor sizing from font metrics
                double fontSize = 14;
                var fmt = CodeEditor.Document.GetDefaultCharacterFormat();
                if (fmt.Size > 0) fontSize = fmt.Size;
                BlockCursor.Height = fontSize * 1.35;  // line height
                BlockCursor.Width = fontSize * 0.62;   // monospace char width

                // Transform from RichEditBox coordinates to the parent Grid
                var transform = CodeEditor.TransformToVisual(BlockCursor.Parent as UIElement);
                var point = transform.TransformPoint(new Point(rect.X, rect.Y));

                BlockCursor.Margin = new Thickness(point.X, point.Y, 0, 0);
                _cursorVisible = true;
                BlockCursor.Opacity = 1;

                // Reset blink cycle so cursor is visible right after moving
                _cursorBlinkTimer?.Stop();
                _cursorBlinkTimer?.Start();
            }
            catch
            {
                // Ignore positioning errors (e.g., during load)
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void ShowAskAIContextMenu(bool show)
        {
            AskAIMenuItem.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            AskAISeparator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void AskAI_Click(object sender, RoutedEventArgs e)
        {
            string selected = GetSelectedText();
            AskAIRequested?.Invoke(this, selected);
        }

        private void ContextCut_Click(object sender, RoutedEventArgs e) => Cut();
        private void ContextCopy_Click(object sender, RoutedEventArgs e) => Copy();
        private void ContextPaste_Click(object sender, RoutedEventArgs e) => Paste();
        private void ContextSelectAll_Click(object sender, RoutedEventArgs e)
        {
            CodeEditor.Document.GetText(TextGetOptions.None, out string text);
            CodeEditor.Document.Selection.SetRange(0, text.Length);
        }
    }
}
