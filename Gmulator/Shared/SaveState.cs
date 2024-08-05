using System.Text;

namespace Gmulator.Shared;
public abstract class SaveState
{
    public abstract void Save(BinaryWriter bw);
    public abstract void Load(BinaryReader br);
}
