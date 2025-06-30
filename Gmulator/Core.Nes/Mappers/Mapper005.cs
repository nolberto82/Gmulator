using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GNes.Core.Mappers
{
    internal class Mapper005 : BaseMapper
    {
        private int NametableBank { get; set; }

        public Mapper005(Header cart) : base(cart)
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

        public override void Write(int a, byte v)
        {
            if (a == 0x5100)
                PrgMode = v & 3;
            else if (a == 0x5101)
                ChrMode = v & 3;
            else if (a == 0x5105)
            {
                NametableBank = v;
                switch (v)
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

            else if (a >= 0x5113 && a <= 0x5117)
                UpdatePrgBanks(a, v);
            else if (a >= 0x5120 && a <= 0x512b)
            {
                switch (ChrMode)
                {
                    case 0:
                        break;
                    case 1:
                        break;
                    case 2:
                        Chr[a & 15] = v;
                        break;
                    case 3:
                        Chr[a & 15] = v;
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

        public override byte ReadChr(int a)
        {
            if (a >= 0x2000) return 0;
            if (ChrMode == 1)
                return base.ReadChr(a);
            else if (ChrMode == 2)
                return base.ReadChr(a);
            else if (ChrMode == 3)
            {
                if (a == 0x7ff)
                { }
                var i = a >> 10;
                i = i % 4 + 8;

                return base.ReadChr(0x0400 * Chr[i] + a % 0x0400);
            }

            return base.ReadChr(0x0400 * Chr[a >> 10] + a % 0x0400);
        }

        public override void WritePrg(int a, byte v) => base.WritePrg(0x2000 * Prg[(a % 0x8000) >> 13] + a % 0x2000, v);

        public override byte ReadVram(int a) => base.ReadVram(0x400 * NametableBank + a % 0x400 + 0x2000);

        public override void Reset()
        {
            Prg = [0, 1, 2, (byte)(Header.PrgBanks * 2 - 1)];
            Chr = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
            base.Reset();
        }

        public override void Scanline() => base.Scanline();

        public override void SetLatch(int a, byte v) => base.SetLatch(a, v);

        private void UpdatePrgBanks(int a, byte v)
        {
            switch (PrgMode)
            {
                case 0:
                    break;
                case 1:
                    break;
                case 2:
                    if (a == 0x5115)
                    {
                        Prg[0] = (byte)(v & 0x7f);
                        Prg[1] = (byte)(Prg[0] + 1);
                    }
                    else
                        Prg[a - 0x5114] = (byte)(v & 0x7f);
                    break;
                case 3:
                    Prg[a - 0x5114 - 1] = (byte)(v & 0x7f);
                    break;
            }
        }
    }
}
