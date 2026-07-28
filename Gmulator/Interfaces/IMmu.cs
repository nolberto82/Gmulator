
namespace Gmulator.Interfaces;

public interface IMmu
{
    public delegate int ReadDel(int a);
    public delegate void WriteDel(int a, int value);
    public int ReadByte(int addr);
    public void WriteByte(int addr, int value);
    public int ReadWord(int addr);
    public void WriteWord(int addr, int value);
    public int ReadLong(int addr);
    public void WriteLong(int addr, int value);
    public int ReadVram(int addr);
    public int GetOffset(int addr);

}
