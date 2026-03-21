using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using C.Compiler.Models;

namespace C.Compiler.Services
{
    public class CompilerService
    {
        private readonly ProcessRunner _processRunner = new();
        private CompilerSettings _settings = new();

        public CompilerSettings Settings
        {
            get => _settings;
            set => _settings = value;
        }

        public string? DetectedCompilerPath { get; private set; }
        public string DetectedCompilerType { get; private set; } = "none";

        private readonly TccManager _tccManager = new();
        public TccManager BundledTcc => _tccManager;

        public bool DetectCompiler()
        {
            // 1. Check for bundled TCC (auto-downloaded)
            if (_tccManager.IsInstalled)
            {
                DetectedCompilerPath = _tccManager.TccExePath;
                DetectedCompilerType = "tcc";
                return true;
            }

            // 2. Check for GCC/MinGW on PATH
            string? gccPath = FindOnPath("gcc.exe");
            if (gccPath != null)
            {
                DetectedCompilerPath = gccPath;
                DetectedCompilerType = "gcc";
                return true;
            }

            // Check for TCC on PATH
            string? tccPath = FindOnPath("tcc.exe");
            if (tccPath != null)
            {
                DetectedCompilerPath = tccPath;
                DetectedCompilerType = "tcc";
                return true;
            }

            // Check for MSVC cl.exe on PATH
            string? clPath = FindOnPath("cl.exe");
            if (clPath != null)
            {
                DetectedCompilerPath = clPath;
                DetectedCompilerType = "msvc";
                return true;
            }

            return false;
        }

        public bool HasCompiler => DetectedCompilerPath != null 
            || (!string.IsNullOrEmpty(_settings.CompilerPath) && File.Exists(_settings.CompilerPath));

        public string GetCompilerPath()
        {
            if (!string.IsNullOrEmpty(_settings.CompilerPath) && File.Exists(_settings.CompilerPath))
                return _settings.CompilerPath;

            if (DetectedCompilerPath != null)
                return DetectedCompilerPath;

            // Try bundled TCC as last resort
            if (_tccManager.IsInstalled)
                return _tccManager.TccExePath;

            return "gcc"; // fallback
        }

        public string GetCompilerType()
        {
            if (_settings.CompilerType != "auto" && !string.IsNullOrEmpty(_settings.CompilerType))
                return _settings.CompilerType;

            return DetectedCompilerType;
        }

        public async Task<CompileResult> CompileAsync(string sourceFilePath)
        {
            if (!HasCompiler)
            {
                return new CompileResult
                {
                    Success = false,
                    RawOutput = "No C compiler available. The app will try to download TCC on next restart, or configure manually in Options > Compiler.",
                    Errors = new List<CompilerError> { new CompilerError { Message = "No compiler found", Severity = CompilerErrorSeverity.Error, Line = 0 } }
                };
            }

            string compiler = GetCompilerPath();
            string compilerType = GetCompilerType();
            string outputDir = !string.IsNullOrEmpty(_settings.OutputDirectory) 
                ? _settings.OutputDirectory 
                : Path.GetDirectoryName(sourceFilePath) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string objPath = Path.Combine(outputDir, baseName + ".obj");

            string args = compilerType switch
            {
                "gcc" => BuildGccCompileArgs(sourceFilePath, objPath),
                "tcc" => BuildTccCompileArgs(sourceFilePath, objPath),
                "msvc" => BuildMsvcCompileArgs(sourceFilePath, objPath),
                _ => BuildGccCompileArgs(sourceFilePath, objPath)
            };

            var result = await _processRunner.RunAsync(compiler, args, Path.GetDirectoryName(sourceFilePath));

            return new CompileResult
            {
                Success = result.Success,
                OutputPath = objPath,
                RawOutput = result.StandardOutput + result.StandardError,
                Errors = ParseErrors(result.StandardError + result.StandardOutput, sourceFilePath)
            };
        }

        public async Task<CompileResult> MakeAsync(string sourceFilePath)
        {
            if (!HasCompiler)
            {
                return new CompileResult
                {
                    Success = false,
                    RawOutput = "No C compiler available. The app will try to download TCC on next restart, or configure manually in Options > Compiler.",
                    Errors = new List<CompilerError> { new CompilerError { Message = "No compiler found", Severity = CompilerErrorSeverity.Error, Line = 0 } }
                };
            }

            string compiler = GetCompilerPath();
            string compilerType = GetCompilerType();
            string outputDir = !string.IsNullOrEmpty(_settings.OutputDirectory) 
                ? _settings.OutputDirectory 
                : Path.GetDirectoryName(sourceFilePath) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string exePath = Path.Combine(outputDir, baseName + ".exe");

            string args = compilerType switch
            {
                "gcc" => BuildGccMakeArgs(sourceFilePath, exePath),
                "tcc" => BuildTccMakeArgs(sourceFilePath, exePath),
                "msvc" => BuildMsvcMakeArgs(sourceFilePath, exePath),
                _ => BuildGccMakeArgs(sourceFilePath, exePath)
            };

            var result = await _processRunner.RunAsync(compiler, args, Path.GetDirectoryName(sourceFilePath));

            return new CompileResult
            {
                Success = result.Success,
                OutputPath = exePath,
                RawOutput = result.StandardOutput + result.StandardError,
                Errors = ParseErrors(result.StandardError + result.StandardOutput, sourceFilePath)
            };
        }

