using System.Text;

namespace ProjectMaelstrom.Utilities;

/// <summary>Incremental owned-file transport. Partial records never reach the
/// consumer; truncation, malformed UTF-8 and budget overruns end the reader.</summary>
internal sealed class W101TraceTail(string path, Action<string> receive) : IDisposable
{
    private readonly byte[] buffer = new byte[4096];
    private readonly List<byte> pending = [];
    private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
    private FileStream? stream;
    private long total;
    private bool ended;
    internal void Poll()
    {
        if (ended) throw new InvalidOperationException("Trace transport ended.");
        try
        {
            if (stream is null)
            {
                if (!File.Exists(path)) return;
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            if (stream.Length < stream.Position) throw new InvalidDataException("Trace truncated.");
            // Bound work per poll, including when the producer gets ahead.
            for (int block = 0; block < 16; block++)
            {
                int count = stream.Read(buffer);
                if (count == 0) break;
                total += count;
                if (total > 8 * 1024 * 1024) throw new InvalidDataException("Trace budget exceeded.");
                for (int i = 0; i < count; i++)
                {
                    if (buffer[i] == 10)
                    {
                        string line = Utf8.GetString(pending.ToArray());
                        pending.Clear();
                        if (line.Length == 0) throw new InvalidDataException("Empty trace record.");
                        receive(line);
                    }
                    else
                    {
                        if (pending.Count >= 262144) throw new InvalidDataException("Trace record budget exceeded.");
                        pending.Add(buffer[i]);
                    }
                }
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        ended = true;
        pending.Clear();
        stream?.Dispose();
    }
}
