namespace Gmulator.Shared;

public class CheatConverter
{
    public static Dictionary<(int, int), Cheat> Cheats { get; private set; }
    public static Cheat Cheat { get; private set; }

    public CheatConverter(Dictionary<(int, int), Cheat> cheats)
    {
        Cheats = cheats;
    }
}