        public void RunExecutable(string exePath, string arguments = "")
        {
            _processRunner.RunInConsole(exePath, arguments, Path.GetDirectoryName(exePath));
        }

        private string BuildGccCompileArgs(string source, string output)
        {
            var args = $"-c \"{source}\" -o \"{output}\"";
            if (!string.IsNullOrEmpty(_settings.IncludeDirectories))
            {
                foreach (var dir in _settings.IncludeDirectories.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    args += $" -I\"{dir.Trim()}\"";
            }
            if (!string.IsNullOrEmpty(_settings.AdditionalFlags))
                args += $" {_settings.AdditionalFlags}";
            return args;
        }

        private string BuildGccMakeArgs(string source, string output)
        {
            var args = $"\"{source}\" -o \"{output}\"";
            if (!string.IsNullOrEmpty(_settings.IncludeDirectories))
            {
                foreach (var dir in _settings.IncludeDirectories.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    args += $" -I\"{dir.Trim()}\"";
            }
            if (!string.IsNullOrEmpty(_settings.LibraryDirectories))
            {
                foreach (var dir in _settings.LibraryDirectories.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    args += $" -L\"{dir.Trim()}\"";
            }
            if (!string.IsNullOrEmpty(_settings.AdditionalFlags))
                args += $" {_settings.AdditionalFlags}";
            return args;
        }

        private string BuildTccCompileArgs(string source, string output)
        {
            return $"-c \"{source}\" -o \"{output}\"";
        }

        private string BuildTccMakeArgs(string source, string output)
        {
            return $"\"{source}\" -o \"{output}\"";
        }

        private string BuildMsvcCompileArgs(string source, string output)
        {
            return $"/c \"{source}\" /Fo\"{output}\"";
        }

        private string BuildMsvcMakeArgs(string source, string output)
        {
            return $"\"{source}\" /Fe\"{output}\"";
        }

        private List<CompilerError> ParseErrors(string output, string defaultFile)
        {
            var errors = new List<CompilerError>();
            if (string.IsNullOrWhiteSpace(output)) return errors;

            // GCC/TCC format: file.c:line:col: error: message
            var gccPattern = new Regex(@"^(.+?):(\d+):(?:(\d+):)?\s*(error|warning):\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            foreach (Match match in gccPattern.Matches(output))
            {
                errors.Add(new CompilerError
                {
                    FilePath = match.Groups[1].Value.Trim(),
                    Line = int.Parse(match.Groups[2].Value),
                    Column = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 1,
                    Message = match.Groups[5].Value.Trim(),
                    Severity = match.Groups[4].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
                        ? CompilerErrorSeverity.Error
                        : CompilerErrorSeverity.Warning
                });
            }

            // MSVC format: file.c(line): error Cxxxx: message
            if (errors.Count == 0)
            {
                var msvcPattern = new Regex(@"^(.+?)\((\d+)\)\s*:\s*(error|warning)\s+\w+\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                foreach (Match match in msvcPattern.Matches(output))
                {
                    errors.Add(new CompilerError
                    {
                        FilePath = match.Groups[1].Value.Trim(),
                        Line = int.Parse(match.Groups[2].Value),
                        Column = 1,
                        Message = match.Groups[4].Value.Trim(),
                        Severity = match.Groups[3].Value.Equals("error", StringComparison.OrdinalIgnoreCase)
                            ? CompilerErrorSeverity.Error
                            : CompilerErrorSeverity.Warning
                    });
                }
            }

            return errors;
        }

        private string? FindOnPath(string exeName)
        {
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (pathEnv == null) return null;

            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                string fullPath = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(fullPath)) return fullPath;
            }
            return null;
        }
    }

    public class CompileResult
    {
        public bool Success { get; set; }
        public string OutputPath { get; set; } = string.Empty;
        public string RawOutput { get; set; } = string.Empty;
        public List<CompilerError> Errors { get; set; } = new();
    }
}
