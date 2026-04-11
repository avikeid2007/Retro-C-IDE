using C.Compiler.AI.Models;

namespace C.Compiler.AI.Services;

/// <summary>
/// Contract for AI chat services. The main app references this interface;
/// the free build uses a no-op stub, the paid build uses LocalAIChatService.
/// </summary>
public interface IAIChatService
{
    bool IsModelLoaded { get; }
    bool IsModelDownloaded { get; }

    Task LoadModelAsync(IProgress<string>? progress = null);
    Task UnloadModelAsync();
    Task DownloadModelAsync(IProgress<(string status, double percent)>? progress = null);

    IAsyncEnumerable<string> SendMessageAsync(
        ChatConversation conversation,
        EditorContext? editorContext = null,
        CancellationToken cancellationToken = default);
}
