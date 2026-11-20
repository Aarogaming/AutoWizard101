using System.Numerics;
using System.Runtime.CompilerServices;

namespace ProjectMaelstrom.Utilities.Circuit;

/// <summary>
/// High-speed combinational logic gates and perceptual comparators.
/// Operates on 64-bit status words and image spans with zero heap allocations.
/// </summary>
public static class PerceptualGate
{
    /// <summary>
    /// Acts as a hardware 64-bit XNOR + PopCount comparator.
    /// Returns true (1) if the observed 64-bit dHash is within maxHammingBits of targetHash.
    /// Executes in approximately 2 CPU clock cycles.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareHash(ulong observedHash, ulong targetHash, int maxHammingBits = 6)
    {
        return BitOperations.PopCount(observedHash ^ targetHash) <= maxHammingBits;
    }

    /// <summary>
    /// Computes the bitwise Hamming distance between two 64-bit words.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HammingDistance(ulong a, ulong b)
    {
        return BitOperations.PopCount(a ^ b);
    }

    /// <summary>
    /// Evaluates a combinational AND-gate rung: returns true (1) only if all requiredHigh bits
    /// are active (1) and all requiredLow bits are inactive (0).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EvaluateRung(WorldBits currentBus, WorldBits requiredHigh, WorldBits requiredLow = WorldBits.None)
    {
        return (currentBus & requiredHigh) == requiredHigh &&
               (currentBus & requiredLow) == WorldBits.None;
    }

    /// <summary>
    /// Computes a standard 64-bit difference hash (dHash) from a 9x8 (72-byte) grayscale grid.
    /// For each of the 8 rows, compares adjacent columns (col > col + 1), generating 8 bits per row.
    /// 8 rows * 8 bits = 64 bits packed into a single ulong.
    /// </summary>
    public static ulong ComputeDHash64(ReadOnlySpan<byte> grayscale72)
    {
        if (grayscale72.Length < 72)
            throw new ArgumentException("Grayscale buffer must contain at least 72 bytes (9x8).", nameof(grayscale72));

        ulong hash = 0;
        int bitPos = 0;

        for (int y = 0; y < 8; y++)
        {
            int rowOffset = y * 9;
            for (int x = 0; x < 8; x++)
            {
                byte left = grayscale72[rowOffset + x];
                byte right = grayscale72[rowOffset + x + 1];

                if (left > right)
                {
                    hash |= 1UL << bitPos;
                }
                bitPos++;
            }
        }

        return hash;
    }

    /// <summary>
    /// Downsamples an arbitrary 24-bit/32-bit pixel buffer ROI into a 9x8 grayscale grid on the stack,
    /// then computes its 64-bit dHash without any heap allocations.
    /// </summary>
    public static ulong ComputeDHashFromPixelBuffer(
        ReadOnlySpan<byte> pixelData,
        int roiX, int roiY, int roiW, int roiH,
        int sourceStride,
        int bytesPerPixel = 4)
    {
        if (roiW < 9 || roiH < 8)
            throw new ArgumentException("ROI dimensions must be at least 9x8.");

        Span<byte> grid72 = stackalloc byte[72];

        // Sample 9 columns and 8 rows evenly across the ROI
        for (int gy = 0; gy < 8; gy++)
        {
            int srcY = roiY + (gy * roiH) / 8;
            int rowStart = srcY * sourceStride;

            for (int gx = 0; gx < 9; gx++)
            {
                int srcX = roiX + (gx * roiW) / 9;
                int pixelOffset = rowStart + (srcX * bytesPerPixel);

                byte b = pixelData[pixelOffset];
                byte g = pixelData[pixelOffset + 1];
                byte r = pixelData[pixelOffset + 2];

                // Fast integer grayscale: (77*R + 150*G + 29*B) >> 8
                byte luma = (byte)((77 * r + 150 * g + 29 * b) >> 8);
                grid72[gy * 9 + gx] = luma;
            }
        }

        return ComputeDHash64(grid72);
    }

    /// <summary>
    /// Evaluates the pixel density of a target color box in a given ROI.
    /// Returns the fraction of pixels matching the color range [0.0, 1.0].
    /// </summary>
    public static float ComputeColorDensity(
        ReadOnlySpan<byte> pixelData,
        int roiX, int roiY, int roiW, int roiH,
        int sourceStride,
        byte minB, byte maxB,
        byte minG, byte maxG,
        byte minR, byte maxR,
        int bytesPerPixel = 4)
    {
        int total = roiW * roiH;
        if (total == 0) return 0f;

        int matched = 0;

        for (int y = 0; y < roiH; y++)
        {
            int rowStart = (roiY + y) * sourceStride + (roiX * bytesPerPixel);
            for (int x = 0; x < roiW; x++)
            {
                int offset = rowStart + (x * bytesPerPixel);
                byte b = pixelData[offset];
                byte g = pixelData[offset + 1];
                byte r = pixelData[offset + 2];

                if (b >= minB && b <= maxB &&
                    g >= minG && g <= maxG &&
                    r >= minR && r <= maxR)
                {
                    matched++;
                }
            }
        }

        return (float)matched / total;
    }
}
