using Gmulator.Core.Gbc;
using static Gmulator.Shared.Cheat;

namespace Gmulator.Shared;

public class CheatConverter
{
    public static Dictionary<int, Cheat> Cheats { get; private set; }
    public static Cheat Cheat { get; private set; }

    public CheatConverter(Dictionary<int, Cheat> cheats)
    {
        Cheats = cheats;
    }


}
