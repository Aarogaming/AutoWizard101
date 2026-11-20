using System.Drawing.Imaging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ProjectMaelstrom.Utilities;

internal sealed record CapturedWindowFrame(Bitmap Image, TimeSpan? CaptureTime) : IDisposable
{
    public void Dispose() => Image.Dispose();
}

internal sealed class CapturedWindowPixels(byte[] bgra, int width, int height, TimeSpan? captureTime, Action<byte[]> release) : IDisposable
{
    private Action<byte[]>? _release = release;
    public byte[] Bgra { get; } = bgra;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public TimeSpan? CaptureTime { get; } = captureTime;
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke(Bgra);
}

/// <summary>Captures only an explicitly selected HWND through Windows Graphics Capture.</summary>
internal sealed class WindowObserver : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly SemaphoreSlim _frameAvailable = new(0, 1);
    private readonly object _surfaceSync = new();
    private Action<Direct3D11CaptureFrame>? _surfaceReceiver;
    private Action<Exception>? _surfaceError;
    private readonly ConcurrentBag<byte[]> _pixelPool = new();
    private int _pooledPixels;
    private IDirect3DDevice? _device;
    private Windows.Storage.Streams.Buffer? _copyBuffer;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private Windows.Graphics.SizeInt32 _size;
    private volatile bool _disposed;
    private long _nextHealthCheck;
    public GameWindow Target { get; }
    public int Width => _size.Width;
    public int Height => _size.Height;

    public WindowObserver(GameWindow target, int framePoolCapacity = 2)
    {
        Target = target;
        if (framePoolCapacity is < 2 or > 16) throw new ArgumentOutOfRangeException(nameof(framePoolCapacity));
        if (!GameWindows.IsCurrent(target)) throw new InvalidOperationException("That window has closed. Refresh and select the game again.");
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows window capture is unavailable on this computer.");
        try
        {
            var item = CreateItem(target.Handle);
            _size = item.Size;
            CheckSize(_size.Width, _size.Height);
            _device = CreateDevice();
            _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_device, DirectXPixelFormat.B8G8R8A8UIntNormalized, framePoolCapacity, _size);
            _pool.FrameArrived += OnFrameArrived;
            _session = _pool.CreateCaptureSession(item);
            _session.IsCursorCaptureEnabled = false;
            _session.StartCapture();
        }
        catch { _session?.Dispose(); _pool?.Dispose(); _device?.Dispose(); throw; }
    }

    private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        lock (_surfaceSync)
        {
            if (_disposed) return;
            if (_surfaceReceiver == null)
            {
                try { _frameAvailable.Release(); } catch (SemaphoreFullException) { }
                return;
            }
            Direct3D11CaptureFrame? frame = null;
            try
            {
                frame = sender.TryGetNextFrame();
                if (frame == null) return;
                if (frame.ContentSize.Width != _size.Width || frame.ContentSize.Height != _size.Height)
                    throw new InvalidOperationException("Game size changed. Reconnect to refresh the capture area.");
                _surfaceReceiver(frame);
                frame = null; // The receiver owns it.
            }
            catch (Exception ex) { frame?.Dispose(); _surfaceError?.Invoke(ex); }
        }
    }

    /// <summary>Used only by the GPU experiment; receiver takes ownership of each frame.</summary>
    public void SetSurfaceReceiver(Action<Direct3D11CaptureFrame>? receiver, Action<Exception>? error)
    {
        lock (_surfaceSync)
        {
            _surfaceReceiver = receiver;
            _surfaceError = error;
            if (receiver == null || _disposed || _pool == null) return;
            Direct3D11CaptureFrame? latest = null;
            try
            {
                // A still window may have delivered its only frame before the receiver was attached.
                for (var i = 0; i < 16; i++)
                {
                    var next = _pool.TryGetNextFrame();
                    if (next == null) break;
                    latest?.Dispose();
                    latest = next;
                }
                if (latest == null) return;
                if (latest.ContentSize.Width != _size.Width || latest.ContentSize.Height != _size.Height)
                    throw new InvalidOperationException("Game size changed. Reconnect to refresh the capture area.");
                receiver(latest);
                latest = null;
            }
            catch (Exception ex) { latest?.Dispose(); error?.Invoke(ex); }
        }
    }

    /// <summary>Waits for a frame-ready notification; callers must still check for an actual frame.</summary>
    public Task WaitForFrameAsync(CancellationToken token) => _frameAvailable.WaitAsync(TimeSpan.FromMilliseconds(250), token);

    public static void CheckSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > 16_777_216)
            throw new InvalidOperationException("Use a non-empty game window no larger than 16 megapixels, then reconnect.");
    }

    public static Size GetCaptureSize(GameWindow target)
    {
        if (!GameWindows.IsCurrent(target)) throw new InvalidOperationException("That window has closed. Refresh and select the game again.");
        var size = CreateItem(target.Handle).Size;
        CheckSize(size.Width, size.Height);
        return new Size(size.Width, size.Height);
    }

    public async Task<Bitmap?> ReadAsync()
    {
        var frame = await ReadTimestampedAsync();
        return frame?.Image;
    }

    public async Task<CapturedWindowFrame?> ReadTimestampedAsync(bool latestOnly = false)
    {
        using var frame = await ReadPixelsAsync(latestOnly);
        if (frame == null) return null;
        var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
        try
        {
            var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try { Marshal.Copy(frame.Bgra, 0, data.Scan0, frame.Bgra.Length); }
            finally { bitmap.UnlockBits(data); }
            return new CapturedWindowFrame(bitmap, frame.CaptureTime);
        }
        catch { bitmap.Dispose(); throw; }
    }

    public async Task<CapturedWindowPixels?> ReadPixelsAsync(bool latestOnly = false)
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return null;
            if (!GameWindows.Visible(Target)) throw new InvalidOperationException("Game disconnected. Refresh and select its window again.");
            if (GameWindows.Minimized(Target)) throw new InvalidOperationException("Game is minimized. Restore it, then reconnect.");
            var now = Stopwatch.GetTimestamp();
            if (now >= _nextHealthCheck)
            {
                if (!GameWindows.IsCurrent(Target)) throw new InvalidOperationException("Game disconnected. Refresh and select its window again.");
                _nextHealthCheck = now + Stopwatch.Frequency / 4;
            }
            using var frame = TakeFrame(latestOnly);
            if (frame == null) return null;
            if (frame.ContentSize.Width != _size.Width || frame.ContentSize.Height != _size.Height)
                throw new InvalidOperationException("Game size changed. Reconnect to refresh the capture area.");
            using var pixels = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface);
            var length = checked((uint)(_size.Width * _size.Height * 4));
            var buffer = _copyBuffer ??= new Windows.Storage.Streams.Buffer(length);
            pixels.CopyToBuffer(buffer);
            if (!_pixelPool.TryTake(out var bytes)) bytes = new byte[length];
            else Interlocked.Decrement(ref _pooledPixels);
            try
            {
                using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(bytes);
                return new CapturedWindowPixels(bytes, _size.Width, _size.Height, frame.SystemRelativeTime, ReturnPixels);
            }
            catch { ReturnPixels(bytes); throw; }
        }
        finally { _gate.Release(); }
    }

    private Direct3D11CaptureFrame? TakeFrame(bool latestOnly)
    {
        var latest = _pool!.TryGetNextFrame();
        if (!latestOnly) return latest;
        try
        {
            // Low-rate still sampling must discard queued surfaces before copying to CPU.
            // Video callers retain the default ordered-frame behavior.
            for (var i = 0; i < 16; i++)
            {
                var next = _pool.TryGetNextFrame();
                if (next == null) break;
                latest?.Dispose(); latest = next;
            }
            return latest;
        }
        catch { latest?.Dispose(); throw; }
    }

    /// <summary>The caller owns the returned capture frame and must dispose it after the GPU is finished using its surface.</summary>
    public async Task<Direct3D11CaptureFrame?> ReadSurfaceAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return null;
            if (!GameWindows.Visible(Target)) throw new InvalidOperationException("Game disconnected. Refresh and select its window again.");
            if (GameWindows.Minimized(Target)) throw new InvalidOperationException("Game is minimized. Restore it, then reconnect.");
            var now = Stopwatch.GetTimestamp();
            if (now >= _nextHealthCheck)
            {
                if (!GameWindows.IsCurrent(Target)) throw new InvalidOperationException("Game disconnected. Refresh and select its window again.");
                _nextHealthCheck = now + Stopwatch.Frequency / 4;
            }
            var frame = _pool!.TryGetNextFrame();
            if (frame != null && (frame.ContentSize.Width != _size.Width || frame.ContentSize.Height != _size.Height))
            {
                frame.Dispose();
                throw new InvalidOperationException("Game size changed. Reconnect to refresh the capture area.");
            }
            return frame;
        }
        finally { _gate.Release(); }
    }

    private void ReturnPixels(byte[] pixels)
    {
        if (_disposed) return;
        if (Interlocked.Increment(ref _pooledPixels) <= 8) _pixelPool.Add(pixels);
        else Interlocked.Decrement(ref _pooledPixels);
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            lock (_surfaceSync) { _surfaceReceiver = null; _surfaceError = null; }
            while (_pixelPool.TryTake(out _)) Interlocked.Decrement(ref _pooledPixels);
            _copyBuffer = null;
            _session?.Dispose();
            if (_pool != null) _pool.FrameArrived -= OnFrameArrived;
            _pool?.Dispose();
            _device?.Dispose();
        }
        finally { _gate.Release(); }
    }

    private static GraphicsCaptureItem CreateItem(nint window)
    {
        const string name = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(name, name.Length, out var text));
        nint factory = 0, item = 0;
        try
        {
            var interopId = new Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(text, ref interopId, out factory));
            var interop = (ICaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
            try
            {
                var itemId = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
                Marshal.ThrowExceptionForHR(interop.CreateForWindow(window, ref itemId, out item));
                return WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(item);
            }
            finally { Marshal.ReleaseComObject(interop); }
        }
        finally
        {
            if (item != 0) Marshal.Release(item);
            if (factory != 0) Marshal.Release(factory);
            WindowsDeleteString(text);
        }
    }

    private static IDirect3DDevice CreateDevice()
    {
        nint device = 0, context = 0, dxgi = 0, inspectable = 0;
        try
        {
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7, out device, out _, out context));
            var iid = new Guid("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, in iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            foreach (var pointer in new[] { inspectable, dxgi, context, device }) if (pointer != 0) Marshal.Release(pointer);
        }
    }

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICaptureItemInterop
    {
        [PreserveSig] int CreateForWindow(nint window, ref Guid iid, out nint item);
        [PreserveSig] int CreateForMonitor(nint monitor, ref Guid iid, out nint item);
    }
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string value, int length, out nint text);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint text);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint text, ref Guid iid, out nint factory);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint device, out nint result);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software, uint flags, nint levels, uint levelCount, uint sdkVersion, out nint device, out int featureLevel, out nint context);
}
