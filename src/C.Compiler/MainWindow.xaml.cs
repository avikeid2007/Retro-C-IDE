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
            Title = "Turbo C";
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
            _ = InitAsync();
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

            if (doc.IsNewFile)
            {
                if (!await _fileService.SaveAsAsync(doc)) { AddMessage("Compilation cancelled — file not saved."); return; }
            }
            else if (doc.IsDirty)
            {
                await _fileService.SaveAsync(doc);
            }

            ClearMessages();
            AddMessage($"Compiling {doc.FilePath}...");

            var result = compileOnly
                ? await _compilerService.CompileAsync(doc.FilePath!)
                : await _compilerService.MakeAsync(doc.FilePath!);

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
        private void ToggleMessageWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            MessagePanel.Visibility = MessagePanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        // ═══════════════════════════════════════
        // HELP MENU
        // ═══════════════════════════════════════

        private void HelpContents_Click(object sender, RoutedEventArgs e)
        {
            CloseAllMenus();
            ClearMessages();
            AddMessage("═══════════════════════════════════════════════════════════════");
            AddMessage("                 TURBO C 3.0 HELP — CONTENTS");
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
