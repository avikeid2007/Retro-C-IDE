namespace C.Compiler.Models
{
    /// <summary>
    /// Persisted settings for the local AI features.
    /// Lives in main project so AppSettings can serialize it even in non-AI builds.
    /// </summary>
    public class AISettings
    {
        /// <summary>Custom path to a GGUF model file. Empty = use default Phi-3 in %LOCALAPPDATA%.</summary>
        public string CustomModelPath { get; set; } = string.Empty;

        /// <summary>Number of GPU layers to offload (0 = CPU only).</summary>
        public int GpuLayerCount { get; set; }

        /// <summary>Context window size in tokens.</summary>
        public uint ContextSize { get; set; } = 4096;

        /// <summary>Max tokens for AI response generation.</summary>
        public int MaxTokens { get; set; } = 2048;

        /// <summary>Temperature for sampling (0.0 = deterministic, 1.0 = creative).</summary>
        public float Temperature { get; set; } = 0.3f;
    }
}
