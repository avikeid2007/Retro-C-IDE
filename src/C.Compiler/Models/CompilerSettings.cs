namespace C.Compiler.Models
{
    public class CompilerSettings
    {
        public string CompilerPath { get; set; } = string.Empty;
        public string CompilerType { get; set; } = "auto"; // auto, gcc, tcc, msvc
        public string IncludeDirectories { get; set; } = string.Empty;
        public string LibraryDirectories { get; set; } = string.Empty;
        public string OutputDirectory { get; set; } = string.Empty;
        public string AdditionalFlags { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30; // Compilation timeout in seconds
    }
}
