
namespace Gmulator.Interfaces;

public interface IMmu
{
    public delegate byte ReadDel(int a);
    public delegate void WriteDel(int a, byte value);
    public byte ReadByte(int addr);
    public void WriteByte(int addr, byte value);
    public int ReadWord(int addr);
    public void WriteWord(int addr, int value);
    public int ReadLong(int addr);
    public void WriteLong(int addr, int value);
    public byte ReadVram(int addr);
    public int GetOffset(int addr);

}
