
namespace Gmulator.Interfaces;

public interface ICpu
{
    ulong Cycles { get; }
    Action Tick { get; set; }
    int StepOverAddr { get; set; }
    List<RegisterInfo> GetRegisters();
    List<RegisterInfo> GetFlags();
    int GetReg(string reg);
    void SetReg(string reg, int value);
    void Step();
    void Save(BinaryWriter bw);
    void Load(BinaryReader br);
}

public interface ICpuState
{

}
