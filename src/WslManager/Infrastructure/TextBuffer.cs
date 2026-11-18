using System.Collections.Concurrent;
using System.Text;
using System.Windows.Threading;

namespace WslManager.Infrastructure;

/// <summary>
/// Thread-safe, throttled text accumulator for log/console output. Lines may be appended
/// from any thread; the UI is updated at most every ~120 ms.
/// </summary>
public sealed class TextBuffer
{
    private readonly ConcurrentQueue<string> _pending = new();
    private readonly StringBuilder _sb = new();
    private readonly Action<string> _publish;
    private readonly int _maxChars;
    private readonly DispatcherTimer _timer;

    public TextBuffer(Action<string> publish, int maxChars = 400_000)
    {
        _publish = publish;
        _maxChars = maxChars;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(120), DispatcherPriority.Background, (_, _) => Flush(), Dispatcher.CurrentDispatcher);
        _timer.Start();
    }

    public void AppendLine(string line) => _pending.Enqueue(line);

    public void Clear()
    {
        while (_pending.TryDequeue(out _)) { }
        _sb.Clear();
        _publish(string.Empty);
    }

    private void Flush()
    {
        if (_pending.IsEmpty) return;
        while (_pending.TryDequeue(out var line)) _sb.Append(line).Append('\n');
        if (_sb.Length > _maxChars)
        {
            var cut = _sb.Length - _maxChars * 3 / 4;
            var nl = _sb.ToString(cut, Math.Min(500, _sb.Length - cut)).IndexOf('\n');
            _sb.Remove(0, cut + Math.Max(nl + 1, 0));
        }
        _publish(_sb.ToString());
    }
}
