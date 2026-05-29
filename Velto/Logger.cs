using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Velto.Logging
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error,
        Fatal
    }

    public sealed class Logger : IDisposable
    {
        private readonly bool _logToFile;
        private readonly string? _filePath;
        private readonly object _lock = new();

        private StreamWriter? _writer;

        public LogLevel MinLevel { get; set; } = LogLevel.Trace;

        public Logger(bool logToFile = false, string? filePath = null)
        {
            _logToFile = logToFile;

            if (_logToFile)
            {
                _filePath = filePath ?? $"logs/log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";

                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);

                _writer = new StreamWriter(_filePath, append: true)
                {
                    AutoFlush = true
                };
            }
        }

        public void Log(
            LogLevel level,
            string message,
            [CallerMemberName] string member = "",
            [CallerFilePath] string file = "")
        {
            if (level < MinLevel)
                return;

            string className = Path.GetFileNameWithoutExtension(file);
            string time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            string line = $"[{time}] [{level}] [{className}] {message}";

            lock (_lock)
            {
                Console.WriteLine(line);

                if (_logToFile && _writer != null)
                    _writer.WriteLine(line);
            }
        }

        // Convenience methods
        public void Trace(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "")
            => Log(LogLevel.Trace, msg, m, f);

        public void Debug(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "")
            => Log(LogLevel.Debug, msg, m, f);

        public void Info(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "")
            => Log(LogLevel.Info, msg, m, f);

        public void Warn(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "")
            => Log(LogLevel.Warn, msg, m, f);

        public void Error(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "")
            => Log(LogLevel.Error, msg, m, f);

        public void Fatal(string msg, [CallerMemberName] string m = "", [CallerFilePath] string f = "")
            => Log(LogLevel.Fatal, msg, m, f);

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}