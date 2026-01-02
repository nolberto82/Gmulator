namespace Gmulator.Core.Nes.Mappers
{
    internal class Mapper000 : BaseMapper
    {
        public Mapper000(Header Header, NesMmu mmu) : base(Header, mmu)
        {
            Reset();
        }

        public override int ReadPrg(int a) => base.ReadPrg(0x4000 * Prg[(a & 0x4000) >> 14] + a % 0x4000);

        public override void Reset()
        {
            if (Header.PrgBanks > 1)
                Prg = [0, 1];
            else
                Prg = [0, 0];
            Chr = [0];
            base.Reset();
        }

        public override void Scanline() => base.Scanline();

        public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
    }
}
