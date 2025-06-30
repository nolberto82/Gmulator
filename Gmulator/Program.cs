public partial class Program
{
    [STAThread]
    private static void Main()
    {
#if DECKDEBUG || DECKRELEASE
        GuiDeck Gui = new();
        Gui.Init(true);
        Gui.Run(true);
#else
        GuiDesktop Gui = new();
        Gui.Init(false);
        Gui.Run(false);
#endif
    }
}

