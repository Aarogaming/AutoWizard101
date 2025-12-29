using System.Reflection;
using System.Text.Json;

// Dev-only Functional Test Runner for Project Maelstrom.
// Verifies policy enforcement, executor selection, plugin gating, and failure safety via reflection.

var runner = new TestRunner();
runner.Run();

internal sealed class TestRunner
{
    private readonly List<TestCase> _cases = new();
    private int _pass;
    private int _fail;

    public void Run()
    {
        try
        {
            Setup();
            foreach (var tc in _cases)
            {
                Console.WriteLine($"--- {tc.Name} ---");
                try
                {
                    tc.Action();
                    Console.WriteLine($"[PASS] {tc.Expectation}");
                    _pass++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FAIL] {tc.Expectation}");
                    Console.WriteLine($"Reason: {ex.Message}");
                    _fail++;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Runner error: {ex}");
            _fail++;
        }
        finally
        {
            Console.WriteLine("=== Summary ===");
            Console.WriteLine($"Total: {_cases.Count}, Passed: {_pass}, Failed: {_fail}");
        }
    }

    private void Setup()
    {
        var ctx = new ReflectionContext();

        // Clean plugin root for deterministic results
        ctx.CleanPlugins();

        _cases.Add(new TestCase(
            "Policy: Public blocks live",
            "Public profile blocks live execution",
            () =>
            {
                ctx.WritePolicy(allowLive: false, profile: "AcademicSimulation");
                ctx.ReloadPolicy();
                var snap = ctx.GetSnapshot();
                if (snap.allowLive) throw new InvalidOperationException("AllowLiveAutomation should be false");
                if (snap.profile != "AcademicSimulation") throw new InvalidOperationException("Profile should be AcademicSimulation");
                var exec = ctx.CreateExecutor();
                var result = ctx.Execute(exec, "test");
                if (result.status != "Simulated") throw new InvalidOperationException($"Expected Simulated, got {result.status}");
            }));

        _cases.Add(new TestCase(
            "Policy: Experimental blocks live",
            "Experimental profile still blocks when AllowLive=false",
            () =>
            {
                ctx.WritePolicy(allowLive: false, profile: "ExperimentalSimulation");
                ctx.ReloadPolicy();
                var snap = ctx.GetSnapshot();
                if (snap.allowLive) throw new InvalidOperationException("AllowLiveAutomation should be false");
                if (snap.profile != "ExperimentalSimulation") throw new InvalidOperationException("Profile should be ExperimentalSimulation");
                var exec = ctx.CreateExecutor();
                var result = ctx.Execute(exec, "test");
                if (result.status != "Simulated") throw new InvalidOperationException($"Expected Simulated, got {result.status}");
            }));

        _cases.Add(new TestCase(
            "Policy: Live allowed but no backend",
            "Live enabled returns LiveEnabledNoBackend or LiveDispatched (no crash)",
            () =>
            {
                ctx.WritePolicy(allowLive: true, profile: "ExperimentalSimulation");
                ctx.ReloadPolicy();
                var snap = ctx.GetSnapshot();
                if (!snap.allowLive) throw new InvalidOperationException("AllowLiveAutomation should be true");
                var exec = ctx.CreateExecutor();
                var result = ctx.Execute(exec, "test");
                if (result.status != "LiveEnabledNoBackend" && result.status != "LiveDispatched")
                {
                    throw new InvalidOperationException($"Expected LiveEnabledNoBackend/LiveDispatched, got {result.status}");
                }
            }));

        _cases.Add(new TestCase(
            "Plugin: SampleOverlay allowed",
            "SampleOverlay allowed in Public profile",
            () =>
            {
                ctx.WritePolicy(allowLive: false, profile: "AcademicSimulation");
                ctx.ReloadPolicy();
                ctx.InstallSampleOverlay();
                ctx.ReloadPlugins();
                var info = ctx.GetPlugin("SampleOverlay");
                if (info == null) throw new InvalidOperationException("SampleOverlay not found");
                if (info.Value.status != "Allowed") throw new InvalidOperationException($"Expected Allowed, got {info.Value.status} ({info.Value.reason})");
            }));

        _cases.Add(new TestCase(
            "Plugin: SampleLiveIntegration blocked",
            "SampleLiveIntegration blocked when live disabled",
            () =>
            {
                ctx.WritePolicy(allowLive: false, profile: "ExperimentalSimulation");
                ctx.ReloadPolicy();
                ctx.InstallSampleLiveIntegration();
                ctx.ReloadPlugins();
                var info = ctx.GetPlugin("SampleLiveIntegration");
                if (info == null) throw new InvalidOperationException("SampleLiveIntegration not found");
                if (info.Value.status != "Blocked") throw new InvalidOperationException($"Expected Blocked, got {info.Value.status} ({info.Value.reason})");
            }));

        _cases.Add(new TestCase(
            "Plugin: Invalid manifest",
            "Corrupt manifest reports Failed or error reason",
            () =>
            {
                ctx.WritePolicy(allowLive: false, profile: "AcademicSimulation");
                ctx.ReloadPolicy();
                ctx.InstallCorruptPlugin();
                ctx.ReloadPlugins();
                var info = ctx.GetPlugin("CorruptPlugin");
                if (info == null) throw new InvalidOperationException("CorruptPlugin not found");
                if (info.Value.status != "Failed" && !info.Value.reason.Contains("error", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Expected Failed/error, got {info.Value.status} ({info.Value.reason})");
                }
            }));

        _cases.Add(new TestCase(
            "Policy: Corrupt policy fallback",
            "Corrupt policy file falls back to safe defaults",
            () =>
            {
                ctx.WriteCorruptPolicy();
                ctx.ReloadPolicy();
                var snap = ctx.GetSnapshot();
                if (snap.allowLive) throw new InvalidOperationException("Corrupt policy should default to allowLive=false");
                if (snap.profile != "AcademicSimulation") throw new InvalidOperationException("Corrupt policy should default to AcademicSimulation");
            }));
    }
}

internal sealed class TestCase
{
    public string Name { get; }
    public string Expectation { get; }
    public Action Action { get; }
    public TestCase(string name, string expectation, Action action)
    {
        Name = name;
        Expectation = expectation;
        Action = action;
    }
}

internal sealed class ReflectionContext
{
    private readonly Assembly _asm;
    private readonly Type _policyManager;
    private readonly Type _executorFactory;
    private readonly Type _inputCommand;
    private readonly Type _pluginLoader;
    private readonly string _policyPath;

    public ReflectionContext()
    {
        var asmPath = Path.Combine(AppContext.BaseDirectory, "ProjectMaelstrom.dll");
        if (!File.Exists(asmPath))
        {
            throw new FileNotFoundException("ProjectMaelstrom.dll not found in output directory", asmPath);
        }
        _asm = Assembly.LoadFrom(asmPath);
        _policyManager = _asm.GetType("ProjectMaelstrom.Utilities.ExecutionPolicyManager")!;
        _executorFactory = _asm.GetType("ProjectMaelstrom.Utilities.ExecutorFactory")!;
        _inputCommand = _asm.GetType("ProjectMaelstrom.Models.InputCommand")!;
        _pluginLoader = _asm.GetType("ProjectMaelstrom.Utilities.PluginLoader")!;
        _policyPath = (string)(_policyManager.GetProperty("PolicyPath", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!);
    }

    public void WritePolicy(bool allowLive, string profile)
    {
        var lines = new[]
        {
            $"ALLOW_LIVE_AUTOMATION={allowLive.ToString().ToLowerInvariant()}",
            $"EXECUTION_PROFILE={profile}"
        };
        File.WriteAllLines(_policyPath, lines);
    }

    public void WriteCorruptPolicy()
    {
        File.WriteAllText(_policyPath, "THIS_IS_NOT_VALID");
    }

    public void ReloadPolicy()
    {
        _policyManager.GetMethod("Reload", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
    }

    public (bool allowLive, string mode, string profile) GetSnapshot()
    {
        var snap = _policyManager.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var allow = (bool)snap.GetType().GetProperty("AllowLiveAutomation")!.GetValue(snap)!;
        var mode = snap.GetType().GetProperty("Mode")!.GetValue(snap)!.ToString()!;
        var profile = snap.GetType().GetProperty("Profile")!.GetValue(snap)!.ToString()!;
        return (allow, mode, profile);
    }

    public object CreateExecutor()
    {
        var snapObj = _policyManager.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        return _executorFactory.GetMethod("FromPolicy", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new[] { snapObj })!;
    }

    public (string status, string message) Execute(object executor, string source)
    {
        var cmd = Activator.CreateInstance(_inputCommand)!;
        _inputCommand.GetProperty("Type")!.SetValue(cmd, "click");
        var list = Array.CreateInstance(_inputCommand, 1);
        list.SetValue(cmd, 0);

        var ctxType = _asm.GetType("ProjectMaelstrom.Utilities.ExecutionContext")!;
        var ctx = Activator.CreateInstance(ctxType)!;
        ctxType.GetProperty("Source")!.SetValue(ctx, source);

        var result = executor.GetType().GetMethod("Execute")!.Invoke(executor, new object?[] { list, ctx })!;
        var status = result.GetType().GetProperty("Status")!.GetValue(result)!.ToString()!;
        var message = result.GetType().GetProperty("Message")!.GetValue(result)?.ToString() ?? "";
        return (status, message);
    }

    public void CleanPlugins()
    {
        var pluginRoot = (string)_pluginLoader.GetProperty("PluginRoot", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        if (Directory.Exists(pluginRoot))
        {
            Directory.Delete(pluginRoot, true);
        }
    }

    public void ReloadPlugins()
    {
        _pluginLoader.GetMethod("Reload", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
        // Force ensure load by reading Current
        _ = _pluginLoader.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
    }

    public void InstallSampleOverlay()
    {
        var pluginRoot = (string)_pluginLoader.GetProperty("PluginRoot", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var samplesRoot = Path.Combine(pluginRoot, "_samples");
        Directory.CreateDirectory(samplesRoot);
        var manifest = new
        {
            pluginId = "SampleOverlay",
            name = "SampleOverlay",
            version = "1.0.0",
            targetAppVersion = "any",
            requiredProfile = "Public",
            declaredCapabilities = new[] { "OverlayWidgets" }
        };
        File.WriteAllText(Path.Combine(samplesRoot, "SampleOverlay.manifest.json"), JsonSerializer.Serialize(manifest));
    }

    public void InstallSampleLiveIntegration()
    {
        var pluginRoot = (string)_pluginLoader.GetProperty("PluginRoot", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var samplesRoot = Path.Combine(pluginRoot, "_samples");
        Directory.CreateDirectory(samplesRoot);
        var manifest = new
        {
            pluginId = "SampleLiveIntegration",
            name = "SampleLiveIntegration",
            version = "1.0.0",
            targetAppVersion = "any",
            requiredProfile = "Experimental",
            declaredCapabilities = new[] { "LiveIntegration" }
        };
        File.WriteAllText(Path.Combine(samplesRoot, "SampleLiveIntegration.manifest.json"), JsonSerializer.Serialize(manifest));
    }

    public void InstallCorruptPlugin()
    {
        var pluginRoot = (string)_pluginLoader.GetProperty("PluginRoot", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        var samplesRoot = Path.Combine(pluginRoot, "_samples");
        Directory.CreateDirectory(samplesRoot);
        File.WriteAllText(Path.Combine(samplesRoot, "CorruptPlugin.manifest.json"), "{ this is not valid json }");
    }

    public (string status, string reason)? GetPlugin(string id)
    {
        var list = (IEnumerable<object>)_pluginLoader.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        foreach (var p in list)
        {
            var pid = p.GetType().GetProperty("PluginId")!.GetValue(p)?.ToString();
            if (string.Equals(pid, id, StringComparison.OrdinalIgnoreCase))
            {
                var status = p.GetType().GetProperty("Status")!.GetValue(p)!.ToString()!;
                var reason = p.GetType().GetProperty("Reason")!.GetValue(p)?.ToString() ?? "";
                return (status, reason);
            }
        }
        return null;
    }
}
