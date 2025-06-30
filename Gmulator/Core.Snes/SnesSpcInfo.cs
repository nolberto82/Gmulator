using System.Text.Json.Serialization;

namespace Gmulator.Core.Snes;
public partial class SnesSpc
{
	public struct Opcode(string name, string oper, string format, int id, int mode, int size)
    {
        public string Name = name;
        public string Oper = oper;
        public string Format = format;
        public int Id = id;
        public int Mode = mode;
        public int Size = size;
    };

	[JsonIgnore]
	public List<Opcode> Disasm = [];

	public void CreateOpcodes() 
	{
		Disasm.Add(new("nop", "", "", NOP, Impliedtype3, 1)); //00
		Disasm.Add(new("tcall", "0", "", TCALL, Impliedtype3, 1)); //01
		Disasm.Add(new("set1", "${0:x2}.0", "", SET1, DirectPageBit, 2)); //02
		Disasm.Add(new("bbs", "${0:x2}.0,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //03
		Disasm.Add(new("or", "a,${0:x2}", "", OR, DirectPage, 2)); //04
		Disasm.Add(new("or", "a,!${0:x4}", "", OR, Absolute, 3)); //05
		Disasm.Add(new("or", "a,(x)", "", OR, Impliedtype1, 1)); //06
		Disasm.Add(new("or", "a,[${0:x2}+x]", "", OR, DirectPageIndexedbyX, 2)); //07
		Disasm.Add(new("or", "a,#${0:x2}", "", OR, Immediate, 2)); //08
		Disasm.Add(new("or", "${0:x2},${1:x2}", "", OR, DirectPage, 3)); //09
		Disasm.Add(new("or1", "c,${0:x4}.{1}", "", OR1, AbsoluteBooleanBit, 3)); //0A
		Disasm.Add(new("asl", "${0:x2}", "", ASL, DirectPage, 2)); //0B
		Disasm.Add(new("asl", "!${0:x4}", "", ASL, Absolute, 3)); //0C
		Disasm.Add(new("push", "psw", "", PUSH, StackPSW, 1)); //0D
		Disasm.Add(new("tset1", "!${0:x4}", "", TSET1, Absolute, 3)); //0E
		Disasm.Add(new("brk", "", "", BRK, StackInterrupt, 1)); //0F
		Disasm.Add(new("bpl", "${0:x4}", "", BPL, ProgramCounterRelative, 2)); //10
		Disasm.Add(new("tcall", "1", "", TCALL, Impliedtype3, 1)); //11
		Disasm.Add(new("clr1", "${0:x2}.0", "", CLR1, DirectPageBit, 2)); //12
		Disasm.Add(new("bbc", "${0:x2}.0,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //13
		Disasm.Add(new("or", "a,${0:x2}+x", "", OR, DirectPageIndexedbyX, 2)); //14
		Disasm.Add(new("or", "a,!${0:x4}+x", "", OR, AbsoluteIndexedbyX, 3)); //15
		Disasm.Add(new("or", "a,!${0:x4}+y", "", OR, AbsoluteIndexedbyY, 3)); //16
		Disasm.Add(new("or", "a,[${0:x2}]+y", "", OR, DirectPageIndirectIndexedbyY, 2)); //17
		Disasm.Add(new("or", "${0:x2},#${1:x2}", "", OR, DirectPageImmediate, 3)); //18
		Disasm.Add(new("or", "(x),(y)", "", OR, ImpliedIndirecttype1, 1)); //19
		Disasm.Add(new("decw", "${0:x2}", "", DECW, DirectPage, 2)); //1A
		Disasm.Add(new("asl", "${0:x2}+x", "", ASL, DirectPageIndexedbyX, 2)); //1B
		Disasm.Add(new("asl", "a", "", ASL, Accumulator, 1)); //1C
		Disasm.Add(new("dec", "x", "", DEC, Impliedtype1, 1)); //1D
		Disasm.Add(new("cmp", "x,!${0:x4}", "", CMP, Absolute, 3)); //1E
		Disasm.Add(new("jmp", "[!${0:x4}+x]", "", JMP, AbsoluteIndexedbyX, 3)); //1F
		Disasm.Add(new("clrp", "", "", CLRP, Impliedtype2, 1)); //20
		Disasm.Add(new("tcall", "2", "", TCALL, Impliedtype3, 1)); //21
		Disasm.Add(new("set1", "${0:x2}.1", "", SET1, DirectPageBit, 2)); //22
		Disasm.Add(new("bbs", "${0:x2}.1,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //23
		Disasm.Add(new("and", "a,${0:x2}", "", AND, DirectPage, 2)); //24
		Disasm.Add(new("and", "a,!${0:x4}", "", AND, Absolute, 3)); //25
		Disasm.Add(new("and", "a,(x)", "", AND, ImpliedIndirecttype1, 1)); //26
		Disasm.Add(new("and", "a,[${0:x2}+x]", "", AND, DirectPageIndexedbyX, 2)); //27
		Disasm.Add(new("and", "a,#${0:x2}", "", AND, Immediate, 2)); //28
		Disasm.Add(new("and", "${0:x2},${1:x2}", "", AND, DirectPage, 3)); //29
		Disasm.Add(new("or1", "c,/${0:x4}.{1}", "", OR1, AbsoluteBooleanBit, 3)); //2A
		Disasm.Add(new("rol", "${0:x2}", "", ROL, DirectPage, 2)); //2B
		Disasm.Add(new("rol", "!${0:x4}", "", ROL, Absolute, 3)); //2C
		Disasm.Add(new("push", "a", "", PUSH, Accumulator, 1)); //2D
		Disasm.Add(new("cbne", "${0:x2},${1:x4}", "", CBNE, DirectPageProgramCounterRelative, 3)); //2E
		Disasm.Add(new("bra", "${0:x4}", "", BRA, ProgramCounterRelative, 2)); //2F
		Disasm.Add(new("bmi", "${0:x4}", "", BMI, ProgramCounterRelative, 2)); //30
		Disasm.Add(new("tcall", "3", "", TCALL, Impliedtype3, 1)); //31
		Disasm.Add(new("clr1", "${0:x2}.1", "", CLR1, DirectPageBit, 2)); //32
		Disasm.Add(new("bbc", "${0:x2}.1,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //33
		Disasm.Add(new("and", "a,${0:x2}+x", "", AND, DirectPageIndexedbyX, 2)); //34
		Disasm.Add(new("and", "a,!${0:x4}+x", "", AND, AbsoluteIndexedbyX, 3)); //35
		Disasm.Add(new("and", "a,!${0:x4}+y", "", AND, AbsoluteIndexedbyY, 3)); //36
		Disasm.Add(new("and", "a,[${0:x2}]+y", "", AND, DirectPageIndirectIndexedbyY, 2)); //37
		Disasm.Add(new("and", "${0:x2},#${1:x2}", "", AND, DirectPageImmediate, 3)); //38
		Disasm.Add(new("and", "(x),(y)", "", AND, ImpliedIndirecttype1, 1)); //39
		Disasm.Add(new("incw", "${0:x2}", "", INCW, DirectPage, 2)); //3A
		Disasm.Add(new("rol", "${0:x2}+x", "", ROL, DirectPageIndexedbyX, 2)); //3B
		Disasm.Add(new("rol", "a", "", ROL, Accumulator, 1)); //3C
		Disasm.Add(new("inc", "x", "", INC, Impliedtype1, 1)); //3D
		Disasm.Add(new("cmp", "x,${0:x2}", "", CMP, DirectPage, 2)); //3E
		Disasm.Add(new("call", "!${0:x4}", "", CALL, Absolute, 3)); //3F
		Disasm.Add(new("setp", "", "", SETP, Impliedtype2, 1)); //40
		Disasm.Add(new("tcall", "4", "", TCALL, Impliedtype3, 1)); //41
		Disasm.Add(new("set1", "${0:x2}.2", "", SET1, DirectPageBit, 2)); //42
		Disasm.Add(new("bbs", "${0:x2}.2,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //43
		Disasm.Add(new("eor", "a,${0:x2}", "", EOR, DirectPage, 2)); //44
		Disasm.Add(new("eor", "a,!${0:x4}", "", EOR, Absolute, 3)); //45
		Disasm.Add(new("eor", "a,(x)", "", EOR, ImpliedIndirecttype2, 1)); //46
		Disasm.Add(new("eor", "a,[${0:x2}+x]", "", EOR, DirectPageIndexedIndirectbyX, 2)); //47
		Disasm.Add(new("eor", "a,#${0:x2}", "", EOR, Immediate, 2)); //48
		Disasm.Add(new("eor", "${0:x2},${1:x2}", "", EOR, DirectPage, 3)); //49
		Disasm.Add(new("and1", "c,${0:x4}.{1}", "", AND1, AbsoluteBooleanBit, 3)); //4A
		Disasm.Add(new("lsr", "${0:x2}", "", LSR, DirectPage, 2)); //4B
		Disasm.Add(new("lsr", "!${0:x4}", "", LSR, Absolute, 3)); //4C
		Disasm.Add(new("push", "x", "", PUSH, StackX, 1)); //4D
		Disasm.Add(new("tclr1", "!${0:x4}", "", TCLR1, Absolute, 3)); //4E
		Disasm.Add(new("pcall", "u", "", PCALL, UppermostPage, 2)); //4F
		Disasm.Add(new("bvc", "${0:x4}", "", BVC, ProgramCounterRelative, 2)); //50
		Disasm.Add(new("tcall", "5", "", TCALL, Impliedtype3, 1)); //51
		Disasm.Add(new("clr1", "${0:x2}.2", "", CLR1, DirectPageBit, 2)); //52
		Disasm.Add(new("bbc", "${0:x2}.2,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //53
		Disasm.Add(new("eor", "a,${0:x2}+x", "", EOR, DirectPageIndexedbyX, 2)); //54
		Disasm.Add(new("eor", "a,!${0:x4}+x", "", EOR, AbsoluteIndexedbyX, 3)); //55
		Disasm.Add(new("eor", "a,!${0:x4}+y", "", EOR, AbsoluteIndexedbyY, 3)); //56
		Disasm.Add(new("eor", "a,[${0:x2}]+y", "", EOR, DirectPageIndirectIndexedbyY, 2)); //57
		Disasm.Add(new("eor", "${0:x2},#${1:x2}", "", EOR, DirectPageImmediate, 3)); //58
		Disasm.Add(new("eor", "(x),(y)", "", EOR, ImpliedIndirecttype1, 1)); //59
		Disasm.Add(new("cmpw", "ya,${0:x2}", "", CMPW, DirectPage, 2)); //5A
		Disasm.Add(new("lsr", "${0:x2}+x", "", LSR, DirectPageIndexedbyX, 2)); //5B
		Disasm.Add(new("lsr", "a", "", LSR, Accumulator, 1)); //5C
		Disasm.Add(new("mov", "x,a", "", MOV, Impliedtype1, 1)); //5D
		Disasm.Add(new("cmp", "y,!${0:x4}", "", CMP, Absolute, 3)); //5E
		Disasm.Add(new("jmp", "!${0:x4}", "", JMP, Absolute, 3)); //5F
		Disasm.Add(new("clrc", "", "", CLRC, Impliedtype2, 1)); //60
		Disasm.Add(new("tcall", "6", "", TCALL, Impliedtype3, 1)); //61
		Disasm.Add(new("set1", "${0:x2}.3", "", SET1, DirectPageBit, 2)); //62
		Disasm.Add(new("bbs", "${0:x2}.3,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //63
		Disasm.Add(new("cmp", "a,${0:x2}", "", CMP, DirectPage, 2)); //64
		Disasm.Add(new("cmp", "a,!${0:x4}", "", CMP, Absolute, 3)); //65
		Disasm.Add(new("cmp", "a,(x)", "", CMP, ImpliedIndirecttype1, 1)); //66
		Disasm.Add(new("cmp", "a,[${0:x2}+x]", "", CMP, DirectPageIndexedIndirectbyX, 2)); //67
		Disasm.Add(new("cmp", "a,#${0:x2}", "", CMP, Immediate, 2)); //68
		Disasm.Add(new("cmp", "${0:x2},${1:x2}", "", CMP, DirectPage, 3)); //69
		Disasm.Add(new("and1", "c,/${0:x4}.{1}", "", AND1, AbsoluteBooleanBit, 3)); //6A
		Disasm.Add(new("ror", "${0:x2}", "", ROR, DirectPage, 2)); //6B
		Disasm.Add(new("ror", "!${0:x4}", "", ROR, Absolute, 3)); //6C
		Disasm.Add(new("push", "y", "", PUSH, StackY, 1)); //6D
		Disasm.Add(new("dbnz", "${0:x2},${1:x4}", "", DBNZ, DirectPageProgramCounterRelative, 3)); //6E
		Disasm.Add(new("ret", "", "", RET, StackR, 1)); //6F
		Disasm.Add(new("bvs", "${0:x4}", "", BVS, ProgramCounterRelative, 2)); //70
		Disasm.Add(new("tcall", "7", "", TCALL, Impliedtype3, 1)); //71
		Disasm.Add(new("clr1", "${0:x2}.3", "", CLR1, DirectPageBit, 2)); //72
		Disasm.Add(new("bbc", "${0:x2}.3,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //73
		Disasm.Add(new("cmp", "a,${0:x2}+x", "", CMP, DirectPageIndexedbyX, 2)); //74
		Disasm.Add(new("cmp", "a,!${0:x4}+x", "", CMP, AbsoluteIndexedbyX, 3)); //75
		Disasm.Add(new("cmp", "a,!${0:x4}+y", "", CMP, AbsoluteIndexedbyY, 3)); //76
		Disasm.Add(new("cmp", "a,[${0:x2}]+y", "", CMP, DirectPageIndirectIndexedbyY, 2)); //77
		Disasm.Add(new("cmp", "${0:x2},#${1:x2}", "", CMP, DirectPageImmediate, 3)); //78
		Disasm.Add(new("cmp", "(x),(y)", "", CMP, ImpliedIndirecttype1, 1)); //79
		Disasm.Add(new("addw", "ya,${0:x2}", "", ADDW, DirectPage, 2)); //7A
		Disasm.Add(new("ror", "${0:x2}+x", "", ROR, DirectPageIndexedbyX, 2)); //7B
		Disasm.Add(new("ror", "a", "", ROR, Accumulator, 1)); //7C
		Disasm.Add(new("mov", "a,x", "", MOV, Impliedtype1, 1)); //7D
		Disasm.Add(new("cmp", "y,${0:x2}", "", CMP, DirectPage, 2)); //7E
		Disasm.Add(new("reti", "", "", RETI, StackR, 1)); //7F
		Disasm.Add(new("setc", "", "", SETC, Impliedtype2, 1)); //80
		Disasm.Add(new("tcall", "8", "", TCALL, Impliedtype3, 1)); //81
		Disasm.Add(new("set1", "${0:x2}.4", "", SET1, DirectPageBit, 2)); //82
		Disasm.Add(new("bbs", "${0:x2}.4,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //83
		Disasm.Add(new("adc", "a,${0:x2}", "", ADC, DirectPage, 2)); //84
		Disasm.Add(new("adc", "a,!${0:x4}", "", ADC, Absolute, 3)); //85
		Disasm.Add(new("adc", "a,(x)", "", ADC, Impliedtype1, 1)); //86
		Disasm.Add(new("adc", "a,[${0:x2}+x]", "", ADC, DirectPageIndexedbyX, 2)); //87
		Disasm.Add(new("adc", "a,#${0:x2}", "", ADC, Immediate, 2)); //88
		Disasm.Add(new("adc", "${0:x2},${1:x2}", "", ADC, DirectPage, 3)); //89
		Disasm.Add(new("eor1", "c,${0:x4}.{1}", "", EOR1, AbsoluteBooleanBit, 3)); //8A
		Disasm.Add(new("dec", "${0:x2}", "", DEC, DirectPage, 2)); //8B
		Disasm.Add(new("dec", "!${0:x4}", "", DEC, Absolute, 3)); //8C
		Disasm.Add(new("mov", "y,#${0:x2}", "", MOV, Immediate, 2)); //8D
		Disasm.Add(new("pop", "psw", "", POP, StackPSW, 1)); //8E
		Disasm.Add(new("mov", "${0:x2},#${1:x2}", "", MOV, DirectPageImmediate, 3)); //8F
		Disasm.Add(new("bcc", "${0:x4}", "", BCC, ProgramCounterRelative, 2)); //90
		Disasm.Add(new("tcall", "9", "", TCALL, Impliedtype3, 1)); //91
		Disasm.Add(new("clr1", "${0:x2}.4", "", CLR1, DirectPageBit, 2)); //92
		Disasm.Add(new("bbc", "${0:x2}.4,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //93
		Disasm.Add(new("adc", "a,${0:x2}+x", "", ADC, DirectPageIndexedbyX, 2)); //94
		Disasm.Add(new("adc", "a,!${0:x4}+x", "", ADC, AbsoluteIndexedbyX, 3)); //95
		Disasm.Add(new("adc", "a,!${0:x4}+y", "", ADC, AbsoluteIndexedbyY, 3)); //96
		Disasm.Add(new("adc", "a,[${0:x2}]+y", "", ADC, DirectPageIndirectIndexedbyY, 2)); //97
		Disasm.Add(new("adc", "${0:x2},#${1:x2}", "", ADC, DirectPageImmediate, 3)); //98
		Disasm.Add(new("adc", "(x),(y)", "", ADC, Impliedtype1, 1)); //99
		Disasm.Add(new("subw", "ya,${0:x2}", "", SUBW, DirectPage, 2)); //9A
		Disasm.Add(new("dec", "${0:x2}+x", "", DEC, DirectPageIndexedbyX, 2)); //9B
		Disasm.Add(new("dec", "a", "", DEC, Accumulator, 1)); //9C
		Disasm.Add(new("mov", "x,sp", "", MOV, Impliedtype1, 1)); //9D
		Disasm.Add(new("div", "ya,x", "", DIV, Impliedtype1, 1)); //9E
		Disasm.Add(new("xcn", "a", "", XCN, Accumulator, 1)); //9F
		Disasm.Add(new("ei", "", "", EI, Impliedtype2, 1)); //A0
		Disasm.Add(new("tcall", "10", "", TCALL, Impliedtype3, 1)); //A1
		Disasm.Add(new("set1", "${0:x2}.5", "", SET1, DirectPageBit, 2)); //A2
		Disasm.Add(new("bbs", "${0:x2}.5,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //A3
		Disasm.Add(new("sbc", "a,${0:x2}", "", SBC, DirectPage, 2)); //A4
		Disasm.Add(new("sbc", "a,!${0:x4}", "", SBC, Absolute, 3)); //A5
		Disasm.Add(new("sbc", "a,(x)", "", SBC, ImpliedIndirecttype1, 1)); //A6
		Disasm.Add(new("sbc", "a,[${0:x2}+x]", "", SBC, DirectPageIndexedbyX, 2)); //A7
		Disasm.Add(new("sbc", "a,#${0:x2}", "", SBC, Immediate, 2)); //A8
		Disasm.Add(new("sbc", "${0:x2},${1:x2}", "", SBC, DirectPage, 3)); //A9
		Disasm.Add(new("mov1", "c,${0:x4}.{1}", "", MOV1, AbsoluteBooleanBit, 3)); //AA
		Disasm.Add(new("inc", "${0:x2}", "", INC, DirectPage, 2)); //AB
		Disasm.Add(new("inc", "!${0:x4}", "", INC, Absolute, 3)); //AC
		Disasm.Add(new("cmp", "y,#${0:x2}", "", CMP, Immediate, 2)); //AD
		Disasm.Add(new("pop", "a", "", POP, Accumulator, 1)); //AE
		Disasm.Add(new("mov", "(x)+,a", "", MOV, Impliedtype1, 1)); //AF
		Disasm.Add(new("bcs", "${0:x4}", "", BCS, ProgramCounterRelative, 2)); //B0
		Disasm.Add(new("tcall", "11", "", TCALL, Impliedtype3, 1)); //B1
		Disasm.Add(new("clr1", "${0:x2}.5", "", CLR1, DirectPageBit, 2)); //B2
		Disasm.Add(new("bbc", "${0:x2}.5,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //B3
		Disasm.Add(new("sbc", "a,${0:x2}+x", "", SBC, DirectPageIndexedbyX, 2)); //B4
		Disasm.Add(new("sbc", "a,!${0:x4}+x", "", SBC, AbsoluteIndexedbyX, 3)); //B5
		Disasm.Add(new("sbc", "a,!${0:x4}+y", "", SBC, AbsoluteIndexedbyY, 3)); //B6
		Disasm.Add(new("sbc", "a,[${0:x2}]+y", "", SBC, DirectPageIndirectIndexedbyY, 2)); //B7
		Disasm.Add(new("sbc", "${0:x2},#${1:x2}", "", SBC, DirectPageImmediate, 3)); //B8
		Disasm.Add(new("sbc", "(x),(y)", "", SBC, ImpliedIndirecttype1, 1)); //B9
		Disasm.Add(new("movw", "ya,${0:x2}", "", MOVW, DirectPage, 2)); //BA
		Disasm.Add(new("inc", "${0:x2}+x", "", INC, DirectPageIndexedbyX, 2)); //BB
		Disasm.Add(new("inc", "a", "", INC, Accumulator, 1)); //BC
		Disasm.Add(new("mov", "sp,x", "", MOV, Impliedtype1, 1)); //BD
		Disasm.Add(new("das", "a", "", DAS, Accumulator, 1)); //BE
		Disasm.Add(new("mov", "a,(x)+", "", MOV, Impliedtype1, 1)); //BF
		Disasm.Add(new("di", "", "", DI, Impliedtype2, 1)); //C0
		Disasm.Add(new("tcall", "12", "", TCALL, Impliedtype3, 1)); //C1
		Disasm.Add(new("set1", "${0:x2}.6", "", SET1, DirectPageBit, 2)); //C2
		Disasm.Add(new("bbs", "${0:x2}.6,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //C3
		Disasm.Add(new("mov", "${0:x2},a", "", MOV, DirectPage, 2)); //C4
		Disasm.Add(new("mov", "!${0:x4},a", "", MOV, Absolute, 3)); //C5
		Disasm.Add(new("mov", "(x),a", "", MOV, Impliedtype1, 1)); //C6
		Disasm.Add(new("mov", "[${0:x2}+x],a", "", MOV, DirectPageIndexedIndirectbyX, 2)); //C7
		Disasm.Add(new("cmp", "x,#${0:x2}", "", CMP, Immediate, 2)); //C8
		Disasm.Add(new("mov", "!${0:x4},x", "", MOV, Absolute, 3)); //C9
		Disasm.Add(new("mov1", "${0:x4}.{1},c", "", MOV1, AbsoluteBooleanBit, 3)); //CA
		Disasm.Add(new("mov", "${0:x2},y", "", MOV, DirectPage, 2)); //CB
		Disasm.Add(new("mov", "!${0:x4},y", "", MOV, Absolute, 3)); //CC
		Disasm.Add(new("mov", "x,#${0:x2}", "", MOV, Immediate, 2)); //CD
		Disasm.Add(new("pop", "x", "", POP, StackX, 1)); //CE
		Disasm.Add(new("mul", "ya", "", MUL, Impliedtype1, 1)); //CF
		Disasm.Add(new("bne", "${0:x4}", "", BNE, ProgramCounterRelative, 2)); //D0
		Disasm.Add(new("tcall", "13", "", TCALL, Impliedtype3, 1)); //D1
		Disasm.Add(new("clr1", "${0:x2}.6", "", CLR1, DirectPageBit, 2)); //D2
		Disasm.Add(new("bbc", "${0:x2}.6,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //D3
		Disasm.Add(new("mov", "${0:x2}+x,a", "", MOV, DirectPageIndexedbyX, 2)); //D4
		Disasm.Add(new("mov", "!${0:x4}+x,a", "", MOV, AbsoluteIndexedbyX, 3)); //D5
		Disasm.Add(new("mov", "!${0:x4}+y,a", "", MOV, AbsoluteIndexedbyY, 3)); //D6
		Disasm.Add(new("mov", "[${0:x2}]+y,a", "", MOV, DirectPageIndirectIndexedbyY, 2)); //D7
		Disasm.Add(new("mov", "${0:x2},x", "", MOV, DirectPage, 2)); //D8
		Disasm.Add(new("mov", "${0:x2}+y,x", "", MOV, DirectPageIndexedbyY, 2)); //D9
		Disasm.Add(new("movw", "${0:x2},ya", "", MOVW, DirectPage, 2)); //DA
		Disasm.Add(new("mov", "${0:x2}+x,y", "", MOV, DirectPageIndexedbyX, 2)); //DB
		Disasm.Add(new("dec", "y", "", DEC, Impliedtype1, 1)); //DC
		Disasm.Add(new("mov", "a,y", "", MOV, Impliedtype1, 1)); //DD
		Disasm.Add(new("cbne", "${0:x2}+x,${1:x4}", "", CBNE, DirectPageIndexedbyXProgramCounterRelative, 3)); //DE
		Disasm.Add(new("daa", "a", "", DAA, Accumulator, 1)); //DF
		Disasm.Add(new("clrv", "", "", CLRV, Impliedtype2, 1)); //E0
		Disasm.Add(new("tcall", "14", "", TCALL, Impliedtype3, 1)); //E1
		Disasm.Add(new("set1", "${0:x2}.7", "", SET1, DirectPageBit, 2)); //E2
		Disasm.Add(new("bbs", "${0:x2}.7,${1:x4}", "", BBS, DirectPageBitRelative, 3)); //E3
		Disasm.Add(new("mov", "a,${0:x2}", "", MOV, DirectPage, 2)); //E4
		Disasm.Add(new("mov", "a,!${0:x4}", "", MOV, Absolute, 3)); //E5
		Disasm.Add(new("mov", "a,(x)", "", MOV, Impliedtype1, 1)); //E6
		Disasm.Add(new("mov", "a,[${0:x2}+x]", "", MOV, DirectPageIndexedIndirectbyX, 2)); //E7
		Disasm.Add(new("mov", "a,#${0:x2}", "", MOV, Immediate, 2)); //E8
		Disasm.Add(new("mov", "x,!${0:x4}", "", MOV, Absolute, 3)); //E9
		Disasm.Add(new("not1", "${0:x4}.{1}", "", NOT1, AbsoluteBooleanBit, 3)); //EA
		Disasm.Add(new("mov", "y,${0:x2}", "", MOV, DirectPage, 2)); //EB
		Disasm.Add(new("mov", "y,!${0:x4}", "", MOV, Absolute, 3)); //EC
		Disasm.Add(new("notc", "", "", NOTC, Impliedtype2, 1)); //ED
		Disasm.Add(new("pop", "y", "", POP, StackY, 1)); //EE
		Disasm.Add(new("sleep", "", "", SLEEP, Impliedtype3, 1)); //EF
		Disasm.Add(new("beq", "${0:x4}", "", BEQ, ProgramCounterRelative, 2)); //F0
		Disasm.Add(new("tcall", "15", "", TCALL, Impliedtype3, 1)); //F1
		Disasm.Add(new("clr1", "${0:x2}.7", "", CLR1, DirectPageBit, 2)); //F2
		Disasm.Add(new("bbc", "${0:x2}.7,${1:x4}", "", BBC, DirectPageBitRelative, 3)); //F3
		Disasm.Add(new("mov", "a,${0:x2}+x", "", MOV, DirectPageIndexedbyX, 2)); //F4
		Disasm.Add(new("mov", "a,!${0:x4}+x", "", MOV, AbsoluteIndexedbyX, 3)); //F5
		Disasm.Add(new("mov", "a,!${0:x4}+y", "", MOV, AbsoluteIndexedbyY, 3)); //F6
		Disasm.Add(new("mov", "a,[${0:x2}]+y", "", MOV, DirectPageIndirectIndexedbyY, 2)); //F7
		Disasm.Add(new("mov", "x,${0:x2}", "", MOV, DirectPage, 2)); //F8
		Disasm.Add(new("mov", "x,${0:x2}+y", "", MOV, DirectPageIndexedbyY, 2)); //F9
		Disasm.Add(new("mov", "${0:x2},${1:x2}", "", MOV, DirectPage, 3)); //FA
		Disasm.Add(new("mov", "y,${0:x2}+x", "", MOV, DirectPageIndexedbyX, 2)); //FB
		Disasm.Add(new("inc", "y", "", INC, Impliedtype1, 1)); //FC
		Disasm.Add(new("mov", "y,a", "", MOV, Impliedtype1, 1)); //FD
		Disasm.Add(new("dbnz", "y,${1:x4}", "", DBNZ, Impliedtype1ProgramCounterRelative, 2)); //FE
		Disasm.Add(new("stop", "", "", STOP, Impliedtype3, 1)); //FF
	}

	//Opcodes
	public const int ADC = 0x00;
	public const int ADDW = 0x01;
	public const int AND = 0x02;
	public const int AND1 = 0x03;
	public const int ASL = 0x04;
	public const int BBC = 0x05;
	public const int BBS = 0x06;
	public const int BCC = 0x07;
	public const int BCS = 0x08;
	public const int BEQ = 0x09;
	public const int BMI = 0x0A;
	public const int BNE = 0x0B;
	public const int BPL = 0x0C;
	public const int BRA = 0x0D;
	public const int BRK = 0x0E;
	public const int BVC = 0x0F;
	public const int BVS = 0x10;
	public const int CALL = 0x11;
	public const int CBNE = 0x12;
	public const int CLR1 = 0x13;
	public const int CLRC = 0x14;
	public const int CLRP = 0x15;
	public const int CLRV = 0x16;
	public const int CMP = 0x17;
	public const int CMPW = 0x18;
	public const int DAA = 0x19;
	public const int DAS = 0x1A;
	public const int DBNZ = 0x1B;
	public const int DEC = 0x1C;
	public const int DECW = 0x1D;
	public const int DI = 0x1E;
	public const int DIV = 0x1F;
	public const int EI = 0x20;
	public const int EOR = 0x21;
	public const int EOR1 = 0x22;
	public const int INC = 0x23;
	public const int INCW = 0x24;
	public const int JMP = 0x25;
	public const int LSR = 0x26;
	public const int MOV = 0x27;
	public const int MOV1 = 0x28;
	public const int MOVW = 0x29;
	public const int MUL = 0x2A;
	public const int NOP = 0x2B;
	public const int NOT1 = 0x2C;
	public const int NOTC = 0x2D;
	public const int OR = 0x2E;
	public const int OR1 = 0x2F;
	public const int PCALL = 0x30;
	public const int POP = 0x31;
	public const int PUSH = 0x32;
	public const int RET = 0x33;
	public const int RETI = 0x34;
	public const int ROL = 0x35;
	public const int ROR = 0x36;
	public const int SBC = 0x37;
	public const int SET1 = 0x38;
	public const int SETC = 0x39;
	public const int SETP = 0x3A;
	public const int SLEEP = 0x3B;
	public const int STOP = 0x3C;
	public const int SUBW = 0x3D;
	public const int TCALL = 0x3E;
	public const int TCLR1 = 0x3F;
	public const int TSET1 = 0x40;
	public const int XCN = 0x41;

	//AddrMode
	public const int Absolute = 0x00;
	public const int AbsoluteBooleanBit = 0x01;
	public const int AbsoluteIndexedbyX = 0x02;
	public const int AbsoluteIndexedbyY = 0x03;
	public const int Accumulator = 0x04;
	public const int DirectPage = 0x05;
	public const int DirectPageBit = 0x06;
	public const int DirectPageBitRelative = 0x07;
	public const int DirectPageImmediate = 0x08;
	public const int DirectPageIndexedIndirectbyX = 0x09;
	public const int DirectPageIndexedbyX = 0x0A;
	public const int DirectPageIndexedbyXProgramCounterRelative = 0x0B;
	public const int DirectPageIndexedbyY = 0x0C;
	public const int DirectPageIndirectIndexedbyY = 0x0D;
	public const int DirectPageProgramCounterRelative = 0x0E;
	public const int Immediate = 0x0F;
	public const int ImpliedIndirecttype1 = 0x10;
	public const int ImpliedIndirecttype2 = 0x11;
	public const int Impliedtype1 = 0x12;
	public const int Impliedtype1ProgramCounterRelative = 0x13;
	public const int Impliedtype2 = 0x14;
	public const int Impliedtype3 = 0x15;
	public const int ProgramCounterRelative = 0x16;
	public const int StackInterrupt = 0x17;
	public const int StackPSW = 0x18;
	public const int StackR = 0x19;
	public const int StackX = 0x1A;
	public const int StackY = 0x1B;
	public const int UppermostPage = 0x1C;
	}