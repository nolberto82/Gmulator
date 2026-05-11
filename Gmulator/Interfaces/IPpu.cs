namespace Gmulator.Interfaces;

public interface IPpu
{
    int GetScanline();
    ReadOnlySpan<uint> ScreenBuffer { get; }
    List<RegisterInfo> GetState();
}
