using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Tcrfc.Api.Tests.Fixtures;

/// <summary>
/// 把測試主機記下的 Error 以上日誌（含例外）收進記憶體，讓「回了 500 但看不到為什麼」的測試能把真正的例外放進失敗訊息。
/// ApiExceptionHandler 刻意不把例外細節回給呼叫端，所以 CI 上只看到狀態碼；這裡補上測試側的可觀測性（E-335：結帳並行測試曾在 CI 回 500 而無從診斷）。
/// 只有明確呼叫 <see cref="Snapshot"/> 的測試才會用到，其他測試不受影響。
/// </summary>
public sealed class ServerErrorLog : ILoggerProvider
{
    private static readonly ConcurrentQueue<string> Entries = new();
    private const int MaxEntries = 200;

    public ILogger CreateLogger(string categoryName) => new Capture(categoryName);

    public void Dispose()
    {
    }

    public static string Snapshot() => string.Join("\n----\n", Entries);

    private sealed class Capture(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            Entries.Enqueue($"[{category}] {formatter(state, exception)}\n{exception}");
            while (Entries.Count > MaxEntries && Entries.TryDequeue(out _))
            {
            }
        }
    }
}
