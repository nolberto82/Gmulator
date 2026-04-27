namespace Gmulator.Core.Snes.Sa1
{
    public partial class SnesSa1
    {
        public byte ReadRegister(int a)
        {
            a &= 0xffff;
            switch (a)
            {
                case 0x2300:
                {
                    return (byte)((_irqRequest ? 0x80 : 0) |
                    (_irqVectorSelect ? 0x40 : 0) |
                    (_nmiVectorSelect ? 0x10 : 0) |
                    (_snesCharConvIrqFlag ? 0x20 : 0) |
                    _sa1Message & 0x0f);
                }
                case 0x2301:
                {
                    return (byte)((_sa1IrqRequest ? 0x80 : 0) |
                    (_sa1NmiRequest ? 0x20 : 0) |
                    ((_snesMessage & 0x0f) != 0 ? 0x01 : 0)); ;
                }
                case 0x2302:
                {

                    return 0;
                }
                case 0x2303:
                {

                    return 0;
                }
                case 0x2304:
                {

                    return 0;
                }
                case 0x2305:
                {

                    return 0;
                }
                case >= 0x2306 and <= 0x230a:
                {
                    int shift = (a - 0x2306) * 8;
                    return (byte)((_mathResult >> shift) & 0xff);
                }
                case 0x230b:
                {

                    return 0;
                }
                case 0x230c:
                {

                    return 0;
                }
                case 0x230d:
                {

                    return 0;
                }
                case 0x230e:
                {

                    return 0;
                }
            }
            return 0;
        }

        public void WriteSnesRegister(int a, byte v)
        {
            switch (a)
            {
                case 0x2200:
                    _sa1IrqRequest = (v & 0x80) != 0;
                    _sa1Wait = (v & 0x40) != 0;
                    if ((v & 0x20) == 0 && _sa1Reset)
                        ResetVector();
                    _sa1Reset = (v & 0x20) != 0;
                    _sa1NmiRequest = (v & 0x10) != 0;
                    _snesMessage = v & 0x0f;
                    CheckInterrupts();
                    break;
                case 0x2201:
                    _irqEnabled = (v & 0x80) != 0;
                    _snesCharConvIrqEnabled = (v & 0x20) != 0;
                    CheckInterrupts();
                    break;
                case 0x2202:
                    if ((v & 0x80) != 0)
                        _irqRequest = false;
                    if ((v & 0x20) != 0)
                        _snesCharConvIrqFlag = false;
                    CheckInterrupts();
                    break;
                case 0x2203 or 0x2204:
                    if (a % 2 == 1)
                        _resetVector = (_resetVector & 0xff00) | v;
                    else
                        _resetVector = (_resetVector & 0xff) | v << 8;
                    break;
                case 0x2205 or 0x2206:
                    if (a % 2 == 1)
                        _nmiVector = (_nmiVector & 0xff00) | v;
                    else
                        _nmiVector = (_nmiVector & 0xff) | v << 8;
                    break;
                case 0x2207 or 0x2208:
                    if (a % 2 == 1)
                        _irqVector = (_irqVector & 0xff00) | v;
                    else
                        _irqVector = (_irqVector & 0xff) | v << 8;
                    break;
                case >= 0x2220 and <= 0x2223:
                    _mmcBanks[a & 3] = v;
                    UpdateMmcBanks();
                    break;
                case 0x2224:
                    _bwCpuBank = v & 0x1f;
                    UpdateRamBanks();
                    break;
                case 0x2225:
                    _bwSa1Bank = v & 0x7f;
                    UpdateRamBanks();
                    break;
                case 0x2226: Snes.Mapper.SramEnabled = (v & 0x80) != 0; break;
                case 0x2228: _bwRamRegionProtect = v & 0xf; break;
                case 0x2229: _cpuIramProtect = v; break;
                default:
                    WriteRegister(a, v);
                    break;
            }
        }

        public void WriteSa1Register(int a, byte v)
        {
            switch (a & 0xffff)
            {
                case 0x2209:
                    _sa1Message = v & 0x0f;
                    _nmiVectorSelect = (v & 0x10) != 0;
                    _irqVectorSelect = (v & 0x40) != 0;
                    _irqRequest = (v & 0x80) != 0;
                    CheckInterrupts();
                    break;
                case 0x220a:
                    _sa1IrqEnabled = (v & 0x80) != 0;
                    _sa1NmiEnabled = (v & 0x10) != 0;
                    CheckInterrupts();
                    break;
                case 0x220b:
                    if ((v & 0x80) != 0)
                        _sa1IrqRequest = false;
                    if ((v & 0x10) != 0)
                        _sa1NmiRequest = false;
                    CheckInterrupts();
                    break;
                case 0x2225:
                    _bwSa1Bank = v & 0x7f;
                    UpdateRamBanks();
                    break;
                case 0x2227:

                    break;
                case 0x222a:

                    break;
                case 0x2238:

                    break;
                case 0x2239:

                    break;
                case 0x223f:

                    break;
                case >= 0x2240 and <= 0x224f:

                    break;
                case 0x2250:
                    _mathControl = v & 3;
                    break;
                case 0x2251 or 0x2252:
                    if (a % 2 == 1)
                        _multiplicand = (_multiplicand & 0xff00) | v;
                    else
                        _multiplicand = (_multiplicand & 0xff) | v << 8;
                    break;
                case 0x2253 or 0x2254:
                    if (a % 2 == 1)
                        _multiplier = (_multiplier & 0xff00) | v;
                    else
                    {
                        _multiplier = (_multiplier & 0xff) | v << 8;
                        if (_mathControl == 0)
                            _mathResult = (short)_multiplicand * (short)_multiplier;
                        else if (_mathControl == 1)
                            _mathResult = _multiplier != 0 ? (_multiplicand / _multiplier) & 0xffff : 0;
                        else
                            _mathResult = (_multiplicand + _multiplier) & 0xffff;
                    }
                    break;
                case 0x2258:
                    break;
                case 0x2259:
                    break;
                case 0x225a:
                    break;
                case 0x225b:
                    break;
                default:
                    WriteRegister(a, v);
                    break;
            }
        }

        private void WriteRegister(int a, int v)
        {
            switch (a & 0xffff)
            {
                case 0x2230:
                    _dmaSrcDevice = v & 3;
                    _dmaDstDevice = (v >> 2) & 3;
                    _dmaCharConv = (v & 0x10) != 0;
                    _dmaMode = (v & 0x20) != 0;
                    _dmaControl = (v & 0x80) != 0;
                    break;
                case 0x2231:

                    break;
                case 0x2232:
                    _dmaSrcStartAddr = (_dmaSrcStartAddr & 0xffff00) | v;
                    break;
                case 0x2233:
                    _dmaSrcStartAddr = (_dmaSrcStartAddr & 0xff00ff) | v << 8;
                    break;
                case 0x2234:
                    _dmaSrcStartAddr = (_dmaSrcStartAddr & 0x00ffff) | v << 16;
                    break;
                case 0x2235:
                    _dmaDstStartAddr = (_dmaDstStartAddr & 0xffff00) | v;
                    break;
                case 0x2236:
                    _dmaDstStartAddr = (_dmaDstStartAddr & 0xff00ff) | v << 8;
                    if (_dmaControl && !_dmaCharConv)
                    {

                    }
                    else if (_dmaMode && _dmaCharConv)
                    {
                        _charDmaActive = true;
                        _snesCharConvIrqFlag = true;
                        CheckInterrupts();
                    }
                    break;
                case 0x2237:
                    _dmaDstStartAddr = (_dmaDstStartAddr & 0x00ffff) | v << 16;
                    break;
            }
        }
    }
}
