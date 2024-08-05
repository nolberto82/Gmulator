namespace GNes.Core.Mappers
{
    internal class Mapper001 : BaseMapper
    {
        public int Writes { get; private set; }
        public int Control { get; private set; }
        public int Shift { get; private set; }
        public int PrgBank { get; private set; }

        public Mapper001(Header cart) : base(cart)
        {
            Header = cart;
            var Mmu = Header.Mmu;
            int prgsize = Header.PrgBanks * 0x4000;
            int chrsize = Header.ChrBanks * 0x2000;

            Prom = Header.Prom;
            Vrom = cart.Vrom;

            Buffer.BlockCopy(Prom, 0, Mmu.Ram, 0x8000, prgsize / Header.PrgBanks);
            Buffer.BlockCopy(Prom, prgsize - prgsize / Header.PrgBanks, Mmu.Ram, 0xc000, prgsize / Header.PrgBanks);

            if (Header.ChrBanks > 0)
                Buffer.BlockCopy(Vrom, 0, Mmu.Vram, 0x0000, chrsize / Header.ChrBanks);

            Reset();
        }

        public override byte ReadPrg(int a)
        {
            if (a < 0xc000)
                return base.ReadPrg(0x4000 * Prg[0] + a % 0x4000);
            else
                return base.ReadPrg(0x4000 * Prg[1] + a % 0x4000);
        }

        public override byte ReadChr(int a)
        {
            if (Header.ChrBanks > 0)
                return base.ReadChr(0x1000 * Chr[a >> 12] + a % 0x1000);
            else
                return Header.Mmu.Vram[a];
        }

        public override void WritePrg(int a, byte v)
        {
            if (a < 0xc000)
                base.WritePrg(0x4000 * Prg[0] + a % 0x4000, v);
            else
                base.WritePrg(0x4000 * Prg[1] + a % 0x4000, v);
        }

        public override void Write(int a, byte v)
        {
            if (a <= 0xffff)
            {
                if (v.GetBit(7))
                {
                    Control |= 0xc;
                    Shift = 0x10;
                    Reset();
                }
                else
                {
                    Shift = Shift >> 1 | (v & 0x01) << 4;
                    Writes++;
                }

                if (Writes == 5)
                {
                    Control = (byte)Shift;
                    if (a <= 0x9fff)
                    {
                        Header.Mirror = Control & 3;
                        PrgMode = (Control >> 2) & 3;
                        ChrMode = (Control >> 4) & 1;

                        //UpdatePrg((byte)Shift);
                    }
                    else if (a <= 0xbfff)
                    {
                        UpdateChr((byte)Shift, 0);
                    }
                    else if (a <= 0xdfff)
                    {
                        UpdateChr((byte)Shift, 1);
                    }
                    else if (a <= 0xffff)
                    {
                        UpdatePrg((byte)Shift);
                        Sram = true;
                    }
                    Writes = 0;
                }
            }

            base.Write(a, v);
        }

        public override void SetLatch(int a, byte v)
        {
            base.SetLatch(a, v);
        }

        public override void Reset()
        {
            int prgsize = Header.PrgBanks * 0x2000;
            Prg = [0, (byte)((prgsize / 0x2000) - 1)];
            Chr = [0, 1];
            base.Reset();
        }

        public override void Scanline()
        {
            base.Scanline();
        }

        private void UpdatePrg(byte v)
        {
            v &= 0xf;
            if (PrgMode == 0 || PrgMode == 1)
            {
                Prg[0] = ((byte)(v & 0xfe));
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
