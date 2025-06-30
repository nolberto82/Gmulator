namespace GNes.Core.Mappers
{
    internal class Mapper000 : BaseMapper
    {
        public Mapper000(Header Header) : base(Header)
        {
            PrgSize = Header.PrgBanks * 0x4000;
            ChrSize = Header.ChrBanks * 0x2000;

            Prom = Header.Prom;
            Vrom = Header.Vrom;

            var Mmu = Header.Mmu;

            if (Header.PrgBanks == 1)
            {
                Mmu.CopyRam(Prom, 0x8000, PrgSize);
                Mmu.CopyRam(Prom, 0xc000, PrgSize);
            }
            else
                Mmu.CopyRam(Prom, 0x8000, PrgSize);

            Mmu.CopyVram(Vrom, 0x0000, ChrSize);

            Reset();

            Sram = true;
        }

        public override byte ReadPrg(int a) => base.ReadPrg(0x4000 * Prg[(a & 0x4000) >> 14] + a % 0x4000);

        public override void Reset()
        {
            if (Header.PrgBanks > 1)
                Prg = [0, 1];
            else
                Prg = [0, 0];
            Chr = [0, 1];
            base.Reset();
        }

        public override void Scanline() => base.Scanline();

        public override void SetLatch(int a, byte v) => base.SetLatch(a, v);
    }
}
