namespace Gmulator.Core.Gbc
{
    public partial class GbcCpu
    {
        #region 00 Instructions
        private void OpAdc8(int r1)
        {
            int c = (byte)(F & FC) >> 4;
            int v = _a + r1 + c;
            SetF((v & 0xff) == 0, FZ); SetF(false, FN);
            SetF((((_a & 0xf) + (r1 & 0xf) + c) & 0x10) != 0, FH);
            SetF(v > 0xff, FC);
            _a = (byte)v;
        }

        private void OpAdd8(int r1)
        {
            int v = _a + r1;
            SetF((v & 0xff) == 0, FZ); SetF(false, FN);
            SetF(((_a & 0xf) + (r1 & 0xf) & 0x10) != 0, FH);
            SetF(v > 0xff, FC);
            _a = (byte)v;
        }

        private ushort OpAdd(int r1, int r2)
        {
            int v = r1 + r2;
            SetF(false, FN); SetF(v > 0xffff, FC);
            SetF((((r1 & 0xfff) + (r2 & 0xfff)) & 0x1000) != 0, FH);
            Tick();
            return (ushort)v;
        }

        private ushort OpAddSP(int r1, int r2, bool f8 = false)
        {
            int v = r1 + r2;
            if (!f8)
            {
                Tick(); Tick();
            }
            else
                Tick();

            SetF(false, FZ); SetF(false, FN);
            SetF(((r1 & 0xf) + (r2 & 0xf) & 0x10) != 0, FH);
            SetF((byte)r1 + (byte)r2 > 0xff, FC);
            return (ushort)v;
        }

        private void OpAnd(int r1)
        {
            int v = _a & r1;
            SetF(v == 0, FZ); SetF(false, FN);
            SetF(true, FH); SetF(false, FC);

            _a = (byte)v;
        }

        private void OpCall(bool flag)
        {
            if (flag)
            {
                OpPush((ushort)(PC + 2));
                PC = OpLdImm16();
            }
            else
            {
                PC += 2;
                Tick();
            }
            Tick();
        }

        private void OpCcf()
        {
            int c = (F ^ FC) & FC;
            SetF(false, FN); SetF(false, FH); SetF(c != 0, FC);
        }

