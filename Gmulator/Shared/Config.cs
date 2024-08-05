using Raylib_cs;
using System.IO;
using System.Text.Json;

namespace Gmulator
{
    public class Config
    {
        public string WorkingDir { get; set; }
        public float Volume { get; set; }
        public bool RotateAB { get; set; }

        public Config() { }
        public Config(string directory, float volume, bool ab)
        {
            WorkingDir = directory;
            Volume = volume;
            RotateAB = ab;
        }

        public static void Load()
        {
            var file = @$"{ConfigDirectory}/Settings.json";
            if (File.Exists(file))
            {
                var res = JsonSerializer.Deserialize<List<Config>>(File.ReadAllText(file), GEmuJsonContext.Default.Options);
                if (res.Count > 0)
                {
                    Menu.WorkingDir = res[0].WorkingDir;
                    Raylib.SetMasterVolume(res[0].Volume);
                    Input.RotateAB = res[0].RotateAB;
                }
            }
        }

        public static void Save()
        {
            List<Config> config = [new(Menu.WorkingDir, Raylib.GetMasterVolume(), Input.RotateAB)];
            var file = @$"{ConfigDirectory}/Settings.json";
            var json = JsonSerializer.Serialize(config, GEmuJsonContext.Default.Options);
            File.WriteAllText(file, json);
        }

        public static void CreateDirectories()
        {
            if (!Directory.Exists(RomDirectory))
                Directory.CreateDirectory(RomDirectory);

            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);

            if (!Directory.Exists(StateDirectory))
                Directory.CreateDirectory(StateDirectory);

            if (!Directory.Exists(CheatDirectory))
                Directory.CreateDirectory(CheatDirectory);

            if (!Directory.Exists(LuaDirectory))
                Directory.CreateDirectory(LuaDirectory);

            if (!Directory.Exists(ConfigDirectory))
                Directory.CreateDirectory(ConfigDirectory);

            if (!Directory.Exists(DebugDirectory))
                Directory.CreateDirectory(DebugDirectory);
        }
    }
}
