using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Gmulator.Shared;

public class Config
{
    public string WorkingDir { get; set; } = "C:";
    public int Volume { get; set; } = 2;
    public int FrameSkip { get; set; } = 1;
    [JsonIgnore]
    public int RotateAB { get; set; }

    private string _gameName;

    private Action<int> SetRotateAB;

    public Config() { }

    public Config(string name, Action<int> setAB)
    {
        _gameName = name;
        SetRotateAB = setAB;
    }
    public Config(string workingdir, int volume, int frameskip)
    {
        WorkingDir = workingdir;
        Volume = volume;
        FrameSkip = frameskip;
    }

    public void Load()
    {
        var file = @$"{ConfigDirectory}/Settings.json";
        if (File.Exists(file))
        {
            var res = JsonSerializer.Deserialize<List<Config>>(File.ReadAllText(file), GEmuJsonContext.Default.Options);
            if (res != null && res.Count > 0)
            {
                Audio.SetVolume(res[0].Volume);
                WorkingDir = res[0].WorkingDir;
                Volume = res[0].Volume;
                FrameSkip = res[0].FrameSkip;
            }
        }

        if (_gameName == null || _gameName == string.Empty) return;
        file = @$"{ConfigDirectory}/{Path.GetFileNameWithoutExtension(_gameName)}.json";
        if (File.Exists(file))
        {
            var json = File.ReadAllText(file);
            var res = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            if (res != null && res.Count > 0)
                RotateAB = Convert.ToInt32($"{res["AB"]}");
            SetRotateAB(RotateAB);
        }
    }

    public void Save(string name)
    {
        List<Config> config = [new(WorkingDir, Volume, FrameSkip)];
        var file = @$"{ConfigDirectory}/Settings.json";
        var json = JsonSerializer.Serialize(config, GEmuJsonContext.Default.Options);
        File.WriteAllText(file, json);

        if (name == null || name == string.Empty) return;
        file = @$"{ConfigDirectory}/{Path.GetFileNameWithoutExtension(name)}.json";
        var data = new { AB = RotateAB };
        json = JsonSerializer.Serialize(data);
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
