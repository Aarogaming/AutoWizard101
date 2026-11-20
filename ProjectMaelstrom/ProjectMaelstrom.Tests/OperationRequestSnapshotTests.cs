using System.Collections;
using ProjectMaelstrom.Utilities;

namespace ProjectMaelstrom.Tests;

public sealed class OperationRequestSnapshotTests
{
    private static readonly string InputPath = Path.GetFullPath("snapshot-input.json");
    private static readonly string Digest = new('a', 64);
    private static OperationRequest Request(IReadOnlyDictionary<string, string>? pins = null) => new(
        "aabbccddeeff00112233445566778899", "test.snapshot", "snapshot-v1", 137,
        true, "pid-123-start-456", pins ?? new Dictionary<string, string> { [InputPath] = Digest }, DelegatedInput: true);

    [Fact]
    public void CallerMutationCannotChangeAdmittedPins()
    {
        var original = new Dictionary<string, string> { [InputPath] = Digest };
        var request = Request(original);
        var snapshot = OperationRequestSnapshot.Freeze(request);
        original[InputPath] = new('b', 64);
        original.Clear();
        original[Path.GetFullPath("different-input.json")] = new('c', 64);

        Assert.NotSame(request, snapshot);
        Assert.NotSame(original, snapshot.Pins);
        Assert.Equal(Digest, Assert.Single(snapshot.Pins).Value);
        Assert.Equal(InputPath, Assert.Single(snapshot.Pins).Key);
        Assert.Equal(Digest, snapshot.Pins[InputPath.ToUpperInvariant()]);
    }

    [Fact]
    public void DictionaryCastCannotEditThePrivateSnapshot()
    {
        var snapshot = OperationRequestSnapshot.Freeze(Request());
        var dictionary = Assert.IsAssignableFrom<IDictionary<string, string>>(snapshot.Pins);
        Assert.True(dictionary.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => dictionary[InputPath] = new('b', 64));
        Assert.Throws<NotSupportedException>(() => dictionary.Remove(InputPath));
        Assert.Throws<NotSupportedException>(() => dictionary.Clear());
        Assert.Throws<NotSupportedException>(() => dictionary.Add(Path.GetFullPath("another-input.json"), Digest));
        Assert.Equal(Digest, snapshot.Pins[InputPath]);
    }

    [Fact]
    public void SnapshotPreservesEveryOtherRequestField()
    {
        var request = Request(); var snapshot = OperationRequestSnapshot.Freeze(request);
        Assert.Equal(request.Id, snapshot.Id);
        Assert.Equal(request.Operation, snapshot.Operation);
        Assert.Equal(request.Version, snapshot.Version);
        Assert.Equal(request.TimeoutSeconds, snapshot.TimeoutSeconds);
        Assert.Equal(request.WritesGame, snapshot.WritesGame);
        Assert.Equal(request.Target, snapshot.Target);
        Assert.Equal(request.DelegatedInput, snapshot.DelegatedInput);
    }

    [Fact]
    public void ValidationUsesTheSingleCopiedViewOfCallerPins()
    {
        var source = new ChangingPins();
        var snapshot = OperationRequestSnapshot.Freeze(Request(source));
        Assert.Equal(1, source.Enumerations);
        Assert.Equal(Digest, snapshot.Pins[InputPath]);
        Assert.Equal("invalid-if-enumerated-again", Assert.Single(source).Value);
        OperationScheduler.Validate(snapshot);
    }

    [Fact]
    public void CaseOnlyPathAliasesCannotBecomeTwoAdmittedInputs()
    {
        var pins = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [InputPath] = Digest,
            [InputPath.ToUpperInvariant()] = new('b', 64)
        };
        Assert.Throws<InvalidDataException>(() => OperationRequestSnapshot.Freeze(Request(pins)));
    }

    [Fact]
    public void NullRequestIsRejected() => Assert.Throws<ArgumentNullException>(() => OperationRequestSnapshot.Freeze(null!));

    [Theory]
    [InlineData("null-pins")]
    [InlineData("invalid-id")]
    [InlineData("empty-operation")]
    [InlineData("empty-version")]
    [InlineData("invalid-budget")]
    [InlineData("missing-live-target")]
    [InlineData("relative-pin")]
    [InlineData("short-digest")]
    [InlineData("null-digest")]
    public void InvalidFrozenRequestsAreRejected(string problem)
    {
        var request = Request();
        request = problem switch
        {
            "null-pins" => request with { Pins = null! },
            "invalid-id" => request with { Id = "invalid" },
            "empty-operation" => request with { Operation = " " },
            "empty-version" => request with { Version = " " },
            "invalid-budget" => request with { TimeoutSeconds = 0 },
            "missing-live-target" => request with { Target = " " },
            "relative-pin" => request with { Pins = new Dictionary<string, string> { ["relative.json"] = Digest } },
            "short-digest" => request with { Pins = new Dictionary<string, string> { [InputPath] = "bad" } },
            "null-digest" => request with { Pins = new Dictionary<string, string> { [InputPath] = null! } },
            _ => throw new InvalidOperationException()
        };
        Assert.Throws<InvalidDataException>(() => OperationRequestSnapshot.Freeze(request));
    }

    [Fact]
    public void PinAdmissionRetainsTheExistingThirtyTwoInputLimit()
    {
        var pins = Enumerable.Range(0, 32).ToDictionary(index => Path.GetFullPath($"input-{index}.json"), _ => Digest);
        Assert.Equal(32, OperationRequestSnapshot.Freeze(Request(pins)).Pins.Count);
        pins[Path.GetFullPath("one-too-many.json")] = Digest;
        Assert.Throws<InvalidDataException>(() => OperationRequestSnapshot.Freeze(Request(pins)));
    }

    private sealed class ChangingPins : IReadOnlyDictionary<string, string>
    {
        public int Enumerations { get; private set; }
        public int Count => 1;
        public IEnumerable<string> Keys => [InputPath];
        public IEnumerable<string> Values => [Digest];
        public string this[string key] => key == InputPath ? Digest : throw new KeyNotFoundException();
        public bool ContainsKey(string key) => key == InputPath;
        public bool TryGetValue(string key, out string value)
        {
            value = key == InputPath ? Digest : "";
            return key == InputPath;
        }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            Enumerations++;
            yield return new(InputPath, Enumerations == 1 ? Digest : "invalid-if-enumerated-again");
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
