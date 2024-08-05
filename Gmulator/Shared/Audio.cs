
using Raylib_cs;

namespace Gmulator;
public class Audio
{
    public static AudioStream Stream { get; private set; }
    private static int MaxSamples;

    public Audio(int maxsamples)
    {
        MaxSamples = maxsamples;
        Raylib.InitAudioDevice();

        Raylib.SetAudioStreamBufferSizeDefault(MaxSamples);
        Stream = Raylib.LoadAudioStream(44100, 32, 2);
        SetVolume(0.01f);
        Raylib.PlayAudioStream(Stream);
    }

    public static void SetVolume(float v) => Raylib.SetMasterVolume(v);
    public static void Update(float[] AudioBuffer)
    {
        unsafe
        {
            if (Raylib.IsAudioStreamProcessed(Stream))
            {
                fixed (float* ptr = AudioBuffer)
                    Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
            }
        }
    }

    public static void Update(short[] AudioBuffer)
    {
        unsafe
        {
            if (Raylib.IsAudioStreamProcessed(Stream))
            {
                fixed (void* ptr = AudioBuffer)
                    Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
            }
        }
    }

    public static void Update(ushort[] AudioBuffer)
    {
        unsafe
        {
            if (Raylib.IsAudioStreamProcessed(Stream))
            {
                fixed (void* ptr = AudioBuffer)
                    Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
            }
        }
    }

    public static void Update(byte[] AudioBuffer)
    {
        unsafe
        {
            if (Raylib.IsAudioStreamProcessed(Stream))
            {
                fixed (void* ptr = AudioBuffer)
                    Raylib.UpdateAudioStream(Stream, ptr, MaxSamples);
            }
        }
    }

    public static void Unload()
    {
        Raylib.UnloadAudioStream(Stream);
        Raylib.CloseAudioDevice();
    }
}
