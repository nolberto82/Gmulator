using static Gmulator.Interfaces.IMmu;

namespace Gmulator.Shared
{
    public class MemoryMap
    {
        public List<MemoryHandler> Handlers { get; set; }

        private readonly int _size;

        public MemoryMap(int size)
        {
            _size = size;
            Handlers = [];
            for (int i = 0; i < size; i++)
                Handlers.Add(new(0, a => 0, (a, v) => { }, RamType.None));
        }

        public int ReadByte(int a) => Handlers[a >> 12].Ram[a & 0xfff];

        public void Wram(int bankStart, int bankEnd, int addrStart, int addrEnd, ReadDel r, WriteDel w)
        {
            int offset = 0;
            for (int i = bankStart; i <= bankEnd; i++)
            {
                for (int j = addrStart; j <= addrEnd; j += 0x1000)
                {
                    int a = _size == 0x1000 ? i << 4 | (j >> 12) : j;
                    Handlers[a].Offset = offset;
                    Handlers[a].Type = RamType.Wram;
                    Handlers[a].Read = r;
                    Handlers[a].Write = w;
                    offset += 0x1000;
                }
                if (addrEnd == 0x1fff)
                    offset = 0;
            }
        }

        public void Sram(int bankStart, int bankEnd, int addrStart, int addrEnd, ReadDel r, WriteDel w, int mmcbank = 0)
        {
            int offset = 0;
            for (int i = bankStart; i <= bankEnd; i++)
            {
                if ((i % 2) == 0)
                    offset = 0;
                for (int j = addrStart; j <= addrEnd; j += 0x1000)
                {
                    int a = _size == 0x1000 ? i << 4 | (j >> 12) : j;
                    Handlers[a].Offset = offset | (mmcbank * 2) * 0x1000;
                    Handlers[a].Type = RamType.Sram;
                    Handlers[a].Mask = addrEnd - addrStart;
                    Handlers[a].Read = r;
                    Handlers[a].Write = w;
                    offset += 0x1000;
                }
                if (addrStart == 0x6000)
                    offset = 0;
            }
        }

        public void Register(int bankStart, int bankEnd, int addrStart, int addrEnd, ReadDel r, WriteDel w)
        {
            int offset = 0;
            for (int i = bankStart; i <= bankEnd; i++)
            {
                for (int j = addrStart; j <= addrEnd; j += 0x1000)
                {
                    int a = _size == 0x1000 ? i << 4 | (j >> 12) : j;
                    Handlers[a].Offset = addrStart + offset;
                    Handlers[a].Type = RamType.Register;
                    Handlers[a].Read = r;
                    Handlers[a].Write = w;
                    offset += 0x1000;
                }
            }
        }

        public void Rom(int bankStart, int bankEnd, int addrStart, int addrEnd, ReadDel r, WriteDel w)
        {
            int offset = 0;
            for (int i = bankStart; i <= bankEnd; i++)
            {
                for (int j = addrStart; j <= addrEnd; j += 0x1000)
                {
                    int a = _size == 0x1000 ? i << 4 | (j >> 12) : j;
                    Handlers[a].Offset = offset;
                    Handlers[a].Type = RamType.Rom;
                    Handlers[a].Read = r;
                    Handlers[a].Write = w;
                    offset += 0x1000;
                }
            }
        }

        public void Iram(int bankStart, int bankEnd, int addrStart, int addrEnd, ReadDel r, WriteDel w)
        {
            int offset = 0;
            for (int i = bankStart; i <= bankEnd; i++)
            {
                for (int j = addrStart; j <= addrEnd; j += 0x1000)
                {
                    int a = _size == 0x1000 ? i << 4 | (j >> 12) : j;
                    Handlers[a].Offset = offset;
                    Handlers[a].Type = RamType.Iram;
                    Handlers[a].Read = r;
                    Handlers[a].Write = w;
                    offset += 0x1000;
                }
            }
        }

        public void Set(int bank_s, int bank_e, int addr_s, int addr_e, ReadDel read, WriteDel write, RamType type, int add, int mmcbank = 0)
        {
            int page = 0;
            int offset = 0;
            for (int i = bank_s; i <= bank_e; i++)
            {
                if (addr_e - addr_s <= 0x1fff)
                    offset = 0;

                for (int j = addr_s; j <= addr_e; j += add)
                {
                    int a = add == 0x1000 ? i << 4 | (j >> 12) : j;
                    Handlers[a].Offset = offset | mmcbank;
                    Handlers[a].Read = read;
                    Handlers[a].Write = write;
                    Handlers[a].Type = type;
                    offset += 0x1000;
                }
                page++;
            }
        }
    }
}
