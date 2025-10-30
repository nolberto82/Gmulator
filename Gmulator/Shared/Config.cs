using Raylib_cs;
using System.IO;
using System.Text.Json;

namespace Gmulator.Shared;
public class Config
{
    public string WorkingDir { get; set; } = "C:";
    public int Volume { get; set; } = 10;
    public int FrameSkip { get; set; } = 10;
    public int RotateAB { get; set; }

    public Config() { }
    public Config(string directory, int volume, int frameskip, int ab)
    {
        WorkingDir = directory;
        Volume = volume;
        FrameSkip = frameskip;
        RotateAB = ab;
    }

    public void Load()
    {
        var file = @$"{ConfigDirectory}/Settings.json";
        if (File.Exists(file))
        {
            var res = JsonSerializer.Deserialize<List<Config>>(File.ReadAllText(file), GEmuJsonContext.Default.Options);
            if (res != null && res.Count > 0)
            {
                WorkingDir = res[0].WorkingDir ?? "C:";
                Audio.SetVolume(res[0].Volume);
                Volume = res[0].Volume;
                FrameSkip = res[0].FrameSkip;
                RotateAB = res[0].RotateAB;
            }
        }
    }

    public void Save()
    {
        List<Config> config = [new(WorkingDir, Volume, FrameSkip, RotateAB)];
        var file = @$"{ConfigDirectory}/Settings.json";
        var json = JsonSerializer.Serialize(config, GEmuJsonContext.Default.Options);
        File.WriteAllText(file, json);
    }

    public static void CreateDirectories(bool isdeck)
    {
        if (!Directory.Exists(RomDirectory))
            Directory.CreateDirectory(RomDirectory);

        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);

        if (!Directory.Exists(StateDirectory))
            Directory.CreateDirectory(StateDirectory);

        if (!Directory.Exists(CheatDirectory))
            Directory.CreateDirectory(CheatDirectory);

        if (!Directory.Exists(ConfigDirectory))
            Directory.CreateDirectory(ConfigDirectory);

        if (!isdeck && !Directory.Exists(DebugDirectory))
            Directory.CreateDirectory(DebugDirectory);
    }
}
