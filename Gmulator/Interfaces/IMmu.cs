
namespace Gmulator.Interfaces;
public interface IMmu
{
    public delegate int ReadDel(int a);
    public delegate void WriteDel(int a, int v);
    public int ReadByte(int a);
    public void WriteByte(int a, int v);
}
