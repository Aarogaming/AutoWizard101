using Xunit;
using ProjectMaelstrom.Utilities.Circuit;

namespace ProjectMaelstrom.Tests;

public sealed class PerceptualGateTests
{
    [Fact]
    public void CompareHash_ExactMatch_ReturnsTrueWithZeroDistance()
    {
        ulong hashA = 0xABCD_EF01_2345_6789UL;
        ulong hashB = 0xABCD_EF01_2345_6789UL;

        Assert.Equal(0, PerceptualGate.HammingDistance(hashA, hashB));
        Assert.True(PerceptualGate.CompareHash(hashA, hashB, maxHammingBits: 6));
    }

    [Fact]
    public void CompareHash_WithinTolerance_ReturnsTrue()
    {
        ulong hashA = 0b0000_1111UL;
        ulong hashB = 0b0000_1000UL; // 3 bits flipped

        Assert.Equal(3, PerceptualGate.HammingDistance(hashA, hashB));
        Assert.True(PerceptualGate.CompareHash(hashA, hashB, maxHammingBits: 6));
    }

    [Fact]
    public void CompareHash_AboveTolerance_ReturnsFalse()
    {
        ulong hashA = 0x0000_0000_0000_0000UL;
        ulong hashB = 0x0000_0000_0000_03FFUL; // 10 bits set

        Assert.Equal(10, PerceptualGate.HammingDistance(hashA, hashB));
        Assert.False(PerceptualGate.CompareHash(hashA, hashB, maxHammingBits: 6));
    }

    [Fact]
    public void EvaluateRung_CombinationalTruthTable()
    {
        var bus = WorldBits.LeaseActive | WorldBits.BazaarOpen | WorldBits.GoldAboveFloor;

        // Requires LeaseActive, BazaarOpen, GoldAboveFloor
        var required = WorldBits.LeaseActive | WorldBits.BazaarOpen | WorldBits.GoldAboveFloor;
        Assert.True(PerceptualGate.EvaluateRung(bus, required));

        // Missing LeaseActive
        var busWithoutLease = WorldBits.BazaarOpen | WorldBits.GoldAboveFloor;
        Assert.False(PerceptualGate.EvaluateRung(busWithoutLease, required));

        // Inhibited bit (ConfirmModalOpen)
        var busWithModal = bus | WorldBits.ConfirmModalOpen;
        Assert.False(PerceptualGate.EvaluateRung(busWithModal, required, requiredLow: WorldBits.ConfirmModalOpen));
    }

    [Fact]
    public void ComputeDHash64_DecreasingGradient_ProducesAllOnes()
    {
        byte[] grid = new byte[72];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                // Decreasing across columns: col 0 = 200, col 8 = 120
                grid[y * 9 + x] = (byte)(200 - (x * 10));
            }
        }

        ulong hash = PerceptualGate.ComputeDHash64(grid);

        // Every column comparison col > col + 1 is true (1)
        Assert.Equal(ulong.MaxValue, hash);
    }

    [Fact]
    public void ComputeDHash64_IncreasingGradient_ProducesAllZeros()
    {
        byte[] grid = new byte[72];
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                // Increasing across columns: col 0 = 10, col 8 = 90
                grid[y * 9 + x] = (byte)(10 + (x * 10));
            }
        }

        ulong hash = PerceptualGate.ComputeDHash64(grid);

        // Every column comparison col > col + 1 is false (0)
        Assert.Equal(0UL, hash);
    }

    [Fact]
    public void ComputeDHashFromPixelBuffer_DeterministicAndNoiseTolerant()
    {
        int width = 90;
        int height = 80;
        int stride = width * 4;
        byte[] bufferA = new byte[stride * height];
        byte[] bufferB = new byte[stride * height];

        // Fill with a horizontal gradient
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte val = (byte)(x * 2);
                int offset = (y * stride) + (x * 4);
                bufferA[offset] = val;     // B
                bufferA[offset + 1] = val; // G
                bufferA[offset + 2] = val; // R
                bufferA[offset + 3] = 255; // A

                bufferB[offset] = val;
                bufferB[offset + 1] = val;
                bufferB[offset + 2] = val;
                bufferB[offset + 3] = 255;
            }
        }

        // Introduce slight pixel noise at one position in buffer B
        bufferB[100] = (byte)(bufferB[100] ^ 0x05);

        ulong hashA = PerceptualGate.ComputeDHashFromPixelBuffer(bufferA, 0, 0, width, height, stride);
        ulong hashB = PerceptualGate.ComputeDHashFromPixelBuffer(bufferB, 0, 0, width, height, stride);

        int dist = PerceptualGate.HammingDistance(hashA, hashB);
        Assert.True(dist <= 2);
    }

    [Fact]
    public void ComputeColorDensity_CalculatesExactFraction()
    {
        int width = 10;
        int height = 10;
        int stride = width * 4;
        byte[] buffer = new byte[stride * height];

        // 40 red pixels, 60 black pixels
        for (int i = 0; i < 40; i++)
        {
            int offset = i * 4;
            buffer[offset] = 0;       // B
            buffer[offset + 1] = 0;   // G
            buffer[offset + 2] = 255; // R
            buffer[offset + 3] = 255;
        }

        float density = PerceptualGate.ComputeColorDensity(
            buffer, 0, 0, width, height, stride,
            minB: 0, maxB: 10,
            minG: 0, maxG: 10,
            minR: 200, maxR: 255);

        Assert.Equal(0.40f, density, precision: 2);
    }
}
