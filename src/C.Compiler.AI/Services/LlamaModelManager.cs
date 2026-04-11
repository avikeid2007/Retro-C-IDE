using System.Net.Http;

namespace C.Compiler.AI.Services;

/// <summary>
/// Manages downloading and locating the GGUF model file on disk.
/// Default model: Phi-3 mini Q4_K_M (~2.3 GB).
/// </summary>
public class LlamaModelManager
{
    private const string DefaultModelUrl =
        "https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf/resolve/main/Phi-3-mini-4k-instruct-q4.gguf";

    private const string DefaultModelFileName = "Phi-3-mini-4k-instruct-q4.gguf";

    private const int BufferSize = 1024 * 1024; // 1 MB

    private static readonly string ModelsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RetroC-IDE", "models");

    public string ModelPath => Path.Combine(ModelsDirectory, DefaultModelFileName);

    public bool IsModelDownloaded => File.Exists(ModelPath);

    /// <summary>
    /// Allows the user to point to a custom GGUF model file.
    /// </summary>
    public string? CustomModelPath { get; set; }

    public string ResolvedModelPath => CustomModelPath ?? ModelPath;

    public async Task DownloadModelAsync(IProgress<(string status, double percent)>? progress = null, CancellationToken ct = default)
    {
        if (IsModelDownloaded)
        {
            progress?.Report(("Model already downloaded.", 100));
            return;
        }

        Directory.CreateDirectory(ModelsDirectory);

        var tempPath = ModelPath + ".downloading";

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromHours(2);

        using var response = await httpClient.GetAsync(DefaultModelUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var downloaded = 0L;

        using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);

        var buffer = new byte[BufferSize];
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloaded += bytesRead;

            if (totalBytes > 0)
            {
                var pct = (double)downloaded / totalBytes * 100;
                var downloadedMB = downloaded / (1024.0 * 1024.0);
                var totalMB = totalBytes / (1024.0 * 1024.0);
                progress?.Report(($"Downloading: {downloadedMB:F0} / {totalMB:F0} MB", pct));
            }
            else
            {
                var downloadedMB = downloaded / (1024.0 * 1024.0);
                progress?.Report(($"Downloading: {downloadedMB:F1} MB", -1));
            }
        }

        fileStream.Close();
        File.Move(tempPath, ModelPath, overwrite: true);
        progress?.Report(("Download complete.", 100));
    }

    public void DeleteModel()
    {
        if (File.Exists(ModelPath))
            File.Delete(ModelPath);
    }
}
