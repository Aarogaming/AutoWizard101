using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Maelstrom.DeckApi;

/// <summary>Shared local transport. No game logic, HTTP listener, retries or host auto-start.</summary>
internal static class JobDeckWire
{
    internal const int MaximumBytes = 262144;
    internal static string PipeName(string dataRoot) => "maelstrom-deck-" + Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot)).ToUpperInvariant())))[..24];
    internal static async Task<byte[]> ReadAsync(Stream stream, CancellationToken token)
    {
        var length = new byte[4]; await stream.ReadExactlyAsync(length, token).ConfigureAwait(false);
        var count = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(length);
        if (count is < 2 or > MaximumBytes) throw new InvalidDataException("Deck API message is outside its size budget.");
        var bytes = new byte[count]; await stream.ReadExactlyAsync(bytes, token).ConfigureAwait(false); return bytes;
    }
    internal static async Task WriteAsync(Stream stream, byte[] bytes, CancellationToken token)
    {
        if (bytes.Length is < 2 or > MaximumBytes) throw new InvalidDataException("Deck API message is outside its size budget.");
        var length = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
        await stream.WriteAsync(length, token).ConfigureAwait(false); await stream.WriteAsync(bytes, token).ConfigureAwait(false);
        await stream.FlushAsync(token).ConfigureAwait(false);
    }
    internal static async Task<JsonElement> CallAsync(string dataRoot, byte[] request, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var pipe = new NamedPipeClientStream(".", PipeName(dataRoot), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(1500, timeout.Token).ConfigureAwait(false);
        await WriteAsync(pipe, request, timeout.Token).ConfigureAwait(false);
        using var result = JsonDocument.Parse(await ReadAsync(pipe, timeout.Token).ConfigureAwait(false));
        return result.RootElement.Clone();
    }
}
