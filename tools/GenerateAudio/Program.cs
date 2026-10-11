using System.Text;
using SprocketAdvancedAPS.Systems;

if (args.Length != 1) throw new ArgumentException("Pass audio output directory.");
Directory.CreateDirectory(args[0]);
foreach (string profile in new[] { "shot", "rocket", "launcher" })
{
    const int rate = 44100;
    float[] samples = APSWave.Synthesize(profile, rate);
    using var stream = new MemoryStream();
    using (var writer = new BinaryWriter(stream, Encoding.ASCII, true))
    {
        writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples.Length * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
        writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
        writer.Write((short)2); writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(samples.Length * 2);
        foreach (float sample in samples) writer.Write((short)Math.Clamp((int)(sample * 32767), short.MinValue, short.MaxValue));
    }
    byte[] bytes = stream.ToArray();
    var decoded = APSWave.Decode(bytes);
    if (decoded.Rate != rate || decoded.Samples.Length != samples.Length || !decoded.Samples.Any(x => Math.Abs(x) > .05f))
        throw new InvalidDataException("Generated WAV failed round-trip validation.");
    File.WriteAllBytes(Path.Combine(args[0], profile + ".wav"), bytes);
}
Console.WriteLine("Generated and validated three synthetic launch sounds.");
