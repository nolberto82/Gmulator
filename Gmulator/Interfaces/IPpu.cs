namespace Gmulator.Interfaces;
public interface IPpu
{
    int GetScanline();
    uint[] ScreenBuffer { get; set; }
    List<RegisterInfo> GetState();
}
