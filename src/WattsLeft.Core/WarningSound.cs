using System.Runtime.InteropServices;

namespace WattsLeft;

/// <summary>
/// WattsLeft's own warning chimes, made when first needed rather than shipped as files: a soft two-note
/// fall for low, a quicker three-note fall for critical. They play in WattsLeft's own entry in the
/// Windows volume mixer, so their volume can be set apart from everything else.
/// </summary>
internal static partial class WarningSound
{
    private const int SampleRate = 44100;
    private const uint Async = 0x0001, NoDefault = 0x0002, Memory = 0x0004;

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PlaySound(IntPtr sound, IntPtr module, uint flags);

    // Notes in hertz, and when each starts, in seconds, all from the C major chord so they stay in tune. Low
    // falls E to C; critical is quicker and carries on down to the G below, ending lower and darker.
    private static readonly (double Frequency, double Start)[] LowNotes = [(659.25, 0), (523.25, 0.16)];
    private static readonly (double Frequency, double Start)[] CriticalNotes = [(659.25, 0), (523.25, 0.11), (392.00, 0.22)];

    // Built once and pinned for good: an asynchronous PlaySound reads the memory while it plays.
    private static readonly Lazy<GCHandle> Low = new(() => GCHandle.Alloc(Wave(LowNotes), GCHandleType.Pinned));
    private static readonly Lazy<GCHandle> Critical = new(() => GCHandle.Alloc(Wave(CriticalNotes), GCHandleType.Pinned));

    public static void Play(bool critical) =>
        PlaySound((critical ? Critical : Low).Value.AddrOfPinnedObject(), IntPtr.Zero, Memory | Async | NoDefault);

    /// <summary>The chime as a .wav file's bytes, for <see cref="Play"/> and for listening to while designing.</summary>
    public static byte[] Wave(bool critical) => Wave(critical ? CriticalNotes : LowNotes);

    /// <summary>
    /// Bell-like notes: a sine with a little of its second and third harmonics, a few milliseconds' rise so
    /// it doesn't click, then ringing away. Peaks at half of full scale, so it's noticeable without startling.
    /// </summary>
    private static byte[] Wave((double Frequency, double Start)[] notes)
    {
        const double ring = 0.18, length = 0.5, rise = 0.006;
        int count = (int)((notes[^1].Start + length) * SampleRate);
        var samples = new double[count];
        foreach (var (frequency, start) in notes)
        {
            int first = (int)(start * SampleRate);
            for (int i = 0; i < (int)(length * SampleRate) && first + i < count; i++)
            {
                double t = (double)i / SampleRate;
                double envelope = Math.Min(1, t / rise) * Math.Exp(-t / ring);
                double w = 2 * Math.PI * frequency * t;
                samples[first + i] += envelope * (Math.Sin(w) + 0.25 * Math.Sin(2 * w) + 0.08 * Math.Sin(3 * w));
            }
        }
        double peak = samples.Max(Math.Abs);

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        // A plain 16-bit mono PCM .wav: RIFF header, format chunk, data chunk.
        writer.Write("RIFF"u8);
        writer.Write(36 + count * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(count * 2);
        foreach (double sample in samples)
            writer.Write((short)(sample / peak * 0.5 * short.MaxValue));
        writer.Flush();
        return stream.ToArray();
    }
}
