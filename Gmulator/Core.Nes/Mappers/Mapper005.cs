namespace Gmulator.Core.Nes.Mappers
{
    internal class Mapper005 : BaseMapper
    {
        private int NametableBank { get; set; }

        public Mapper005(Header header, NesMmu mmu) : base(header, mmu)
        {
            Reset();
        }

        public override void Write(int addr, byte value)
        {
            if (addr == 0x5100)
                PrgMode = value & 3;
            else if (addr == 0x5101)
                ChrMode = value & 3;
            else if (addr == 0x5105)
            {
                NametableBank = value;
                switch (value)
                {
                    case 0x50: Header.Mirror = Horizontal; break;
                    case 0x44: Header.Mirror = Vertical; break;
                    case 0x00: Header.Mirror = SingleNt0; break;
                    case 0x55: Header.Mirror = SingleNt1; break;
                        //case 0xaa: Cartridge.Mirror = Horizontal; break;
                        //case 0xff: Cartridge.Mirror = Horizontal; break;
                        //case 0x14: Cartridge.Mirror = Horizontal; break;
                }
            }
            //else if (a == 0x5106)

            else if (addr >= 0x5113 && addr <= 0x5117)
                UpdatePrgBanks(addr, value);
            else if (addr >= 0x5120 && addr <= 0x512b)
            {
                switch (ChrMode)
                {
                    case 0:
                        break;
                    case 1:
                        break;
                    case 2:
                        Chr[addr & 15] = value;
                        break;
                    case 3:
                        Chr[addr & 15] = value;
                        break;
                }
            }
        }

        public override byte ReadPrg(int a)
        {
            if (PrgMode == 1)
                return base.ReadPrg(a);
            else if (PrgMode == 2)
            {
                if (a <= 0xbfff)
                    return base.ReadPrg(0x2000 * Prg[(a % 0x8000) >> 13] + a % 0x2000);
                else
                    return base.ReadPrg(0x2000 * Prg[a >> 14] + a % 0x2000);
            }

            else if (PrgMode == 3)
                return base.ReadPrg(a);
            return base.ReadPrg(0x10000 * Prg[a >> 14] + a % 0x10000);
        }

        public override byte ReadChr(int addr)
        {
            if (addr >= 0x2000) return 0;
            if (ChrMode == 1)
                return base.ReadChr(addr);
            else if (ChrMode == 2)
                return base.ReadChr(addr);
            else if (ChrMode == 3)
            {
                if (addr == 0x7ff)
                { }
                var i = addr >> 10;
                i = i % 4 + 8;

                return base.ReadChr(0x0400 * Chr[i] + addr % 0x0400);
            }

            return base.ReadChr(0x0400 * Chr[addr >> 10] + addr % 0x0400);
        }

        public override void WritePrg(int addr, byte value) => base.WritePrg(0x2000 * Prg[(addr % 0x8000) >> 13] + addr % 0x2000, value);

        public override byte ReadVram(int addr) => base.ReadVram(0x400 * NametableBank + addr % 0x400 + 0x2000);

        public override void Reset()
        {
            Prg = [0, 1, 2, (byte)(Header.PrgBanks * 2 - 1)];
            Chr = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
            base.Reset();
        }

        public override void Scanline() => base.Scanline();

        public override void SetLatch(int a, byte v) => base.SetLatch(a, v);

        private void UpdatePrgBanks(int addr, byte value)
        {
            switch (PrgMode)
            {
                case 0:
                    break;
                case 1:
                    break;
                case 2:
                    if (addr == 0x5115)
                    {
                        Prg[0] = (byte)(value & 0x7f);
                        Prg[1] = (byte)(Prg[0] + 1);
                    }
                    else
                        Prg[addr - 0x5114] = (byte)(value & 0x7f);
                    break;
                case 3:
                    Prg[addr - 0x5114 - 1] = (byte)(value & 0x7f);
                    break;
            }
        }
    }
}
