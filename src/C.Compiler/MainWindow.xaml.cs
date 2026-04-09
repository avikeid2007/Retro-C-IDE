using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using C.Compiler.Controls;
using C.Compiler.Models;
using C.Compiler.Services;

using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

using Windows.Graphics;
using Windows.UI;

using WinRT.Interop;

namespace C.Compiler
{
    public sealed partial class MainWindow : Window
    {
        private readonly FileService _fileService;
        private readonly CompilerService _compilerService = new();
        private readonly SettingsService _settingsService = new();
        private readonly ObservableCollection<string> _messages = new();
        private readonly List<CompilerError> _currentErrors = new();
        private readonly List<EditorControl> _editors = new();

        private string _lastSearchText = string.Empty;
        private bool _lastSearchCaseSensitive;
        private string? _lastCompiledExePath;
        private string _programArguments = string.Empty;
        private int _errorIndex;
        private string _lastHelpTopic = string.Empty;

        // Menu system state
        private readonly Popup[] _menuPopups;
        private readonly Button[] _menuButtons;
        private int _activeMenuIndex = -1;
        private bool _menuBarActive;

        private EditorControl ActiveEditor
        {
            get
            {
                if (_editors.Count == 0)
                {
                    // Safety: should never happen since last tab resets instead of closing
                    AddEditorTab(_fileService.CreateNew());
                }
                int idx = EditorTabs.SelectedIndex;
                if (idx >= 0 && idx < _editors.Count) return _editors[idx];
                return _editors[0];
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            _fileService = new FileService(this);

            // Collect popup and button references
            _menuPopups = new Popup[]
            {
                MenuPopup0, MenuPopup1, MenuPopup2, MenuPopup3, MenuPopup4,
                MenuPopup5, MenuPopup6, MenuPopup7, MenuPopup8, MenuPopup9, MenuPopup10
            };
            _menuButtons = new Button[]
            {
                SysMenuBtn, FileMenuBtn, EditMenuBtn, SearchMenuBtn, RunMenuBtn,
                CompileMenuBtn, DebugMenuBtn, ProjectMenuBtn, OptionsMenuBtn, WindowMenuBtn, HelpMenuBtn
            };

            // Window setup — extend into title bar to remove Windows chrome
            Title = "RetroC IDE";
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(MenuBarGrid); // Menu bar doubles as drag region

            // Style the caption buttons to match gray menu bar
            var appWindow = GetAppWindow();
            if (appWindow.TitleBar != null)
            {
                appWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 170, 170, 170);
                appWindow.TitleBar.ButtonForegroundColor = Color.FromArgb(255, 0, 0, 0);
                appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 0, 170, 0);
                appWindow.TitleBar.ButtonHoverForegroundColor = Color.FromArgb(255, 0, 0, 0);
                appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 0, 128, 0);
                appWindow.TitleBar.ButtonPressedForegroundColor = Color.FromArgb(255, 255, 255, 255);
                appWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 170, 170, 170);
                appWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 85, 85, 85);
            }
            appWindow.Resize(new SizeInt32(1024, 768));

            // Message list
            MessageList.ItemsSource = _messages;

            // Create first editor tab
            AddEditorTab(_fileService.CreateNew());

            // Load settings + detect compiler
            _ = InitAsync().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    System.Diagnostics.Debug.WriteLine($"InitAsync failed: {t.Exception}");
            }, TaskScheduler.Default);
        }

        private AppWindow GetAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private async Task InitAsync()
        {
            await _settingsService.LoadAsync();
            _compilerService.Settings = _settingsService.Settings.Compiler;

            if (_compilerService.DetectCompiler())
            {
                AddMessage($"Compiler ready: {_compilerService.DetectedCompilerType} — {_compilerService.DetectedCompilerPath}");
            }
            else
            {
                AddMessage("No C compiler found. Rebuild the project to bundle TCC, or configure manually in Options > Compiler.");
            }
        }

        // ═══════════════════════════════════════
        // TAB / MULTI-WINDOW MANAGEMENT
        // ═══════════════════════════════════════

        private void EditorTabs_AddTabButtonClick(TabView sender, object args)
        {
            AddEditorTab(_fileService.CreateNew());
        }

        private void AddEditorTab(EditorDocument doc)
        {
            var editor = new EditorControl();
            editor.Document = doc;
            editor.WindowNumber = _editors.Count + 1;
            editor.CursorMoved += OnCursorMoved;
            editor.ContentChanged += OnContentChanged;
            editor.CloseRequested += OnEditorCloseRequested;
            _editors.Add(editor);

            // Wrap in a Grid to force stretch — TabView content area doesn't always propagate Stretch
            var wrapper = new Grid();
            wrapper.Children.Add(editor);

            var tab = new TabViewItem
            {
                Header = doc.DisplayTitle,
                Content = wrapper,
                IsClosable = true,
                VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Stretch,
                HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            };
            EditorTabs.TabItems.Add(tab);
            EditorTabs.SelectedItem = tab;
            editor.FocusEditor();
        }

        private void UpdateTabHeaders()
        {
            for (int i = 0; i < _editors.Count; i++)
            {
                if (i < EditorTabs.TabItems.Count && EditorTabs.TabItems[i] is TabViewItem tab)
                {
                    _editors[i].WindowNumber = i + 1;
                    tab.Header = $"{i + 1} {_editors[i].Document.DisplayTitle}";
                }
            }
        }

        private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditorTabs.SelectedIndex >= 0 && EditorTabs.SelectedIndex < _editors.Count)
            {
                var ed = _editors[EditorTabs.SelectedIndex];
                StatusLineCol.Text = $"{ed.Document.CursorLine}:{ed.Document.CursorColumn}";
            }
        }

        private void EditorTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            int idx = sender.TabItems.IndexOf(args.Tab);
            if (idx >= 0 && idx < _editors.Count)
            {
                CloseEditorTab(idx);
            }
        }

        private void CloseEditorTab(int index)
        {
            if (_editors.Count <= 1)
            {
                // Last tab — don't close, just reset to new file
                _editors[0].Document = _fileService.CreateNew();
                UpdateTabHeaders();
                return;
            }

            var editor = _editors[index];
            editor.CursorMoved -= OnCursorMoved;
            editor.ContentChanged -= OnContentChanged;
            editor.CloseRequested -= OnEditorCloseRequested;
            editor.Cleanup();
            _editors.RemoveAt(index);
            EditorTabs.TabItems.RemoveAt(index);
            UpdateTabHeaders();
        }

        private void SwitchToTab(int index)
        {
            if (index >= 0 && index < _editors.Count)
            {
                EditorTabs.SelectedIndex = index;
                ActiveEditor.FocusEditor();
            }
        }

        // ═══════════════════════════════════════
        // CUSTOM MENU SYSTEM
        // ═══════════════════════════════════════

        private void MenuTitle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int index))
            {
                if (_activeMenuIndex == index && _menuPopups[index].IsOpen)
                {
                    CloseAllMenus();
                }
                else
                {
                    OpenMenu(index);
                }
            }
        }

        private void MenuTitle_Hover(object sender, PointerRoutedEventArgs e)
        {
            // Only switch menus on hover if a menu is already open
            if (!_menuBarActive) return;

            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int index))
            {
                if (index != _activeMenuIndex)
                {
                    OpenMenu(index);
                }
            }
        }

        private void OpenMenu(int index)
        {
            // Close any open menu first
            for (int i = 0; i < _menuPopups.Length; i++)
            {
                _menuPopups[i].IsOpen = false;
                _menuButtons[i].Background = new SolidColorBrush(Colors.Transparent);
            }

            // Position the popup below the button
            var btn = _menuButtons[index];
            var transform = btn.TransformToVisual(RootGrid);
            var pos = transform.TransformPoint(new Windows.Foundation.Point(0, btn.ActualHeight));

            _menuPopups[index].HorizontalOffset = pos.X;
            _menuPopups[index].VerticalOffset = pos.Y;
            _menuPopups[index].IsOpen = true;

            // Highlight the active menu title (green like TC3)
            btn.Background = new SolidColorBrush(Color.FromArgb(255, 0, 170, 0));

            _activeMenuIndex = index;
            _menuBarActive = true;
        }

        private void CloseAllMenus()
        {
            for (int i = 0; i < _menuPopups.Length; i++)
            {
                _menuPopups[i].IsOpen = false;
                _menuButtons[i].Background = new SolidColorBrush(Colors.Transparent);
            }
            _activeMenuIndex = -1;
            _menuBarActive = false;
        }

        private void MenuPopup_Closed(object sender, object e)
        {
            // When light-dismiss closes a popup, reset state
            if (sender is Popup closedPopup)
            {
                int index = Array.IndexOf(_menuPopups, closedPopup);
                if (index >= 0)
                {
                    _menuButtons[index].Background = new SolidColorBrush(Colors.Transparent);
                }
            }

            // Check if any popup is still open
            bool anyOpen = false;
            for (int i = 0; i < _menuPopups.Length; i++)
            {
                if (_menuPopups[i].IsOpen) { anyOpen = true; break; }
            }
            if (!anyOpen)
            {
                _activeMenuIndex = -1;
                _menuBarActive = false;
            }
        }

        // ═══════════════════════════════════════
        // KEYBOARD ACCELERATORS
        // ═══════════════════════════════════════

        private void Accel_Save(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { FileSave_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Open(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { FileOpen_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Make(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { Make_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Compile(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { Compile_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Run(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { RunProgram_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Quit(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { Close(); e.Handled = true; }
        private void Accel_UserScreen(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { UserScreen_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Find(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { SearchFind_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Replace(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { SearchReplace_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_GoToLine(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { GoToLine_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_PrevError(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { PreviousError_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_NextError(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { NextError_Click(s, new RoutedEventArgs()); e.Handled = true; }
        private void Accel_Help(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { HelpContents_Click(s, new RoutedEventArgs()); e.Handled = true; }

        private void Accel_NextWindow(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { WindowNext_Click(s, new RoutedEventArgs()); e.Handled = true; }

        private void Accel_SwitchWindow(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            // Alt+1..Alt+9 → switch to window N
            int n = s.Key - Windows.System.VirtualKey.Number1; // 0-based
            if (n >= 0 && n < _editors.Count)
                SwitchToTab(n);
            e.Handled = true;
        }

        private void Accel_Escape(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        {
            // Close any open dialog or menu
            if (FindDialogOverlay.Visibility == Visibility.Visible) { FindDialogOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (GoToLineDialogOverlay.Visibility == Visibility.Visible) { GoToLineDialogOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (CompilerDialogOverlay.Visibility == Visibility.Visible) { CompilerDialogOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (AboutDialogOverlay.Visibility == Visibility.Visible) { AboutDialogOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (ArgumentsDialogOverlay.Visibility == Visibility.Visible) { ArgumentsDialogOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            if (DirectoriesDialogOverlay.Visibility == Visibility.Visible) { DirectoriesDialogOverlay.Visibility = Visibility.Collapsed; e.Handled = true; return; }
            CloseAllMenus();
            e.Handled = true;
        }

        // ═══════════════════════════════════════
        // MESSAGE HELPERS
        // ═══════════════════════════════════════

        private void AddMessage(string message) => _messages.Add(message);
        private void ClearMessages() { _messages.Clear(); _currentErrors.Clear(); }

        // ═══════════════════════════════════════
        // EDITOR EVENTS
        // ═══════════════════════════════════════

        private void OnCursorMoved(object? sender, (int Line, int Column) pos) => StatusLineCol.Text = $"{pos.Line}:{pos.Column}";
        private void OnContentChanged(object? sender, EventArgs e) => UpdateTabHeaders();
        private void OnEditorCloseRequested(object? sender, EventArgs e)
        {
            if (sender is EditorControl ed)
            {
                int idx = _editors.IndexOf(ed);
                if (idx >= 0) CloseEditorTab(idx);
            }
        }

        // ═══════════════════════════════════════
        // FILE MENU HANDLERS
        // ═══════════════════════════════════════

        private void FileNew_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddEditorTab(_fileService.CreateNew()); }

        private async void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            var doc = await _fileService.OpenFileAsync();
            if (doc != null) { AddEditorTab(doc); AddMessage($"Loaded: {doc.FilePath}"); }
        }

        private async void FileSave_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ActiveEditor.Document.Content = ActiveEditor.GetText();
            if (await _fileService.SaveAsync(ActiveEditor.Document))
            {
                AddMessage($"Saved: {ActiveEditor.Document.FilePath}");
                UpdateTabHeaders();
            }
        }

        private async void FileSaveAs_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ActiveEditor.Document.Content = ActiveEditor.GetText();
            if (await _fileService.SaveAsAsync(ActiveEditor.Document))
            {
                AddMessage($"Saved as: {ActiveEditor.Document.FilePath}");
                UpdateTabHeaders();
            }
        }

        private void ChangeDir_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage($"Current directory: {Environment.CurrentDirectory}"); }

        private async void FileSaveAll_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            int saved = 0;
            foreach (var editor in _editors)
            {
                editor.Document.Content = editor.GetText();
                if (editor.Document.IsDirty)
                {
                    if (editor.Document.IsNewFile)
                        await _fileService.SaveAsAsync(editor.Document);
                    else
                        await _fileService.SaveAsync(editor.Document);
                    saved++;
                }
            }
            UpdateTabHeaders();
            AddMessage(saved > 0 ? $"Saved {saved} file(s)." : "No unsaved files.");
        }

        private void FilePrint_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            AddMessage("Print: Printing is not supported in this version. Use File > Save and print from Notepad or another editor.");
        }

        private void DosShell_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = true,
                WorkingDirectory = ActiveEditor.Document.FilePath != null
                    ? Path.GetDirectoryName(ActiveEditor.Document.FilePath) ?? Environment.CurrentDirectory
                    : Environment.CurrentDirectory
            });
        }

        private void Quit_Click(object sender, RoutedEventArgs e) { Close(); }

        // ═══════════════════════════════════════
        // EDIT MENU HANDLERS
        // ═══════════════════════════════════════

        private void EditUndo_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); ActiveEditor.Undo(); }
        private void EditRedo_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); ActiveEditor.Redo(); }
        private void EditCut_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); ActiveEditor.Cut(); }
        private void EditCopy_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); ActiveEditor.Copy(); }
        private void EditPaste_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); ActiveEditor.Paste(); }
        private void EditClear_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); ActiveEditor.ClearSelection(); }

        private void EditCopyExample_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            const string example =
                "#include <stdio.h>\n\nint main() {\n    printf(\"Hello, World!\\n\");\n    return 0;\n}";
            ActiveEditor.InsertText(example);
            AddMessage("Example code inserted at cursor.");
        }

        private async void EditShowClipboard_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            try
            {
                var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
                if (dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                {
                    string text = await dataPackageView.GetTextAsync();
                    ClearMessages();
                    AddMessage("═══ Clipboard Contents ═══");
                    foreach (var line in text.Split('\n'))
                        AddMessage(line.TrimEnd());
                }
                else
                {
                    AddMessage("Clipboard is empty or contains non-text data.");
                }
            }
            catch
            {
                AddMessage("Could not read clipboard.");
            }
        }

        // ═══════════════════════════════════════
        // SEARCH MENU — inline dialog overlays
        // ═══════════════════════════════════════

        private void SearchFind_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgFindSearchBox.Text = _lastSearchText;
            DlgFindReplaceBox.Text = string.Empty;
            FindDialogOverlay.Visibility = Visibility.Visible;
            DlgFindSearchBox.Focus(FocusState.Programmatic);
        }

        private void SearchReplace_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgFindSearchBox.Text = _lastSearchText;
            FindDialogOverlay.Visibility = Visibility.Visible;
            DlgFindReplaceBox.Focus(FocusState.Programmatic);
        }

        private void DlgFind_OK(object sender, RoutedEventArgs e)
        {
            _lastSearchText = DlgFindSearchBox.Text;
            _lastSearchCaseSensitive = DlgFindCaseCheck.IsChecked == true;
            bool wholeWord = DlgFindWholeWordCheck.IsChecked == true;
            FindDialogOverlay.Visibility = Visibility.Collapsed;

            if (!ActiveEditor.Find(_lastSearchText, _lastSearchCaseSensitive, wholeWord))
                AddMessage($"Search string not found: {_lastSearchText}");
        }

        private void DlgFind_ReplaceAll(object sender, RoutedEventArgs e)
        {
            string search = DlgFindSearchBox.Text;
            string replace = DlgFindReplaceBox.Text;
            bool caseSensitive = DlgFindCaseCheck.IsChecked == true;
            FindDialogOverlay.Visibility = Visibility.Collapsed;

            int count = ActiveEditor.ReplaceAll(search, replace, caseSensitive);
            AddMessage($"Replaced {count} occurrences.");
        }

        private void DlgFind_Cancel(object sender, RoutedEventArgs e) => FindDialogOverlay.Visibility = Visibility.Collapsed;

        private void SearchAgain_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            if (!string.IsNullOrEmpty(_lastSearchText))
            {
                if (!ActiveEditor.Find(_lastSearchText, _lastSearchCaseSensitive, false))
                    AddMessage($"Search string not found: {_lastSearchText}");
            }
        }

        private void SearchPrevious_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            if (string.IsNullOrEmpty(_lastSearchText))
            {
                AddMessage("No search text. Use Find first.");
                return;
            }

            var text = ActiveEditor.GetText();
            var comparison = _lastSearchCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int currentPos = ActiveEditor.Document.Selection.StartPosition;
            if (currentPos > 0) currentPos--;

            // Search backward from current position
            int index = text.LastIndexOf(_lastSearchText, currentPos, comparison);
            if (index < 0)
            {
                // Wrap to end and search again
                index = text.LastIndexOf(_lastSearchText, comparison);
            }

            if (index >= 0)
            {
                ActiveEditor.Document.Selection.SetRange(index, index + _lastSearchText.Length);
                AddMessage($"Found at line {GetLineNumber(text, index)}.");
            }
            else
            {
                AddMessage($"No previous match: {_lastSearchText}");
            }
        }

        private int GetLineNumber(string text, int position)
        {
            int line = 1;
            for (int i = 0; i < position && i < text.Length; i++)
                if (text[i] == '\r') line++;
            return line;
        }

        private void GoToLine_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgGoToLineBox.Text = ActiveEditor.Document.CursorLine.ToString();
            GoToLineDialogOverlay.Visibility = Visibility.Visible;
            DlgGoToLineBox.Focus(FocusState.Programmatic);
            DlgGoToLineBox.SelectAll();
        }

        private void DlgGoToLine_OK(object sender, RoutedEventArgs e)
        {
            GoToLineDialogOverlay.Visibility = Visibility.Collapsed;
            if (int.TryParse(DlgGoToLineBox.Text, out int line) && line > 0)
                ActiveEditor.GoToLine(line);
        }

        private void DlgGoToLine_Cancel(object sender, RoutedEventArgs e) => GoToLineDialogOverlay.Visibility = Visibility.Collapsed;

        private void PreviousError_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            if (_currentErrors.Count > 0)
            {
                _errorIndex = (_errorIndex - 1 + _currentErrors.Count) % _currentErrors.Count;
                var error = _currentErrors[_errorIndex];
                ActiveEditor.GoToLine(error.Line);
                AddMessage($"Error {_errorIndex + 1}/{_currentErrors.Count}: {error}");
            }
        }

        private void NextError_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            if (_currentErrors.Count > 0)
            {
                _errorIndex = (_errorIndex + 1) % _currentErrors.Count;
                var error = _currentErrors[_errorIndex];
                ActiveEditor.GoToLine(error.Line);
                AddMessage($"Error {_errorIndex + 1}/{_currentErrors.Count}: {error}");
            }
        }

        // ═══════════════════════════════════════
        // COMPILE MENU
        // ═══════════════════════════════════════

        private async void Compile_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); await CompileCurrentFileAsync(compileOnly: true); }
        private async void Make_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); await CompileCurrentFileAsync(compileOnly: false); }
        private async void LinkExe_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); await CompileCurrentFileAsync(compileOnly: false); AddMessage("Link: TCC links in a single step with Make."); }
        private async void BuildAll_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); await CompileCurrentFileAsync(compileOnly: false); }

        private void CompilePrimaryFile_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            var primary = _compilerService.Settings.PrimarySourceFile;
            if (!string.IsNullOrEmpty(primary))
                AddMessage($"Primary C file is set to: {primary}. Configure via Options > Make.");
            else
                AddMessage("No primary C file set. Configure via Options > Make, or open the file you want to compile.");
        }

        private void GetInfo_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            var doc = ActiveEditor.Document;
            var text = ActiveEditor.GetText();
            int lines = text.Split('\r').Length;
            AddMessage($"═══ File Info ═══");
            AddMessage($"File: {doc.FileName}");
            AddMessage($"Path: {doc.FilePath ?? "(unsaved)"}");
            AddMessage($"Lines: {lines}");
            AddMessage($"Size: {System.Text.Encoding.UTF8.GetByteCount(text)} bytes");
            AddMessage($"Compiler: {_compilerService.DetectedCompilerType} — {_compilerService.DetectedCompilerPath}");
        }

        private async Task CompileCurrentFileAsync(bool compileOnly)
        {
            var doc = ActiveEditor.Document;
            doc.Content = ActiveEditor.GetText();

            string sourceFilePath;
            bool usedTempFile = false;

            if (doc.IsNewFile || doc.IsDirty)
            {
                // Write to a temp file so the user never has to save manually
                string tempDir = Path.Combine(Path.GetTempPath(), "TurboC");
                Directory.CreateDirectory(tempDir);

                // Use a stable name based on the doc so the EXE path is predictable
                string safeName = doc.IsNewFile
                    ? "noname"
                    : Path.GetFileNameWithoutExtension(doc.FileName);
                sourceFilePath = Path.Combine(tempDir, safeName + ".c");
                await File.WriteAllTextAsync(sourceFilePath, doc.Content);
                usedTempFile = doc.IsNewFile; // only treat as temp if truly unsaved
            }
            else
            {
                sourceFilePath = doc.FilePath!;
            }

            ClearMessages();
            string label = doc.IsNewFile ? $"(temp) {Path.GetFileName(sourceFilePath)}" : doc.FileName;
            AddMessage($"Compiling {label}...");

            var result = compileOnly
                ? await _compilerService.CompileAsync(sourceFilePath)
                : await _compilerService.MakeAsync(sourceFilePath);

            if (result.Success)
            {
                AddMessage("Compilation successful.");
                if (!compileOnly) { _lastCompiledExePath = result.OutputPath; AddMessage($"Output: {result.OutputPath}"); }
            }
            else
            {
                AddMessage("Compilation failed:");
                _currentErrors.Clear();
                foreach (var error in result.Errors) { AddMessage(error.ToString()); _currentErrors.Add(error); }
                if (result.Errors.Count == 0 && !string.IsNullOrWhiteSpace(result.RawOutput))
                    foreach (var line in result.RawOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                        AddMessage(line.TrimEnd());
                if (_currentErrors.Count > 0) ActiveEditor.GoToLine(_currentErrors[0].Line);
            }

            // Clean up temp .c source (keep the .exe so it can be run)
            if (usedTempFile && result.Success && !compileOnly)
            {
                try { File.Delete(sourceFilePath); } catch { }
            }
        }

        // ═══════════════════════════════════════
        // DEBUG MENU
        // ═══════════════════════════════════════

        private void DebugEvaluate_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            AddMessage("Evaluate/Modify: Debugger not integrated. Compile with -g and attach GDB for watch expressions.");
        }

        private void DebugWatches_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            AddMessage("Watches: Debugger not integrated. Variable watching requires GDB integration.");
        }

        private void DebugToggleBreakpoint_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            AddMessage("Toggle Breakpoint: Debugger not integrated. Breakpoints require GDB integration.");
        }

        private void DebugBreakpoints_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            AddMessage("Breakpoints: Debugger not integrated. Requires GDB integration.");
        }

        // ═══════════════════════════════════════
        // RUN MENU
        // ═══════════════════════════════════════

        private async void RunProgram_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            await CompileCurrentFileAsync(compileOnly: false);
            if (_lastCompiledExePath != null && File.Exists(_lastCompiledExePath))
            {
                AddMessage($"Running: {_lastCompiledExePath} {_programArguments}");
                _compilerService.RunExecutable(_lastCompiledExePath, _programArguments);
            }
            else
            {
                AddMessage("No executable to run. Compile your program first.");
            }
        }

        private void UserScreen_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("User Screen: Switch to the console window to see program output."); }

        private void RunArguments_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgArgumentsBox.Text = _programArguments;
            ArgumentsDialogOverlay.Visibility = Visibility.Visible;
            DlgArgumentsBox.Focus(FocusState.Programmatic);
        }

        private void DlgArguments_OK(object sender, RoutedEventArgs e)
        {
            _programArguments = DlgArgumentsBox.Text.Trim();
            ArgumentsDialogOverlay.Visibility = Visibility.Collapsed;
            AddMessage($"Program arguments: {(_programArguments.Length > 0 ? _programArguments : "(none)")}");
        }

        private void DlgArguments_Cancel(object sender, RoutedEventArgs e) => ArgumentsDialogOverlay.Visibility = Visibility.Collapsed;

        private void RunReset_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            _lastCompiledExePath = null;
            _currentErrors.Clear();
            ClearMessages();
            AddMessage("Program reset. Compile to rebuild.");
        }

        private void RunGoToCursor_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Go to Cursor (F4): Requires GDB integration."); }
        private void RunTraceInto_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Trace Into (F7): Requires GDB integration."); }
        private void RunStepOver_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Step Over (F8): Requires GDB integration."); }

        // ═══════════════════════════════════════
        // OPTIONS MENU — inline dialog
        // ═══════════════════════════════════════

        private void OptionsCompiler_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            var s = _compilerService.Settings;
            DlgCompilerPath.Text = s.CompilerPath;
            DlgIncludeDirs.Text = s.IncludeDirectories;
            DlgLibDirs.Text = s.LibraryDirectories;
            DlgOutputDir.Text = s.OutputDirectory;
            DlgFlags.Text = s.AdditionalFlags;
            DlgCompilerTimeout.Text = s.TimeoutSeconds.ToString();
            CompilerDialogOverlay.Visibility = Visibility.Visible;
        }

        private async void DlgCompiler_OK(object sender, RoutedEventArgs e)
        {
            CompilerDialogOverlay.Visibility = Visibility.Collapsed;
            if (!int.TryParse(DlgCompilerTimeout.Text, out int timeout) || timeout <= 0)
                timeout = 30;

            _compilerService.Settings = new CompilerSettings
            {
                CompilerPath = DlgCompilerPath.Text.Trim(),
                CompilerType = "auto",
                IncludeDirectories = DlgIncludeDirs.Text.Trim(),
                LibraryDirectories = DlgLibDirs.Text.Trim(),
                OutputDirectory = DlgOutputDir.Text.Trim(),
                AdditionalFlags = DlgFlags.Text.Trim(),
                TimeoutSeconds = timeout
            };
            _settingsService.Settings.Compiler = _compilerService.Settings;
            await _settingsService.SaveAsync();
            AddMessage("Compiler options saved.");
        }

        private void DlgCompiler_Cancel(object sender, RoutedEventArgs e) => CompilerDialogOverlay.Visibility = Visibility.Collapsed;

        // ═══════════════════════════════════════
        // WINDOW MENU
        // ═══════════════════════════════════════

        private void WindowList_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            AddMessage("═══ Open Windows ═══");
            for (int i = 0; i < _editors.Count; i++)
            {
                string marker = (i == EditorTabs.SelectedIndex) ? " ►" : "  ";
                AddMessage($"{marker} {i + 1}: {_editors[i].Document.FileName}");
            }
        }

        private void WindowNext_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            if (_editors.Count > 1)
                SwitchToTab((EditorTabs.SelectedIndex + 1) % _editors.Count);
        }

        private void WindowPrevious_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            if (_editors.Count > 1)
                SwitchToTab((EditorTabs.SelectedIndex - 1 + _editors.Count) % _editors.Count);
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            CloseEditorTab(EditorTabs.SelectedIndex);
        }

        private void WindowCloseAll_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            while (_editors.Count > 1)
                CloseEditorTab(_editors.Count - 1);
            _editors[0].Document = _fileService.CreateNew();
            UpdateTabHeaders();
        }

        // ═══════════════════════════════════════
        // OPTIONS > DIRECTORIES — inline dialog
        // ═══════════════════════════════════════

        private void OptionsDirectories_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            var s = _compilerService.Settings;
            DlgDirInclude.Text = s.IncludeDirectories;
            DlgDirLib.Text = s.LibraryDirectories;
            DlgDirOutput.Text = s.OutputDirectory;
            DirectoriesDialogOverlay.Visibility = Visibility.Visible;
        }

        private async void DlgDirectories_OK(object sender, RoutedEventArgs e)
        {
            DirectoriesDialogOverlay.Visibility = Visibility.Collapsed;
            _compilerService.Settings.IncludeDirectories = DlgDirInclude.Text.Trim();
            _compilerService.Settings.LibraryDirectories = DlgDirLib.Text.Trim();
            _compilerService.Settings.OutputDirectory = DlgDirOutput.Text.Trim();
            _settingsService.Settings.Compiler = _compilerService.Settings;
            await _settingsService.SaveAsync();
            AddMessage("Directories saved.");
        }

        private void DlgDirectories_Cancel(object sender, RoutedEventArgs e) => DirectoriesDialogOverlay.Visibility = Visibility.Collapsed;

        // ═══════════════════════════════════════
        // OPTIONS > LINKER
        // ═══════════════════════════════════════

        private void OptionsLinker_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgLinkerFlags.Text = _compilerService.Settings.LinkerFlags;
            DlgLinkerMapFile.Text = _compilerService.Settings.GenerateMapFile ? "yes" : "no";
            LinkerDialogOverlay.Visibility = Visibility.Visible;
        }

        private async void DlgLinker_OK(object sender, RoutedEventArgs e)
        {
            LinkerDialogOverlay.Visibility = Visibility.Collapsed;
            _compilerService.Settings.LinkerFlags = DlgLinkerFlags.Text.Trim();
            _compilerService.Settings.GenerateMapFile = DlgLinkerMapFile.Text.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
            _settingsService.Settings.Compiler = _compilerService.Settings;
            await _settingsService.SaveAsync();
            AddMessage("Linker options saved.");
        }

        private void DlgLinker_Cancel(object sender, RoutedEventArgs e) => LinkerDialogOverlay.Visibility = Visibility.Collapsed;

        // ═══════════════════════════════════════
        // OPTIONS > MAKE
        // ═══════════════════════════════════════

        private void OptionsMake_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgMakePrimaryFile.Text = _compilerService.Settings.PrimarySourceFile;
            DlgMakeWarnings.Text = _compilerService.Settings.WarningsAsErrors ? "yes" : "no";
            MakeDialogOverlay.Visibility = Visibility.Visible;
        }

        private async void DlgMake_OK(object sender, RoutedEventArgs e)
        {
            MakeDialogOverlay.Visibility = Visibility.Collapsed;
            _compilerService.Settings.PrimarySourceFile = DlgMakePrimaryFile.Text.Trim();
            _compilerService.Settings.WarningsAsErrors = DlgMakeWarnings.Text.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
            _settingsService.Settings.Compiler = _compilerService.Settings;
            await _settingsService.SaveAsync();
            AddMessage("Make options saved.");
        }

        private void DlgMake_Cancel(object sender, RoutedEventArgs e) => MakeDialogOverlay.Visibility = Visibility.Collapsed;

        // ═══════════════════════════════════════
        // OPTIONS > ARGUMENTS (program arguments)
        // ═══════════════════════════════════════

        private void OptionsArguments_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgArgumentsBox.Text = _programArguments;
            ArgumentsDialogOverlay.Visibility = Visibility.Visible;
            DlgArgumentsBox.Focus(FocusState.Programmatic);
        }

        // ═══════════════════════════════════════
        // OPTIONS > ENVIRONMENT
        // ═══════════════════════════════════════

        private void OptionsEnvironment_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ClearMessages();
            AddMessage("═══ Environment ═══");
            AddMessage($"OS:          {Environment.OSVersion}");
            AddMessage($"Machine:     {Environment.MachineName}");
            AddMessage($"User:        {Environment.UserName}");
            AddMessage($"CPU cores:   {Environment.ProcessorCount}");
            AddMessage($"Temp dir:    {Path.GetTempPath()}");
            AddMessage($"Working dir: {Environment.CurrentDirectory}");
            AddMessage($"Compiler:    {_compilerService.DetectedCompilerType} — {_compilerService.DetectedCompilerPath ?? "(none)"}");
        }

        // ═══════════════════════════════════════
        // OPTIONS > SAVE / RETRIEVE
        // ═══════════════════════════════════════

        private async void OptionsSaveOptions_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            _settingsService.Settings.Compiler = _compilerService.Settings;
            await _settingsService.SaveAsync();
            AddMessage("Options saved to disk.");
        }

        private async void OptionsRetrieveOptions_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            await _settingsService.LoadAsync();
            _compilerService.Settings = _settingsService.Settings.Compiler;
            _compilerService.DetectCompiler();
            AddMessage("Options retrieved from disk.");
        }

        // ═══════════════════════════════════════
        // PROJECT MENU
        // ═══════════════════════════════════════

        private void ProjectOpen_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Open Project: Project system not yet implemented. Use File > Open for individual .C files."); }
        private void ProjectClose_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Close Project: No project currently open."); }
        private void ProjectAddItem_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Add Item: Project system not yet implemented."); }
        private void ProjectDeleteItem_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Delete Item: Project system not yet implemented."); }
        private void ProjectLocalOptions_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Local Options: Project system not yet implemented."); }
        private void ProjectIncludeFiles_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Include Files: Project system not yet implemented. Use #include directives in your source."); }

        // ═══════════════════════════════════════
        // SYSTEM MENU
        // ═══════════════════════════════════════

        private void SysClearDesktop_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ClearMessages();
            AddMessage("Desktop cleared.");
        }

        private void SysRepaintDesktop_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            // Force a layout pass by toggling then restoring a value
            RootGrid.InvalidateArrange();
            AddMessage("Desktop repainted.");
        }
        private void ToggleMessageWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            MessagePanel.Visibility = MessagePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void WindowSizeMove_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Size/Move: Window resizing is handled by the OS title bar. Drag the window edge to resize."); }
        private void WindowZoom_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Zoom: Use the OS maximize button in the title bar."); }
        private void WindowTile_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Tile: Multi-window tiling is not supported. Use tabs to switch between open files."); }
        private void WindowCascade_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Cascade: Multi-window cascading is not supported. Use tabs to switch between open files."); }
        private void WindowOutput_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); ToggleMessageWindow_Click(sender, e); }
        private void WindowWatch_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Watch Panel: Requires GDB integration for variable watching."); }
        private void WindowRegister_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AddMessage("Register Panel: Requires GDB integration for register inspection."); }

        // ═══════════════════════════════════════
        // HELP MENU
        // ═══════════════════════════════════════

        private void HelpContents_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ClearMessages();
            AddMessage("═══════════════════════════════════════════════════════════════");
            AddMessage("                 RETROC IDE — HELP CONTENTS");
            AddMessage("═══════════════════════════════════════════════════════════════");
            AddMessage("");
            AddMessage(" KEYBOARD SHORTCUTS:");
            AddMessage("   F2           Save current file");
            AddMessage("   F3           Open file");
            AddMessage("   F9           Make (compile + link)");
            AddMessage("   Alt+F9       Compile to OBJ only");
            AddMessage("   Ctrl+F9      Run program");
            AddMessage("   Alt+F5       User screen");
            AddMessage("   Alt+X        Quit");
            AddMessage("   Ctrl+F       Find text");
            AddMessage("   Ctrl+H       Find and replace");
            AddMessage("   Ctrl+G       Go to line number");
            AddMessage("   Alt+F7       Previous error");
            AddMessage("   Alt+F8       Next error");
            AddMessage("   Ctrl+Z       Undo");
            AddMessage("   Ctrl+Y       Redo");
            AddMessage("   F1           Help");
            AddMessage("   Escape       Close dialog / menu");
            AddMessage("   F6           Next window");
            AddMessage("   Alt+1..9     Switch to window N");
            AddMessage("   Alt+F3       Close window");
            AddMessage("");
            AddMessage(" MENUS:");
            AddMessage("   File      — New, Open, Save, SaveAs, DOS Shell, Quit");
            AddMessage("   Edit      — Undo, Redo, Cut, Copy, Paste, Clear");
            AddMessage("   Search    — Find, Replace, Go to Line, Error navigation");
            AddMessage("   Run       — Run program, set Arguments");
            AddMessage("   Compile   — Compile to OBJ, Make EXE, Link, Build All");
            AddMessage("   Options   — Compiler settings, Directories");
            AddMessage("   Window    — Next, Previous, Close, Close All, List All");
            AddMessage("");
            AddMessage(" COMPILER: Uses bundled TCC (Tiny C Compiler)");
            AddMessage("   Supports #include <stdio.h> and standard C library.");
            AddMessage("   Configure alternative compiler in Options > Compiler.");
            AddMessage("═══════════════════════════════════════════════════════════════");
        }

        private void HelpIndex_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ClearMessages();
            AddMessage("═══ C KEYWORD INDEX ═══");
            AddMessage("auto  break  case  char  const  continue  default  do  double");
            AddMessage("else  enum  extern  float  for  goto  if  int  long  register");
            AddMessage("return  short  signed  sizeof  static  struct  switch  typedef");
            AddMessage("union  unsigned  void  volatile  while");
            AddMessage("");
            AddMessage("═══ STANDARD LIBRARY ═══");
            AddMessage("<stdio.h>   printf  scanf  fprintf  fscanf  fopen  fclose  fgets");
            AddMessage("<stdlib.h>  malloc  free  calloc  realloc  atoi  atof  exit  rand");
            AddMessage("<string.h>  strlen  strcpy  strcat  strcmp  memcpy  memset  strstr");
            AddMessage("<math.h>    sin  cos  tan  sqrt  pow  abs  ceil  floor  log");
            AddMessage("<ctype.h>   isalpha  isdigit  isalnum  toupper  tolower  isspace");
            AddMessage("<time.h>    time  clock  difftime  localtime  strftime");
        }

        // ═══════════════════════════════════════
        // SEARCH > FIND PROCEDURE
        // ═══════════════════════════════════════

        private void FindProcedure_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgFindProcedureBox.Text = string.Empty;
            FindProcedureDialogOverlay.Visibility = Visibility.Visible;
            DlgFindProcedureBox.Focus(FocusState.Programmatic);
        }

        private void DlgFindProcedure_OK(object sender, RoutedEventArgs e)
        {
            FindProcedureDialogOverlay.Visibility = Visibility.Collapsed;
            string name = DlgFindProcedureBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;

            var text = ActiveEditor.GetText();
            var lines = text.Split('\n');
            // Match lines that look like a function definition containing the name followed by '('
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (line.Contains(name + "(") && !line.TrimStart().StartsWith("//") && !line.TrimStart().StartsWith("*"))
                {
                    ActiveEditor.GoToLine(i + 1);
                    AddMessage($"Found procedure '{name}' at line {i + 1}.");
                    return;
                }
            }
            AddMessage($"Procedure '{name}' not found in current file.");
        }

        private void DlgFindProcedure_Cancel(object sender, RoutedEventArgs e) => FindProcedureDialogOverlay.Visibility = Visibility.Collapsed;

        // ═══════════════════════════════════════
        // HELP > TOPIC SEARCH / PREVIOUS / ON HELP
        // ═══════════════════════════════════════

        private void HelpTopicSearch_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            DlgTopicSearchBox.Text = string.Empty;
            TopicSearchDialogOverlay.Visibility = Visibility.Visible;
            DlgTopicSearchBox.Focus(FocusState.Programmatic);
        }

        private void DlgTopicSearch_OK(object sender, RoutedEventArgs e)
        {
            TopicSearchDialogOverlay.Visibility = Visibility.Collapsed;
            string topic = DlgTopicSearchBox.Text.Trim();
            if (string.IsNullOrEmpty(topic)) return;
            _lastHelpTopic = topic;
            ShowHelpForTopic(topic);
        }

        private void DlgTopicSearch_Cancel(object sender, RoutedEventArgs e) => TopicSearchDialogOverlay.Visibility = Visibility.Collapsed;

        private void HelpPreviousTopic_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            if (string.IsNullOrEmpty(_lastHelpTopic))
                HelpContents_Click(sender, e);
            else
                ShowHelpForTopic(_lastHelpTopic);
        }

        private void HelpOnHelp_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ClearMessages();
            AddMessage("═══ Help on Help ═══");
            AddMessage("");
            AddMessage("  F1              Help Contents — keyboard shortcuts and menu summary");
            AddMessage("  Shift+F1       Help Index — C keywords and standard library reference");
            AddMessage("  Ctrl+F1        Topic Search — look up a specific function or keyword");
            AddMessage("  Alt+F1         Previous Topic — return to the last viewed help topic");
            AddMessage("");
            AddMessage("  Tip: Click any error in the message panel to jump to that line.");
            AddMessage("  Tip: Use Search > Find Procedure to jump to a function definition.");
        }

        private void ShowHelpForTopic(string topic)
        {
            var topics = new System.Collections.Generic.Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["printf"]    = new[] { "printf — Formatted output to stdout", "  int printf(const char *format, ...);", "  Formats and prints to standard output. Returns number of chars written." },
                ["scanf"]     = new[] { "scanf — Formatted input from stdin", "  int scanf(const char *format, ...);", "  Reads formatted data from stdin. Returns number of items read." },
                ["fprintf"]   = new[] { "fprintf — Formatted output to a file", "  int fprintf(FILE *stream, const char *format, ...);", "  Like printf, but writes to the given file stream." },
                ["fscanf"]    = new[] { "fscanf — Formatted input from a file", "  int fscanf(FILE *stream, const char *format, ...);", "  Like scanf, but reads from the given file stream." },
                ["fopen"]     = new[] { "fopen — Open a file", "  FILE *fopen(const char *path, const char *mode);", "  Modes: \"r\" read, \"w\" write, \"a\" append, \"rb\"/\"wb\" binary. Returns NULL on failure." },
                ["fclose"]    = new[] { "fclose — Close a file", "  int fclose(FILE *stream);", "  Closes file and flushes buffers. Returns 0 on success." },
                ["fgets"]     = new[] { "fgets — Read a line from a file", "  char *fgets(char *s, int n, FILE *stream);", "  Reads up to n-1 characters. Returns NULL on error/EOF." },
                ["fputs"]     = new[] { "fputs — Write a string to a file", "  int fputs(const char *s, FILE *stream);", "  Writes string s to stream (no null terminator)." },
                ["malloc"]    = new[] { "malloc — Allocate memory", "  void *malloc(size_t size);", "  Allocates size bytes (uninitialized). Returns NULL on failure. Free with free()." },
                ["calloc"]    = new[] { "calloc — Allocate zero-initialized memory", "  void *calloc(size_t n, size_t size);", "  Allocates n*size bytes, all initialized to zero." },
                ["realloc"]   = new[] { "realloc — Resize allocated memory", "  void *realloc(void *ptr, size_t size);", "  Resizes the block pointed to by ptr to size bytes." },
                ["free"]      = new[] { "free — Free allocated memory", "  void free(void *ptr);", "  Releases memory allocated by malloc/calloc/realloc." },
                ["strlen"]    = new[] { "strlen — String length", "  size_t strlen(const char *s);", "  Returns number of characters before the null terminator." },
                ["strcpy"]    = new[] { "strcpy — Copy string", "  char *strcpy(char *dest, const char *src);", "  Copies src to dest including null terminator. Returns dest." },
                ["strcat"]    = new[] { "strcat — Concatenate strings", "  char *strcat(char *dest, const char *src);", "  Appends src to dest. Returns dest." },
                ["strcmp"]    = new[] { "strcmp — Compare strings", "  int strcmp(const char *s1, const char *s2);", "  Returns 0 if equal, <0 if s1<s2, >0 if s1>s2." },
                ["strchr"]    = new[] { "strchr — Find character in string", "  char *strchr(const char *s, int c);", "  Returns pointer to first occurrence of c, or NULL." },
                ["strstr"]    = new[] { "strstr — Find substring", "  char *strstr(const char *haystack, const char *needle);", "  Returns pointer to first occurrence of needle, or NULL." },
                ["memcpy"]    = new[] { "memcpy — Copy memory", "  void *memcpy(void *dest, const void *src, size_t n);", "  Copies n bytes. Regions must not overlap." },
                ["memset"]    = new[] { "memset — Fill memory", "  void *memset(void *s, int c, size_t n);", "  Fills n bytes of s with byte value c." },
                ["sqrt"]      = new[] { "sqrt — Square root", "  double sqrt(double x);", "  Returns square root of x. Requires <math.h>." },
                ["pow"]       = new[] { "pow — Power", "  double pow(double base, double exp);", "  Returns base raised to exp. Requires <math.h>." },
                ["sin"]       = new[] { "sin — Sine", "  double sin(double x);", "  Returns sine of x (radians). Requires <math.h>." },
                ["cos"]       = new[] { "cos — Cosine", "  double cos(double x);", "  Returns cosine of x (radians). Requires <math.h>." },
                ["tan"]       = new[] { "tan — Tangent", "  double tan(double x);", "  Returns tangent of x (radians). Requires <math.h>." },
                ["ceil"]      = new[] { "ceil — Round up", "  double ceil(double x);", "  Returns smallest integer >= x. Requires <math.h>." },
                ["floor"]     = new[] { "floor — Round down", "  double floor(double x);", "  Returns largest integer <= x. Requires <math.h>." },
                ["abs"]       = new[] { "abs — Absolute value (int)", "  int abs(int n);", "  Returns absolute value of n. Requires <stdlib.h>." },
                ["atoi"]      = new[] { "atoi — String to integer", "  int atoi(const char *s);", "  Converts string to int. Non-numeric characters stop parsing." },
                ["atof"]      = new[] { "atof — String to double", "  double atof(const char *s);", "  Converts string to double." },
                ["exit"]      = new[] { "exit — Terminate program", "  void exit(int status);", "  Terminates program, flushing buffers. status 0 = success." },
                ["isalpha"]   = new[] { "isalpha — Test if alphabetic", "  int isalpha(int c);", "  Non-zero if c is A-Z or a-z. Requires <ctype.h>." },
                ["isdigit"]   = new[] { "isdigit — Test if digit", "  int isdigit(int c);", "  Non-zero if c is 0-9. Requires <ctype.h>." },
                ["isspace"]   = new[] { "isspace — Test if whitespace", "  int isspace(int c);", "  Non-zero if c is space/tab/newline/etc. Requires <ctype.h>." },
                ["toupper"]   = new[] { "toupper — Convert to uppercase", "  int toupper(int c);", "  Returns uppercase equivalent of c. Requires <ctype.h>." },
                ["tolower"]   = new[] { "tolower — Convert to lowercase", "  int tolower(int c);", "  Returns lowercase equivalent of c. Requires <ctype.h>." },
                ["time"]      = new[] { "time — Get current time", "  time_t time(time_t *t);", "  Returns current calendar time. Requires <time.h>." },
                ["clock"]     = new[] { "clock — Processor time used", "  clock_t clock(void);", "  Divide by CLOCKS_PER_SEC for elapsed seconds. Requires <time.h>." },
                ["localtime"] = new[] { "localtime — Convert to local time", "  struct tm *localtime(const time_t *timep);", "  Converts time_t to broken-down local time struct. Requires <time.h>." },
                ["strftime"]  = new[] { "strftime — Format time as string", "  size_t strftime(char *s, size_t max, const char *format, const struct tm *tm);", "  Formats time struct into string, e.g. \"%Y-%m-%d %H:%M:%S\"." },
            };

            ClearMessages();
            AddMessage($"═══ Help: {topic} ═══");
            bool found = false;
            foreach (var kvp in topics)
            {
                if (kvp.Key.IndexOf(topic, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    topic.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (var line in kvp.Value) AddMessage(line);
                    AddMessage("");
                    found = true;
                }
            }
            var keywords = new[] { "auto", "break", "case", "char", "const", "continue", "default", "do",
                "double", "else", "enum", "extern", "float", "for", "goto", "if", "int", "long",
                "register", "return", "short", "signed", "sizeof", "static", "struct", "switch",
                "typedef", "union", "unsigned", "void", "volatile", "while" };
            foreach (var kw in keywords)
            {
                if (kw.Equals(topic, StringComparison.OrdinalIgnoreCase))
                {
                    AddMessage($"{kw} — C keyword. See a C language reference for syntax details.");
                    AddMessage("");
                    found = true;
                }
            }
            if (!found)
                AddMessage($"No help found for '{topic}'. Try: printf, malloc, strlen, fopen, sin, if, for, struct...");
        }

        // ═══════════════════════════════════════
        // ABOUT — inline overlay
        // ═══════════════════════════════════════

        private void About_Click(object sender, RoutedEventArgs e) { CloseAllMenus(); AboutDialogOverlay.Visibility = Visibility.Visible; }
        private void DlgAbout_OK(object sender, RoutedEventArgs e) => AboutDialogOverlay.Visibility = Visibility.Collapsed;

        // ═══════════════════════════════════════
        // MESSAGE LIST — click to navigate errors
        // ═══════════════════════════════════════

        private void MessageList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is string msg)
            {
                // Try to parse error line from message like "file.c:10:5: error: ..."
                var match = System.Text.RegularExpressions.Regex.Match(msg, @":(\d+):");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int line) && line > 0)
                {
                    ActiveEditor.GoToLine(line);
                }
            }
        }
    }
}
