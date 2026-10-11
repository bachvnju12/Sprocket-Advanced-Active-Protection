using System.Buffers.Binary;

namespace SprocketAdvancedAPS.Systems;

internal static class APSWave
{
    internal const int MaxBytes = 1024 * 1024;
    internal static (int Rate, float[] Samples) Decode(byte[] bytes)
    {
        var data = bytes.AsSpan();
        if (data.Length is < 44 or > MaxBytes || BinaryPrimitives.ReadUInt32LittleEndian(data) != 0x46464952 || BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(8, 4)) != 0x45564157)
            throw new InvalidDataException("Expected bounded RIFF/WAVE.");
        long end = 8L + BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        if (end != data.Length) throw new InvalidDataException("Invalid RIFF length.");
        int rate = 0, offset = 12, sampleOffset = -1, sampleBytes = 0;
        while (offset < data.Length)
        {
            if (data.Length - offset < 8) throw new InvalidDataException("Truncated WAV chunk.");
            var id = data.Slice(offset, 4);
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(offset + 4, 4));
            long next = (long)offset + 8 + size + (size & 1);
            if (next > data.Length) throw new InvalidDataException("Truncated WAV payload/padding.");
            offset += 8;
            if (BinaryPrimitives.ReadUInt32LittleEndian(id) == 0x20746d66)
            {
                if (rate != 0 || size < 16) throw new InvalidDataException("Invalid WAV format chunk.");
                var fmt = data.Slice(offset, 16);
                rate = BinaryPrimitives.ReadInt32LittleEndian(fmt.Slice(4, 4));
                if (BinaryPrimitives.ReadUInt16LittleEndian(fmt) != 1 || BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(2, 2)) != 1 ||
                    rate is not (44100 or 48000) || BinaryPrimitives.ReadUInt32LittleEndian(fmt.Slice(8, 4)) != rate * 2 ||
                    BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(12, 2)) != 2 || BinaryPrimitives.ReadUInt16LittleEndian(fmt.Slice(14, 2)) != 16)
                    throw new InvalidDataException("Only PCM16 mono 44.1/48 kHz is supported.");
            }
            else if (BinaryPrimitives.ReadUInt32LittleEndian(id) == 0x61746164)
            {
                if (sampleOffset >= 0 || size == 0 || size % 2 != 0) throw new InvalidDataException("Invalid WAV data chunk.");
                sampleOffset = offset; sampleBytes = (int)size;
            }
            offset = (int)next;
        }
        if (rate == 0 || sampleOffset < 0 || sampleBytes > rate * 2 * 3) throw new InvalidDataException("Missing WAV chunks or duration exceeds 3 seconds.");
        var samples = new float[sampleBytes / 2];
        for (int i = 0; i < samples.Length; i++) samples[i] = BinaryPrimitives.ReadInt16LittleEndian(data.Slice(sampleOffset + i * 2, 2)) / 32768f;
        return (rate, samples);
    }

    // ponytail: original synthetic impulse/noise, not a recording of real APS hardware.
    internal static float[] Synthesize(string profile, int rate = 44100)
    {
        float duration = profile == "rocket" ? .65f : profile == "launcher" ? .42f : .3f;
        var samples = new float[(int)(rate * duration)];
        var random = new Random(profile == "rocket" ? 17 : profile == "launcher" ? 31 : 47);
        float low = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float noise = (float)(random.NextDouble() * 2 - 1);
            low += .08f * (noise - low);
            float attack = Math.Min(1, t / .0005f);
            float snap = noise * MathF.Exp(-t * 120);
            float body = (low * 2 + MathF.Sin(2 * MathF.PI * 145 * t) * .3f) * MathF.Exp(-t * 28);
            float tail = (profile == "rocket" ? low * 1.2f : noise * .14f) * MathF.Exp(-t * (profile == "rocket" ? 9 : 22));
            samples[i] = Math.Clamp(attack * (snap * .55f + body * .4f + tail * .3f), -.85f, .85f);
        }
        return samples;
    }
}
