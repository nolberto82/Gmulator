namespace Gmulator.Interfaces;
internal interface ISaveState
{
    void Save(BinaryWriter bw);
    void Load(BinaryReader br);
}
