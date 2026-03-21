using System;
using System.IO;
using System.Threading.Tasks;

namespace C.Compiler.Services
{
    public class TccManager
    {
        private const string TccExeName = "tcc.exe";

        // Bundled TCC: lives in tcc/ subfolder next to the app executable
        private static readonly string BundledTccDir = Path.Combine(
            AppContext.BaseDirectory, "tcc");

        public string TccDirectory => BundledTccDir;
        public string TccExePath => Path.Combine(BundledTccDir, TccExeName);

        public bool IsInstalled => File.Exists(TccExePath);

        /// <summary>
        /// TCC is now bundled with the app at build time.
        /// This method just verifies it's present.
        /// </summary>
        public Task<string?> EnsureInstalledAsync(Action<string>? progress = null)
        {
            if (IsInstalled)
            {
                progress?.Invoke($"Bundled TCC ready: {TccExePath}");

                // Verify libtcc.dll is present
                string libtccPath = Path.Combine(BundledTccDir, "libtcc.dll");
                if (!File.Exists(libtccPath))
                    progress?.Invoke("Warning: libtcc.dll not found alongside tcc.exe!");

                return Task.FromResult<string?>(TccExePath);
            }

            progress?.Invoke($"Error: Bundled TCC not found at {BundledTccDir}");
            progress?.Invoke("Rebuild the project — TCC is auto-downloaded during build.");
            return Task.FromResult<string?>(null);
        }
    }
}
