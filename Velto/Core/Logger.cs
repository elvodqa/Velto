using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace Velto.Core
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
        private static readonly Lazy<Logger> _instance =
            new(() => new Logger());

        public static Logger Instance => _instance.Value;

        private readonly object _lock = new();

        private StreamWriter? _writer;
        private bool _disposed;

        public LogLevel MinLevel { get; set; } = LogLevel.Trace;

        public bool LogToFile { get; private set; }
        public string? FilePath { get; private set; }

        private Logger()
        {
        }

        public void EnableFileLogging(string? filePath = null)
        {
            lock (_lock)
            {
                if (_writer != null)
                    return;

                LogToFile = true;
                FilePath = filePath ?? $"logs/log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";

                string? directory = Path.GetDirectoryName(FilePath);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                _writer = new StreamWriter(FilePath, append: true)
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
                _writer?.WriteLine(line);
            }
        }

        public void Trace(string msg,
            [CallerMemberName] string m = "",
            [CallerFilePath] string f = "")
            => Log(LogLevel.Trace, msg, m, f);

        public void Debug(string msg,
            [CallerMemberName] string m = "",
            [CallerFilePath] string f = "")
            => Log(LogLevel.Debug, msg, m, f);

        public void Info(string msg,
            [CallerMemberName] string m = "",
            [CallerFilePath] string f = "")
            => Log(LogLevel.Info, msg, m, f);

        public void Warn(string msg,
            [CallerMemberName] string m = "",
            [CallerFilePath] string f = "")
            => Log(LogLevel.Warn, msg, m, f);

        public void Error(string msg,
            [CallerMemberName] string m = "",
            [CallerFilePath] string f = "")
            => Log(LogLevel.Error, msg, m, f);

        public void Fatal(string msg,
            [CallerMemberName] string m = "",
            [CallerFilePath] string f = "")
            => Log(LogLevel.Fatal, msg, m, f);

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _writer?.Dispose();
                _writer = null;
                _disposed = true;
            }
        }
    }
}