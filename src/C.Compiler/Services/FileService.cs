using System;
using System.Diagnostics;
using System.Threading.Tasks;

using C.Compiler.Models;

using Microsoft.UI.Xaml;

using Windows.Storage;
using Windows.Storage.Pickers;

using WinRT.Interop;

namespace C.Compiler.Services
{
    public class FileService
    {
        private readonly Window _window;
        private int _newFileCounter = 0;

        public FileService(Window window)
        {
            _window = window;
        }

        public EditorDocument CreateNew()
        {
            _newFileCounter++;
            return new EditorDocument
            {
                FileName = _newFileCounter == 1 ? "NONAME.C" : $"NONAME{_newFileCounter:D2}.C",
                FilePath = null,
                Content = string.Empty,
                IsDirty = false
            };
        }

        public async Task<EditorDocument?> OpenFileAsync()
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".c");
            picker.FileTypeFilter.Add(".h");
            picker.FileTypeFilter.Add(".txt");
            picker.FileTypeFilter.Add("*");

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));

            var file = await picker.PickSingleFileAsync();
            if (file == null) return null;

            string content = await FileIO.ReadTextAsync(file);
            return new EditorDocument
            {
                FileName = file.Name.ToUpperInvariant(),
                FilePath = file.Path,
                Content = content,
                IsDirty = false
            };
        }

        public async Task<bool> SaveAsync(EditorDocument document)
        {
            if (document.IsNewFile)
            {
                return await SaveAsAsync(document);
            }

            try
            {
                await System.IO.File.WriteAllTextAsync(document.FilePath!, document.Content);
                document.IsDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileService] SaveAsync failed for '{document.FilePath}': {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SaveAsAsync(EditorDocument document)
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("C Source Files", new[] { ".c" });
            picker.FileTypeChoices.Add("C Header Files", new[] { ".h" });
            picker.FileTypeChoices.Add("All Files", new[] { ".txt" });
            picker.SuggestedFileName = document.FileName;

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));

            var file = await picker.PickSaveFileAsync();
            if (file == null) return false;

            try
            {
                await FileIO.WriteTextAsync(file, document.Content);
                document.FilePath = file.Path;
                document.FileName = file.Name.ToUpperInvariant();
                document.IsDirty = false;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileService] SaveAsAsync failed for '{document.FileName}': {ex.Message}");
                return false;
            }
        }

        public async Task<EditorDocument?> OpenFileAsync(string filePath)
        {
            try
            {
                string content = await System.IO.File.ReadAllTextAsync(filePath);
                string fileName = System.IO.Path.GetFileName(filePath).ToUpperInvariant();
                return new EditorDocument
                {
                    FileName = fileName,
                    FilePath = filePath,
                    Content = content,
                    IsDirty = false
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FileService] OpenFileAsync failed for '{filePath}': {ex.Message}");
                return null;
            }
        }
    }
}
