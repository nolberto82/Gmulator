namespace Gmulator.Interfaces;
public interface ICpu
{
    Action Tick { get; set; }
    List<RegisterInfo> GetRegisters();
    List<RegisterInfo> GetFlags();
    int GetReg(string reg);
    void SetReg(string reg, int v);
    void Step();
    void Save(BinaryWriter bw);
    void Load(BinaryReader br);
}
