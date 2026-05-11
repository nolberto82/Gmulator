namespace Gmulator;

public partial class Program
{
    [STAThread]
    private static void Main()
    {
        if (Environment.CurrentDirectory == "C:\\WINDOWS\\system32")
            Directory.SetCurrentDirectory($"{AppContext.BaseDirectory}");
        
#if DECKDEBUG || DECKRELEASE
        GuiDeck Gui = new();
        Gui.Init(true);
        Console.WriteLine($"{Environment.CurrentDirectory}");
        Gui.Run();

#else
        GuiDesktop Gui = new();
        Gui.Init(false);
        Gui.Run();

#endif
    }
}

