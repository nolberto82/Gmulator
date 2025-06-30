
using Raylib_cs;
using System.IO;

namespace Gmulator;
public class Audio
{
    public static AudioStream Stream { get; private set; }
    private static int MaxSamples;

    public Audio()
    {
        Raylib.InitAudioDevice();
    }

    public static void Init(uint freq, int maxsamples, int buffersize, uint samplesize)
    {
        MaxSamples = maxsamples;
        Raylib.SetAudioStreamBufferSizeDefault(MaxSamples);
        Stream = Raylib.LoadAudioStream(freq, samplesize, 2);
        Raylib.PlayAudioStream(Stream);
    }

    public static void SetVolume(float v)
    {
        Raylib.SetMasterVolume(v);
        Raylib.SetAudioStreamVolume(Stream, v);
    }

    public unsafe static void Update(float[] AudioBuffer)
    {
        if (Raylib.IsAudioStreamProcessed(Stream))
        {
            fixed (void* ptr = AudioBuffer)
                Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
        }
    }

    public unsafe static void Update(short[] AudioBuffer)
    {
        if (Raylib.IsAudioStreamProcessed(Stream))
        {
            fixed (void* ptr = AudioBuffer)
                Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
        }
    }

    public unsafe static void Update(ushort[] AudioBuffer)
    {
        if (Raylib.IsAudioStreamProcessed(Stream))
        {
            fixed (void* ptr = AudioBuffer)
                Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
        }

    }

    public unsafe static void Update(byte[] AudioBuffer)
    {
        if (Raylib.IsAudioStreamProcessed(Stream))
        {
            fixed (void* ptr = AudioBuffer)
                Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
        }
    }

    public static void Unload()
    {
        Raylib.UnloadAudioStream(Stream);
        Raylib.CloseAudioDevice();
    }
}
