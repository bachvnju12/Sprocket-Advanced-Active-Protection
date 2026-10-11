using System.Text;
using SprocketAdvancedAPS.Systems;

internal static class WaveCheck
{
    internal static byte[] Encode(int rate, float[] samples)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
        foreach (float sample in samples) writer.Write((short)Math.Clamp((int)(sample * 32767), short.MinValue, short.MaxValue));
        return stream.ToArray();
    }
    internal static void Run()
    {
        void Check(bool value, string message) { if (!value) throw new Exception(message); }
        void Reject(byte[] bytes)
        {
            try { APSWave.Decode(bytes); } catch (InvalidDataException) { return; }
            throw new Exception("Bad WAV accepted.");
        }
        foreach (string profile in new[] { "shot", "rocket", "launcher" })
            foreach (int rate in new[] { 44100, 48000 })
            {
                float[] samples = APSWave.Synthesize(profile, rate);
                byte[] wav = Encode(rate, samples);
                var decoded = APSWave.Decode(wav);
                Check(decoded.Rate == rate && decoded.Samples.Length == samples.Length && decoded.Samples.Any(x => Math.Abs(x) > .05f), "PCM duration/nonzero samples.");
                Check(decoded.Samples.All(float.IsFinite) && decoded.Samples.Max(Math.Abs) <= .86f, "Finite samples/headroom.");
                Reject(wav[..^1]);
                byte[] bad = (byte[])wav.Clone(); bad[20] = 3; Reject(bad);
                bad = (byte[])wav.Clone(); bad[22] = 2; Reject(bad);
                bad = (byte[])wav.Clone(); bad[40] = 255; bad[41] = 255; bad[42] = 255; bad[43] = 127; Reject(bad);
                byte[] padded = new byte[wav.Length + 10];
                Array.Copy(wav, 0, padded, 0, 12);
                Encoding.ASCII.GetBytes("JUNK").CopyTo(padded, 12); BitConverter.GetBytes(1).CopyTo(padded, 16); padded[20] = 7;
                Array.Copy(wav, 12, padded, 22, wav.Length - 12); BitConverter.GetBytes(padded.Length - 8).CopyTo(padded, 4);
                Check(APSWave.Decode(padded).Samples.Length == samples.Length, "Odd chunk padding.");
            }
        Reject(new byte[APSWave.MaxBytes + 1]);
        Reject(Encode(44100, new float[44100 * 4]));
        Console.WriteLine("PASS: production PCM16 decoder, nonzero synthetic shots, headroom, chunk padding, lengths/truncation and duration bounds (offline; listening pending).");
    }
}