        private void OpCp(int r1)
        {
            int v = _a - r1;
            SetF(v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
            SetF(((_a & 0xf) - (r1 & 0xf) & 0x10) != 0, FH);
        }

        private void OpCpl()
        {
            int r1 = _a ^ 0xff;
            SetF(true, FN); SetF(true, FH);
            _a = (byte)r1;
        }

        private void OpDaa()
        {
            int v = _a;
            if ((F & FN) != 0)
            {
                if ((F & FH) != 0)
                    v -= 6;
                if ((F & FC) != 0)
                    v -= 0x60;
            }
            else
            {
                if ((F & FH) != 0 || (_a & 0xf) > 9)
                    v += 6;
                if ((F & FC) != 0 || _a > 0x99)
                {
                    v += 0x60;
                    SetF(true, FC);
                }
            }

            SetF((v & 0xff) == 0, FZ); SetF(false, FH);

            _a = (byte)v;
        }

        private byte OpDec8(int r1)
        {
            int v = r1 - 1;
            SetF((v & 0xff) == 0, FZ); SetF(true, FN);
            SetF((v & 0x0f) == 0x0f, FH);
            return (byte)v;
        }

        private ushort OpDec16(int r1)
        {
            Tick();
            return (ushort)(r1 - 1);
        }

        private void OpDI() => Ime = false;

        private void OpEI()
        {
            ImeDelay = 1;
            Ime = true;
        }

        private byte OpInc8(int r1)
        {
            int o = r1;
            int v = r1 + 1;
            SetF(false, FN); SetF((o & 0xf) == 0xf, FH);
            SetF((v & 0xff) == 0, FZ);

            return (byte)v;
        }

        private ushort OpInc16(int r1)
        {
            Tick();
            return (ushort)(r1 + 1);
        }

        private void OpJp(bool flag)
        {
            if (flag)
                PC = OpLdImm16();
            else
            {
                PC += 2;
                Tick();
            }
            Tick();
        }

        private void OpJr(bool flag)
        {
            if (flag)
                PC += (ushort)((sbyte)ReadCycle(PC) + 1);
            else
                PC++;
            Tick();
        }

        private ushort OpLdHLSP(int r1, int r2) => OpAddSP(r1, r2, true);
        private byte OpLdReg(int addr) => ReadCycle(addr);
        private byte OpLdImm8() => ReadCycle(PC++);
        private int OpLdImm16() => (ReadCycle(PC++) | ReadCycle(PC++) << 8) & 0xffff;
        private void OpLdWr(int a, int v) => WriteCycle(a, (byte)v);

        private void OpLdWr16(int v)
        {
            int a = GetWord(OpLdImm8(), OpLdImm8());
            WriteCycle(a, (byte)v);
            WriteCycle(a + 1, (byte)(v >> 8));
        }

        private void OpOr(int r1)
        {
            int v = _a | r1;
            SetF(v == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF(false, FC);
            _a = (byte)v;
        }

        private ushort OpPop(bool af = false)
        {
            int l = ReadCycle(SP++);
            int h = ReadCycle(SP++);

            if (af)
            {
                SetF((l & FZ) != 0, FZ);
                SetF((l & FN) != 0, FN);
                SetF((l & FH) != 0, FH);
                SetF((l & FC) != 0, FC);
                l = F;
            }
            return (ushort)(h << 8 | l);
        }

        public void OpPush(int r1)
        {
            WriteCycle(--SP, (byte)(r1 >> 8));
            WriteCycle(--SP, (byte)(r1 & 0xff));
        }

        private void OpRet(bool flag, bool c3 = false)
        {
            if (flag)
            {
                PC = OpPop();
                if (!c3)
                    Tick();
            }
            Tick();
        }

        private void OpReti()
        {
            Ime = true;
            PC = OpPop();
            //OpRet(true);
            Tick();
        }

        private byte OpRl(int r1)
        {
            int c = (F & FC) >> 4;
            int v = r1 << 1 | c;

            SetF((byte)v == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF((r1 >> 7) != 0, FC);
            return (byte)v;
        }

        private void OpRla()
        {
            int v = (ushort)(_a << 1);
            int oc = (byte)(F & FC) >> 4;
            int c = (byte)(v >> 8);

            SetF(false, FZ); SetF(false, FN);
            SetF(false, FH); SetF(c != 0, FC);
            _a = (byte)(v | oc);
        }

        private void OpRlca()
        {
            int v = (ushort)(_a << 1);
            int c = (byte)(v >> 8);

            SetF(false, FZ); SetF(false, FN);
            SetF(false, FH); SetF(c != 0, FC);
            _a = (byte)(v | c);
        }

        private byte OpRr(int r1)
        {
            int oc = (F & FC) >> 4;
            int v = r1 >> 1 | (oc << 7);

            SetF(v == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF((r1 & 1) != 0, FC);
            return (byte)v;
        }

        private void OpRra()
        {
            int oc = (F & FC) >> 4;
            int v = _a >> 1;

            SetF(false, FZ); SetF(false, FN);
            SetF(false, FH); SetF((_a & 1) != 0, FC);
            _a = (byte)(v | (oc << 7));
        }

        private void OpRrca()
        {
            int c = (byte)(_a & 1);
            _a = (byte)(_a >> 1);

            SetF(false, FZ); SetF(false, FN);
            SetF(false, FH); SetF(c != 0, FC);
            _a = (byte)(_a | (c << 7));
        }

        private void OpRst(ushort r1, bool interrupt = false)
        {
            Tick();
            if (interrupt)
                OpPush(PC);
            else
                OpPush(PC++);
            PC = r1;
        }

        private void OpSbc8(int r1)
        {
            int c = (byte)(F & FC) >> 4;
            int v = _a - r1 - c;

            SetF((byte)v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
            SetF((((_a & 0xf) - (r1 & 0xf) - c) & 0x10) != 0, FH);
            _a = (byte)v;
        }

        private void OpScf()
        {
            SetF(false, FN); SetF(false, FH); SetF(true, FC);
        }

        private void OpSub8(int r1)
        {
            int v = _a - r1;

            SetF(v == 0, FZ); SetF(true, FN); SetF(v < 0, FC);
            SetF((((_a & 0xf) - (r1 & 0xf)) & FH) != 0, FH);
            _a = (byte)v;
        }

        private void OpXor(int r1)
        {
            int v = _a ^ r1;

            SetF(v == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF(false, FC);
            _a = (byte)v;
        }
        #endregion

        #region CB Instructions
        private void OpBit(int r1, int r2)
        {
            int v = r2 & (1 << r1);
            SetF((v & 0xff) == 0, FZ); SetF(false, FN); SetF(true, FH);
        }

        private byte OpRlc(int r1)
        {
            int c;
            int v = r1;
            c = (byte)(v >> 7);
            v <<= 1;

            SetF(v == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF(c != 0, FC);
            return (byte)(v | c);
        }

        private byte OpRrc(int r1)
        {
            int v = r1;
            int c = (byte)(v & 1);
            v = (v >> 1) | (c << 7);

            SetF(v == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF(c != 0, FC);
            return (byte)v;
        }

        private byte OpSla(int r1)
        {
            int v = r1;
            int c = (byte)(v >> 7);
            v <<= 1;

            SetF((v & 0xff) == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF(c != 0, FC);
            return (byte)v;
        }

        private byte OpSra(int r1)
        {
            int v = r1;
            int c = (byte)(v & 1);
            v = (v >> 1) | (v & 0x80);

            SetF((v & 0xff) == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF(c != 0, FC);
            return (byte)v;
        }

        private byte OpSwap(int r1)
        {
            int v = r1;
            int n1;
            int n2;
            (n1, n2) = (v & 0x0f, v >> 4);
            v = n1 << 4 | n2;

            SetF((byte)v == 0, FZ); SetF(false, FN);
            SetF(false, FH); SetF(false, FC);
            return (byte)v;
        }

        private byte OpSrl(int r1)
        {
            int v = r1;
            int c = (byte)(r1 & 1);
            v >>= 1;

            SetF((v & 0xff) == 0, FZ);
            SetF(false, FN);
            SetF(false, FH);
            SetF(c != 0, FC);
            return (byte)v;
        }

        private static byte OpRes(int r1, int r2) => (byte)(r2 & ~(1 << r1));
        private void OpResHL(int r1) => OpLdWr(HL, OpLdReg(HL) & ~(1 << r1));
        private static byte OpSet(int r1, int r2) => (byte)(r2 | (1 << r1));
        private void OpSetHL(int r1) => OpLdWr(HL, OpLdReg(HL) | (1 << r1));
        #endregion
    }
}
