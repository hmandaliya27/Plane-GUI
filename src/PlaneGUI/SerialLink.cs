using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Ports;
using System.Text;

namespace PlaneGUI;

/// <summary>
/// Reads "Key=Value" lines from a serial port (e.g. an Arduino + IMU) on the
/// port's background thread and keeps the latest value for each key.
/// The UI thread polls <see cref="TryGet"/> and <see cref="DrainLines"/> from a timer,
/// so no controls are ever touched off the UI thread.
///
/// Accepted line formats (keys are case-insensitive):
///   GyroX=12.5
///   Roll=12.5,Pitch=-3.0,Heading=270
///   roll:12.5 pitch:-3.0
/// </summary>
public sealed class SerialLink : IDisposable
{
    readonly SerialPort port;
    readonly ConcurrentDictionary<string, double> latest = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentQueue<string> lines = new();
    readonly StringBuilder pending = new();
    long lastDataTicks;

    static readonly char[] FieldSeparators = { ',', ';', ' ', '\t' };
    static readonly char[] KeyValueSeparators = { '=', ':' };

    public SerialLink(string portName, int baudRate)
    {
        port = new SerialPort(portName, baudRate)
        {
            DtrEnable = true, // most Arduino boards need DTR to start sending
            ReadTimeout = 500,
        };
        port.DataReceived += OnDataReceived;
    }

    public string PortName => port.PortName;
    public int BaudRate => port.BaudRate;
    public bool IsOpen => port.IsOpen;

    /// <summary>Time since the last complete line arrived, or null if none yet.</summary>
    public TimeSpan? SinceLastData
    {
        get
        {
            long ticks = Interlocked.Read(ref lastDataTicks);
            return ticks == 0 ? null : DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    public void Open() => port.Open();

    /// <summary>Returns the latest value for the first of <paramref name="keys"/> that has been received.</summary>
    public bool TryGet(out double value, params string[] keys)
    {
        foreach (var key in keys)
            if (latest.TryGetValue(key, out value))
                return true;
        value = 0;
        return false;
    }

    /// <summary>Removes and returns raw lines received since the last call.</summary>
    public List<string> DrainLines(int max = 100)
    {
        var result = new List<string>();
        while (result.Count < max && lines.TryDequeue(out var line))
            result.Add(line);
        return result;
    }

    void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        string chunk;
        try
        {
            if (!port.IsOpen) return;
            chunk = port.ReadExisting();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
        {
            return;
        }

        // Only this (serialised) event handler touches `pending`.
        pending.Append(chunk);
        while (true)
        {
            string buffered = pending.ToString();
            int newline = buffered.IndexOf('\n');
            if (newline < 0)
            {
                // Guard against a device that never sends newlines.
                if (pending.Length > 4096) pending.Clear();
                break;
            }
            string line = buffered[..newline].Trim('\r', ' ', '\t');
            pending.Remove(0, newline + 1);
            if (line.Length > 0) Parse(line);
        }
    }

    void Parse(string line)
    {
        lines.Enqueue(line);
        while (lines.Count > 500) lines.TryDequeue(out _);

        foreach (var field in line.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = field.Split(KeyValueSeparators, 2);
            if (parts.Length == 2 &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                latest[parts[0].Trim()] = v;
            }
        }
        Interlocked.Exchange(ref lastDataTicks, DateTime.UtcNow.Ticks);
    }

    public void Dispose()
    {
        port.DataReceived -= OnDataReceived;
        try { if (port.IsOpen) port.Close(); } catch (IOException) { }
        port.Dispose();
    }
}
