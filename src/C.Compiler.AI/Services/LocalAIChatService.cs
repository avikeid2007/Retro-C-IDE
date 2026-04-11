using System.Runtime.CompilerServices;
using C.Compiler.AI.Models;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace C.Compiler.AI.Services;

/// <summary>
/// Local AI chat service powered by LLamaSharp. Loads a GGUF model and
/// provides streaming chat completions using the editor context.
/// </summary>
public class LocalAIChatService : IAIChatService, IDisposable
{
    private readonly LlamaModelManager _modelManager = new();
    private LLamaWeights? _model;
    private ModelParams? _modelParams;
    private bool _disposed;

    // Settings applied at load time
    private int _gpuLayerCount;
    private uint _contextSize = 4096;
    private int _maxTokens = 2048;
    private float _temperature = 0.3f;

    public bool IsModelLoaded => _model != null;
    public bool IsModelDownloaded => _modelManager.IsModelDownloaded;
    public LlamaModelManager ModelManager => _modelManager;

    /// <summary>
    /// Configure model parameters before calling LoadModelAsync.
    /// </summary>
    public void Configure(string? customModelPath = null, int gpuLayerCount = 0,
        uint contextSize = 4096, int maxTokens = 2048, float temperature = 0.3f)
    {
        _modelManager.CustomModelPath = string.IsNullOrWhiteSpace(customModelPath) ? null : customModelPath;
        _gpuLayerCount = gpuLayerCount;
        _contextSize = contextSize;
        _maxTokens = maxTokens;
        _temperature = temperature;
    }

    public async Task DownloadModelAsync(IProgress<(string status, double percent)>? progress = null)
    {
        await _modelManager.DownloadModelAsync(progress);
    }

    public Task LoadModelAsync(IProgress<string>? progress = null)
    {
        if (_model != null)
        {
            progress?.Report("Model already loaded.");
            return Task.CompletedTask;
        }

        var modelPath = _modelManager.ResolvedModelPath;
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("Model file not found. Please download the model first.", modelPath);

        progress?.Report("Loading model...");

        var modelParams = new ModelParams(modelPath)
        {
            ContextSize = _contextSize,
            BatchSize = _contextSize,
            GpuLayerCount = _gpuLayerCount,
        };

        _model = LLamaWeights.LoadFromFile(modelParams);
        _modelParams = modelParams;

        progress?.Report("Model loaded and ready.");
        return Task.CompletedTask;
    }

    public Task UnloadModelAsync()
    {
        _model?.Dispose();
        _model = null;
        _modelParams = null;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> SendMessageAsync(
        ChatConversation conversation,
        EditorContext? editorContext = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_model == null)
            throw new InvalidOperationException("Model is not loaded. Call LoadModelAsync first.");

        // Build the full prompt from conversation history
        var prompt = BuildPrompt(conversation, editorContext);

        var executor = new StatelessExecutor(_model, _modelParams!);
        var inferenceParams = new InferenceParams
        {
            MaxTokens = _maxTokens,
            AntiPrompts = new[] { "User:", "\nUser:" },
            SamplingPipeline = new DefaultSamplingPipeline { Temperature = _temperature },
        };

        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            yield return token;
        }
    }

    private static string BuildPrompt(ChatConversation conversation, EditorContext? editorContext)
    {
        var systemPrompt = CodeContextBuilder.GetSystemPrompt(conversation.Mode);
        var contextBlock = CodeContextBuilder.BuildContextBlock(editorContext);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<|system|>\n{systemPrompt}{contextBlock}\n<|end|>");

        foreach (var msg in conversation.Messages)
        {
            if (msg.Role == ChatRole.System)
                continue; // already in system block

            var role = msg.Role == ChatRole.User ? "user" : "assistant";
            sb.AppendLine($"<|{role}|>\n{msg.Content}\n<|end|>");
        }

        sb.Append("<|assistant|>\n");
        return sb.ToString();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true; // set before disposing to prevent double-dispose on concurrent calls
            _model?.Dispose();
            _model = null;
            _modelParams = null;
        }
        GC.SuppressFinalize(this);
    }
}
