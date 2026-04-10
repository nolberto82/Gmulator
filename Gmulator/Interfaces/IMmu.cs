
namespace Gmulator.Interfaces;

public interface IMmu
{
    public delegate int ReadDel(int a);
    public delegate void WriteDel(int a, int v);
    public byte[] Wram { get; protected set; }
    public int ReadByte(int a);
    public void WriteByte(int a, int v);
    public int ReadWord(int a);
    public void WriteWord(int a, int v);
    public int ReadLong(int a);
    public void WriteLong(int a, int v);
    public int GetOffset(int a);
}
