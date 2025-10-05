using Gmulator.Core.Gbc;
using Gmulator.Core.Snes.Mappers;
using Gmulator.Shared;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gmulator.Core.Snes
{
    public class SnesMmu : EmuState
    {
        public RamType MemType { get; set; }
        private byte[] Ram;
        private SnesCpu Cpu;
        private SnesPpu Ppu;
        private SnesApu Apu;
        private SnesSa1 Sa1;
        private BaseMapper Mapper;
        private SnesDma Dma;
        private SnesJoypad Joypad;
        private int RamAddr;

        public void SetSnes(SnesCpu cpu, SnesPpu ppu, SnesApu apu, SnesSa1 sa1, BaseMapper mapper, SnesDma dma, SnesJoypad joypad)
        {
            Cpu = cpu;
            Ppu = ppu;
            Apu = apu;
            Sa1 = sa1;
            Mapper = mapper;
            Dma = dma;
            Joypad = joypad;
            Ram = new byte[0x20000];
        }

        public byte ReadRam(int a) => Ram[a];
        public byte[] GetWram() => Ram;

        public void Reset()
        {
            Array.Fill<byte>(Ram, 0);
        }

        public int Read(int a)
        {
            int bank = a >> 16;
            a = a & 0xffff;
            if (bank == 0x7e || bank == 0x7f)
            {
                MemType = RamType.Wram;
                return Ram[(((bank & 1) << 16) | a & 0xffff)];
            }
            else if (a < 0x8000 && (bank < 0x40 || bank >= 0x80 && bank < 0xc0))
            {
                switch (a)
                {
                    case <= 0x1fff: MemType = RamType.Wram; return Ram[a];
                    case >= 0x2100 and <= 0x213f: return Ppu.Read(a) & 0xff;
                    case <= 0x217f:
                        MemType = RamType.Register;
                        Apu.Step();
                        return Apu.ReadFromSpu(a);
                    case >= 0x2300 and < 0x2400: MemType = RamType.Register; return SnesSa1.ReadReg(a);
                    case >= 0x3000 and <= 0x37ff:
                        MemType = RamType.Register;
                        //if (Mapper.CoProcessor == BaseMapper.CoprocessorGsu)
                        //    return Gsu.Read(a);
                        //else
                        return Sa1.Read(a);
                    case 0x4016: MemType = RamType.Register; return Joypad.Read4016();
                    case >= 0x4200 and <= 0x421f: MemType = RamType.Register; return Ppu.ReadIO(a);
                    case >= 0x4300 and <= 0x437f:
                    {
                        return Dma.Read(a) & 0xff;
                    }
                    case >= 0x6000 and <= 0x7fff: MemType = RamType.Sram; return Mapper.ReadBwRam(a);
                }
            }
            MemType = RamType.Rom;
            return Mapper.Read(bank, a);
        }

        public void Write(int a, int val)
        {
            int bank = a >> 16;
            byte v = (byte)val;
            a = a & 0xffff;
            if (bank == 0x7e || bank == 0x7f)
            {
                MemType = RamType.Wram;
                Ram[((bank & 1) << 16) | (a & 0xffff)] = v;
            }
            else if (a < 0x8000 && ((uint)bank < 0x40u || ((uint)bank >= 0x80u && (uint)bank < 0xc0u)))
            {
                switch (a)
                {
                    case <= 0x1fff:
                        MemType = RamType.Wram;
                        Ram[a] = v;
                        break;
                    case >= 0x2100 and <= 0x213f:
                        MemType = RamType.Register;
                        Ppu.Write(a, v);
                        break;
                    case <= 0x217f:
                        MemType = RamType.Register;
                        Apu.Step();
                        Apu.WriteToSpu(a & 3, v);
                        break;
                    case 0x2180:
                        MemType = RamType.Register;
                        Ram[((RamAddr & 0x10000) | (RamAddr++ & 0xffff))] = v;
                        break;
                    case 0x2181:
                        MemType = RamType.Register;
                        RamAddr = (RamAddr & 0xffff00) | v;
                        break;
                    case 0x2182:
                        MemType = RamType.Register;
                        RamAddr = (RamAddr & 0xff00ff) | (v << 8);
                        break;
                    case 0x2183:
                        MemType = RamType.Register;
                        RamAddr = (RamAddr & 0x00ffff) | (v << 16);
                        break;
                    case >= 0x2200 and < 0x2300:
                        MemType = RamType.Register;
                        Sa1.WriteReg(a, v);
                        return;
                    case >= 0x3000 and <= 0x37ff:
                        MemType = RamType.Register;
                        //if (Mapper.CoProcessor == BaseMapper.CoprocessorGsu)
                        //    Gsu.Write(a, v);
                        //else if (Mapper.CoProcessor == BaseMapper.CoprocessorSa1)
                        Sa1.Write(a, v);
                        break;
                    case >= 0x4200 and <= 0x420a:
                        MemType = RamType.Register;
                        Ppu.WriteIO(a, v);
                        break;
                    case 0x420b: Dma.StatusDma(v); break;
                    case 0x420c: Dma.StatusHdma(v); break;
                    case 0x420d:
                        Cpu.FastMem = (v & 1) != 0;
                        break;
                    case >= 0x4300 and <= 0x437f:
                        Dma.Write(a, v, RamAddr);
                        MemType = RamType.Register;
                        break;
                    case >= 0x6000 and <= 0x7fff:
                        MemType = RamType.Sram;
                        Mapper.WriteBwRam(a, v);
                        break;
                }
            }
            Mapper.Write(bank, a, v);
        }

        public override void Load(BinaryReader br)
        {
            Ram = EmuState.ReadArray<byte>(br, Ram.Length);
        }

        public override void Save(BinaryWriter bw)
        {
            bw.Write(Ram);
        }
    }
}
