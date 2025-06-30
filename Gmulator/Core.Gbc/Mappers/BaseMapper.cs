namespace Gmulator.Core.Gbc.Mappers;
public abstract class BaseMapper : EmuState
{
    public byte[] Rom { get; set; }
    public int Rombank { get; set; }
    public int Rambank { get; set; }
    public bool CartRamOn { get; set; }
    public bool CGB { get; set; }
    public int Ramsize { get; set; }
    public string Name { get; set; }

    public virtual void Init(byte[] rom, string name)
    {
        Rom = rom;
        Rombank = 1;
        CGB = rom[0x143].GetBit(7);
        Ramsize = rom[0x149];
        Name = name;
    }
        
    public virtual byte ReadRom(int a) => Rom[a % Rom.Length];
    public abstract Span<byte> ReadRomBlock(int a, int size);
    public abstract void WriteRom0(int a, byte v, bool edit = false);
    public abstract void WriteRom1(int a, byte v, bool edit = false);

    public void SetRomBank(int number) => Rombank = number;

    public override void Save(BinaryWriter bw)
    {
        bw.Write(Name);
        bw.Write(Ramsize);
        bw.Write(Rombank);
        bw.Write(Rambank);
        bw.Write(CartRamOn);
        bw.Write(CGB);
    }

    public override void Load(BinaryReader br)
    {
        Name = br.ReadString();
        Ramsize = br.ReadInt32();
        Rombank = br.ReadInt32();
        Rambank = br.ReadInt32();
        CartRamOn = br.ReadBoolean();
        CGB = br.ReadBoolean();
    }
}