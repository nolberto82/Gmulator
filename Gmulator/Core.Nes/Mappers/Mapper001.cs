namespace Gmulator.Core.Nes.Mappers
{
    internal class Mapper001 : BaseMapper
    {
        public int Writes { get; private set; }
        public int Control { get; private set; }
        public int Shift { get; private set; }
        public int PrgBank { get; private set; }

        public Mapper001(Header header, NesMmu mmu) : base(header, mmu)
        {
            Reset();
        }

        public override int ReadPrg(int addr) => base.ReadPrg(0x4000 * Prg[addr >> 14 & 1] + addr % 0x4000);

        public override int ReadChr(int addr)
        {
            if (Header.ChrBanks > 0)
                return base.ReadChr(0x1000 * Chr[addr >> 12] + addr % 0x1000);
            else
                return Header.Mmu.Vram[addr];
        }

        public override void WritePrg(int addr, int value)
        {
            if (addr < 0xc000)
                base.WritePrg(0x4000 * Prg[0] + addr % 0x4000, value);
            else
                base.WritePrg(0x4000 * Prg[1] + addr % 0x4000, value);
        }

        public override void Write(int addr, int value)
        {
            if ((value & 0x80) != 0)
            {
                Control |= 0x0c;
                Shift = 0x10;
                Reset();
            }
            else
            {
                Shift = Shift >> 1 | (value & 0x01) << 4;
                Writes++;
            }

            if (Writes == 5)
            {
                Control = (byte)Shift;
                if (addr <= 0x9fff)
                {
                    Header.Mirror = Control & 3;
                    PrgMode = (Control >> 2) & 3;
                    ChrMode = (Control >> 4) & 1;

                    //UpdatePrg((byte)Shift);
                }
                else if (addr <= 0xbfff)
                {
                    UpdateChr((byte)Shift, 0);
                }
                else if (addr <= 0xdfff)
                {
                    UpdateChr((byte)Shift, 1);
                }
                else if (addr <= 0xffff)
                {
                    UpdatePrg((byte)Shift);
                    SramEnabled = true;
                }
                Writes = 0;
            }

            base.Write(addr, value);
        }

        public override void SetLatch(int addr, int value) => base.SetLatch(addr, value);

        public override void Reset()
        {
            int prgsize = Header.PrgBanks * 0x2000;
            Prg = [0, (byte)((prgsize / 0x2000) - 1)];
            Chr = [0, 1];
            base.Reset();
        }

        public override void Scanline() => base.Scanline();

        private void UpdatePrg(byte v)
        {
            v &= 0xf;
            if (PrgMode == 0 || PrgMode == 1)
            {
                Prg[0] = (byte)(v & 0xfe);
            }
            else if (PrgMode == 2)
            {
                Prg[0] = 0;
                Prg[1] = v;
            }
            else if (PrgMode == 3)
            {
                Prg[0] = v;
            }
        }

        private void UpdateChr(byte v, int b)
        {
            if (ChrMode == 0)
            {
                Chr[0] = v;
                Chr[1] = (byte)(Chr[0] | 1);
            }
            else
            {
                Chr[b] = v;
            }

        }
    }
}
