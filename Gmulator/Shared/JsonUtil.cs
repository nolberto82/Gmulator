using System.Text.Json.Serialization;

namespace Gmulator;
public static partial class JsonUtil
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(List<Config>))]
    [JsonSerializable(typeof(List<Breakpoint>))]
    [JsonSerializable(typeof(List<Cheat>))]
    public partial class GEmuJsonContext : JsonSerializerContext
    {
    }
}
