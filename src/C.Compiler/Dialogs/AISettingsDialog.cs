using System;
using System.Threading;

using C.Compiler.Models;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Windows.Storage.Pickers;

namespace C.Compiler.Dialogs
{
    public sealed partial class AISettingsDialog : ContentDialog
    {
        private CancellationTokenSource? _downloadCts;
        private bool _isDownloading;

        /// <summary>
        /// Set by the host so the dialog can check model status and trigger downloads.
        /// </summary>
        public Func<bool>? IsModelDownloaded { get; set; }
        public Func<string>? GetDefaultModelPath { get; set; }
        public Func<IProgress<(string status, double percent)>, CancellationToken, System.Threading.Tasks.Task>? DownloadModelFunc { get; set; }
        public Action? DeleteModelFunc { get; set; }

        public AISettingsDialog()
        {
            InitializeComponent();
        }

        public void LoadSettings(AISettings settings)
        {
            CustomModelPathBox.Text = settings.CustomModelPath;
            GpuLayersBox.Value = settings.GpuLayerCount;
            MaxTokensBox.Value = settings.MaxTokens;
            TemperatureSlider.Value = settings.Temperature;
            TempValueText.Text = settings.Temperature.ToString("F1");

            // Select context size
            var ctxStr = settings.ContextSize.ToString();
            for (int i = 0; i < ContextSizeCombo.Items.Count; i++)
            {
                if (ContextSizeCombo.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == ctxStr)
                {
                    ContextSizeCombo.SelectedIndex = i;
                    break;
                }
            }
            if (ContextSizeCombo.SelectedIndex < 0)
                ContextSizeCombo.SelectedIndex = 1; // default 4096

            UpdateModelStatus();
        }

        public AISettings GetSettings()
        {
            uint contextSize = 4096;
            if (ContextSizeCombo.SelectedItem is ComboBoxItem ctxItem)
                uint.TryParse(ctxItem.Tag?.ToString(), out contextSize);

            return new AISettings
            {
                CustomModelPath = CustomModelPathBox.Text.Trim(),
                GpuLayerCount = (int)GpuLayersBox.Value,
                ContextSize = contextSize,
                MaxTokens = (int)MaxTokensBox.Value,
                Temperature = (float)TemperatureSlider.Value,
            };
        }

        private void UpdateModelStatus()
        {
            var downloaded = IsModelDownloaded?.Invoke() ?? false;
            var defaultPath = GetDefaultModelPath?.Invoke() ?? "(unknown)";
            var customPath = CustomModelPathBox.Text.Trim();

            if (!string.IsNullOrEmpty(customPath))
            {
                var exists = System.IO.File.Exists(customPath);
                ModelStatusText.Text = exists ? "✓ Custom model loaded" : "✗ Custom model file not found";
                ModelStatusText.Foreground = exists
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0))
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 85, 85));
                DownloadBtn.Visibility = Visibility.Collapsed;
                DeleteBtn.Visibility = Visibility.Collapsed;
                ModelInfoText.Text = $"Custom: {customPath}";
            }
            else if (downloaded)
            {
                ModelStatusText.Text = "✓ Default model downloaded";
                ModelStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 255, 0));
                DownloadBtn.Visibility = Visibility.Collapsed;
                DeleteBtn.Visibility = Visibility.Visible;

                try
                {
                    var fi = new System.IO.FileInfo(defaultPath);
                    var sizeMB = fi.Length / (1024.0 * 1024.0);
                    ModelInfoText.Text = $"Path: {defaultPath}\nSize: {sizeMB:F0} MB";
                }
                catch
                {
                    ModelInfoText.Text = $"Path: {defaultPath}";
                }
            }
            else
            {
                ModelStatusText.Text = "✗ Model not downloaded";
                ModelStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 85));
                DownloadBtn.Visibility = Visibility.Visible;
                DeleteBtn.Visibility = Visibility.Collapsed;
                ModelInfoText.Text = "Default: Phi-3 mini 4K instruct Q4 (~2.3 GB)\nWill download from HuggingFace.";
            }
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading || DownloadModelFunc == null) return;

            _isDownloading = true;
            _downloadCts = new CancellationTokenSource();
            DownloadBtn.IsEnabled = false;
            DownloadProgress.Visibility = Visibility.Visible;
            DownloadStatusText.Visibility = Visibility.Visible;

            // Disable OK/Cancel while downloading
            IsPrimaryButtonEnabled = false;

            var progress = new Progress<(string status, double percent)>(p =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    DownloadProgress.Value = p.percent;
                    DownloadStatusText.Text = p.status;
                });
            });

            try
            {
                await DownloadModelFunc(progress, _downloadCts.Token);
                DownloadStatusText.Text = "Download complete!";
                UpdateModelStatus();
            }
            catch (OperationCanceledException)
            {
                DownloadStatusText.Text = "Download cancelled.";
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                _isDownloading = false;
                DownloadBtn.IsEnabled = true;
                IsPrimaryButtonEnabled = true;
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DeleteModelFunc?.Invoke();
                UpdateModelStatus();
                DownloadStatusText.Text = "Model deleted.";
                DownloadStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                DownloadStatusText.Text = $"Error: {ex.Message}";
                DownloadStatusText.Visibility = Visibility.Visible;
            }
        }

        private async void Browse_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".gguf");

            // Initialize with the window handle
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                CustomModelPathBox.Text = file.Path;
                UpdateModelStatus();
            }
        }

        private void Temperature_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (TempValueText != null)
                TempValueText.Text = e.NewValue.ToString("F1");
        }
    }
}
