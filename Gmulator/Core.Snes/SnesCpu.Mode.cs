namespace Gmulator.Core.Snes;
public partial class SnesCpu
{
	public int GetMode(int mode)
	{
		int pbr = PB << 16;
		switch (mode)
		{
			case Absolute:
			{
				var a = Read(pbr | PC++) | Read(pbr | PC++) << 8;
				return DB << 16 | a;
			}
			case AbsoluteIndexedIndirect:
			{
				var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8) + X & 0xffff;
				return pbr | a;
			}
			case AbsoluteIndexedX:
			{
				var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8) & 0xffff;
				if ((a + X & 0xff00) != (a & 0xff00)) Idle();
				if (!XMem) Idle();
				return DB << 16 | a + (XMem ? (byte)X : X);
			}
			case AbsoluteIndexedY:
			{
				Idle();
				var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8) & 0xffff;
				if ((a + X & 0xff00) != (a & 0xff00)) Idle();
				if (!XMem) Idle();
				return DB << 16 | a + Y;
			}
			case AbsoluteIndirect:
			{
				var a = Read(pbr | PC++) | Read(pbr | PC++) << 8;
				return a;
			}
			case AbsoluteIndirectLong:
			{
				var a = Read(pbr | PC++) | Read(pbr | PC++) << 8;
				return a;
			}
			case AbsoluteLong:
			{
				var a = Read(pbr | PC++) | Read(pbr | PC++) << 8 | Read(pbr | PC++) << 16;
				return a;
			}
			case AbsoluteLongIndexedX:
			{
				var a = (Read(pbr | PC++) | Read(pbr | PC++) << 8 | Read(pbr | PC++) << 16) + X;
				return a;
			}
			case Accumulator: return 0;
			case DPIndexedIndirectX:
			{
				int a = Read(pbr | PC++);
				var b = (a + X) & 0xffff;
				if ((D & 0xff) != 0) Idle();
				Idle();
				if (E && (D & 0xff) == 0)
				{
					int d = (D & 0xff00) | b & 0xff;
					a = Read(d);
					a |= Read((d & 0xff) == 0xff ? b + 1 : b + 1) << 8;
				}
				else
				{
					int d = (b + D) & 0xffff;
					a = Read(d);
					a |= Read((d & 0xff) == 0xff ? (d & 0xff00) : d + 1) << 8;
				}
				return DB << 16 | a;
			}
			case DPIndexedX:
			{
				if ((D & 0xff) != 0) Idle();
				Idle();
				var a = (Read(pbr | PC++) + D + X) & 0xffff;
				a = (E && a > 0xff ? a & 0xff | 0x100 : a) & 0xffff;
				return a;
			}
			case DPIndexedY:
			{
				int a = Read(pbr | PC++);
				var b = (a + Y) & 0xffff;
				if (E && (D & 0xff) == 0)
					a = (D & 0xff00) | b & 0xff;
				else
					a = (b + D) & 0xffff;
				return a;
			}
			case DPIndirect:
			{
				var b = Read(pbr | PC++) & 0xffff;
				if ((D & 0xff) != 0) Idle();
				var a = DB << 16 | ReadWord(b + D);
				return a;
			}
			case DPIndirectIndexedY:
			{
				var b = Read(pbr | PC++) & 0xffff;
				if ((D & 0xff) != 0) Idle();
				Idle();
				int a = (DB << 16) | ReadWord(b + D) + Y;
				return a;
			}
			case DPIndirectLong:
			{
				int a = ReadLong((Read(pbr | PC++) + D) & 0xffff);
				return a;
			}
			case DPIndirectLongIndexedY:
			{
				int b = Read(pbr | PC++) & 0xffff;
				if ((D & 0xff) != 0) Idle();
				return ReadLong(b + D) + Y;
			}
			case DirectPage:
			{
				int a = Read(pbr | PC++);
				return (a + D) & 0xffff;
			}
			case Immediate:
			{
				int a = Read(pbr | PC++);
				Idle();
				return a;
			}
			case ImmediateIndex:
			{
				if (!XMem)
					return (Read(pbr | PC++) | Read(pbr | PC++) << 8) & 0xffff;
				else
					return Read(pbr | PC++);
			}
			case ImmediateMemory:
			{
				if (!MMem)
					return Read(pbr | PC++) | Read(pbr | PC++) << 8;
				else
					return Read(pbr | PC++);
			}
			case Implied:
				return 0;
			case ProgramCounterRelative:
				return pbr | (PC + (sbyte)Read(pbr | PC) + 1) & 0xffff;

			case ProgramCounterRelativeLong:
			{
				var a = Read(pbr | PC) | (Read(pbr | PC + 1) << 8);
				return pbr | PC + a + 2 & 0xffff;
			}
			case SRIndirectIndexedY:
			{
				Idle(); Idle();
				return DB << 16 | ReadWord((Read(pbr | PC++) + D + SP) & 0xffff) + Y;
			}
			case StackAbsolute:
				return Read(pbr | PC++) | Read(pbr | PC++) << 8;

			case StackDPIndirect:
			{
				var a = Read(pbr | PC++);
				return (a + D) & 0xffff;
			}
			case StackInterrupt: return 0;
			case StackPCRelativeLong: return Read(pbr | PC++) | Read(pbr | PC++) << 8;
			case StackRelative:
			{
				if ((D & 0xff) != 0) Idle();
				Idle();
				return Read(pbr | PC++) + SP;
			}
		}
		return 0;
	}
}
