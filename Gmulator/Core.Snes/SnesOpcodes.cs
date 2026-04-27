using System.Text.Json.Serialization;

namespace Gmulator.Core.Snes;
public partial class SnesCpu
{
	public struct Opcode(string name, string oper, int id, int mode, bool imm, int size)
    {
        public string Name = name;
        public string Oper = oper;
        public int Id = id;
        public int Mode = mode;     
        public bool Immediate = imm;
        public int Size = size;
    };

	public List<Opcode> Disasm = new();

	public void CreateOpcodes() 
	{
		Disasm.Add(new("brk", "", BRK, StackInterrupt, false, 2)); //00
		Disasm.Add(new("ora", "(${0:x2},x)", ORA, DPIndexedIndirectX, false, 2)); //01
		Disasm.Add(new("cop", "#${0:x2}", COP, StackInterrupt, false, 2)); //02
		Disasm.Add(new("ora", "sr,s", ORA, StackRelative, false, 2)); //03
		Disasm.Add(new("tsb", "${0:x2}", TSB, DirectPage, false, 2)); //04
		Disasm.Add(new("ora", "${0:x2}", ORA, DirectPage, false, 2)); //05
		Disasm.Add(new("asl", "${0:x2}", ASL, DirectPage, false, 2)); //06
		Disasm.Add(new("ora", "[${0:x2}]", ORA, DPIndirectLong, false, 2)); //07
		Disasm.Add(new("php", "", PHP, StackPush, false, 1)); //08
		Disasm.Add(new("ora", "#${0:x2}", ORA, ImmediateMemory, true, 2)); //09
		Disasm.Add(new("asl", "a", ASL, Accumulator, false, 1)); //0A
		Disasm.Add(new("phd", "", PHD, StackPush, false, 1)); //0B
		Disasm.Add(new("tsb", "${0:x4}", TSB, Absolute, false, 3)); //0C
		Disasm.Add(new("ora", "${0:x4}", ORA, Absolute, false, 3)); //0D
		Disasm.Add(new("asl", "${0:x4}", ASL, Absolute, false, 3)); //0E
		Disasm.Add(new("ora", "${0:x6}", ORA, AbsoluteLong, false, 4)); //0F
		Disasm.Add(new("bpl", "${0:x4}", BPL, ProgramCounterRelative, false, 2)); //10
		Disasm.Add(new("ora", "(${0:x2}),y", ORA, DPIndirectIndexedY, false, 2)); //11
		Disasm.Add(new("ora", "(${0:x2})", ORA, DPIndirect, false, 2)); //12
		Disasm.Add(new("ora", "(sr,s),y", ORA, SRIndirectIndexedY, false, 2)); //13
		Disasm.Add(new("trb", "${0:x2}", TRB, DirectPage, false, 2)); //14
		Disasm.Add(new("ora", "${0:x2},x", ORA, DPIndexedX, false, 2)); //15
		Disasm.Add(new("asl", "${0:x2},x", ASL, DPIndexedX, false, 2)); //16
		Disasm.Add(new("ora", "[${0:x2}],y", ORA, DPIndirectLongIndexedY, false, 2)); //17
		Disasm.Add(new("clc", "", CLC, Implied, false, 1)); //18
		Disasm.Add(new("ora", "${0:x4},y", ORA, AbsoluteIndexedY, false, 3)); //19
		Disasm.Add(new("inc", "a", INC, Accumulator, false, 1)); //1A
		Disasm.Add(new("tcs", "", TCS, Implied, false, 1)); //1B
		Disasm.Add(new("trb", "${0:x4}", TRB, Absolute, false, 3)); //1C
		Disasm.Add(new("ora", "${0:x4},x", ORA, AbsoluteIndexedX, false, 3)); //1D
		Disasm.Add(new("asl", "${0:x4},x", ASL, AbsoluteIndexedX, false, 3)); //1E
		Disasm.Add(new("ora", " ${0:x6},x", ORA, AbsoluteLongIndexedX, false, 4)); //1F
		Disasm.Add(new("jsr", "${0:x4}", JSR, Absolute, false, 3)); //20
		Disasm.Add(new("and", "(${0:x2},x)", AND, DPIndexedIndirectX, false, 2)); //21
		Disasm.Add(new("jsl", "${0:x6}", JSL, AbsoluteLong, false, 4)); //22
		Disasm.Add(new("and", "sr,s", AND, StackRelative, false, 2)); //23
		Disasm.Add(new("bit", "${0:x2}", BIT, DirectPage, false, 2)); //24
		Disasm.Add(new("and", "${0:x2}", AND, DirectPage, false, 2)); //25
		Disasm.Add(new("rol", "${0:x2}", ROL, DirectPage, false, 2)); //26
		Disasm.Add(new("and", "[${0:x2}]", AND, DPIndirectLong, false, 2)); //27
		Disasm.Add(new("plp", "", PLP, StackPull, false, 1)); //28
		Disasm.Add(new("and", "#${0:x2}", AND, ImmediateMemory, true, 2)); //29
		Disasm.Add(new("rol", "a", ROL, Accumulator, false, 1)); //2A
		Disasm.Add(new("pld", "", PLD, StackPull, false, 1)); //2B
		Disasm.Add(new("bit", "${0:x4}", BIT, Absolute, false, 3)); //2C
		Disasm.Add(new("and", "${0:x4}", AND, Absolute, false, 3)); //2D
		Disasm.Add(new("rol", "${0:x4}", ROL, Absolute, false, 3)); //2E
		Disasm.Add(new("and", "${0:x6}", AND, AbsoluteLong, false, 4)); //2F
		Disasm.Add(new("bmi", "${0:x4}", BMI, ProgramCounterRelative, false, 2)); //30
		Disasm.Add(new("and", "(${0:x2}),y", AND, DPIndirectIndexedY, false, 2)); //31
		Disasm.Add(new("and", "(${0:x2})", AND, DPIndirect, false, 2)); //32
		Disasm.Add(new("and", "(sr,s),y", AND, SRIndirectIndexedY, false, 2)); //33
		Disasm.Add(new("bit", "${0:x2},x", BIT, DPIndexedX, false, 2)); //34
		Disasm.Add(new("and", "${0:x2},x", AND, DPIndexedX, false, 2)); //35
		Disasm.Add(new("rol", "${0:x2},x", ROL, DPIndexedX, false, 2)); //36
		Disasm.Add(new("and", "[${0:x2}],y", AND, DPIndirectLongIndexedY, false, 2)); //37
		Disasm.Add(new("sec", "", SEC, Implied, false, 1)); //38
		Disasm.Add(new("and", "${0:x4},y", AND, AbsoluteIndexedY, false, 3)); //39
		Disasm.Add(new("dec", "a", DEC, Accumulator, false, 1)); //3A
		Disasm.Add(new("tsc", "", TSC, Implied, false, 1)); //3B
		Disasm.Add(new("bit", "${0:x4},x", BIT, AbsoluteIndexedX, false, 3)); //3C
		Disasm.Add(new("and", "${0:x4},x", AND, AbsoluteIndexedX, false, 3)); //3D
		Disasm.Add(new("rol", "${0:x4},x", ROL, AbsoluteIndexedX, false, 3)); //3E
		Disasm.Add(new("and", " ${0:x6},x", AND, AbsoluteLongIndexedX, false, 4)); //3F
		Disasm.Add(new("rti", "", RTI, StackRTI, false, 1)); //40
		Disasm.Add(new("eor", "(${0:x2},x)", EOR, DPIndexedIndirectX, false, 2)); //41
		Disasm.Add(new("wdm", "", WDM, NoMode, false, 2)); //42
		Disasm.Add(new("eor", "sr,s", EOR, StackRelative, false, 2)); //43
		Disasm.Add(new("mvp", "srcbk,destbk", MVP, BlockMove, false, 3)); //44
		Disasm.Add(new("eor", "${0:x2}", EOR, DirectPage, false, 2)); //45
		Disasm.Add(new("lsr", "${0:x2}", LSR, DirectPage, false, 2)); //46
		Disasm.Add(new("eor", "[${0:x2}]", EOR, DPIndirectLong, false, 2)); //47
		Disasm.Add(new("pha", "", PHA, StackPush, false, 1)); //48
		Disasm.Add(new("eor", "#${0:x2}", EOR, ImmediateMemory, true, 2)); //49
		Disasm.Add(new("lsr", "a", LSR, Accumulator, false, 1)); //4A
		Disasm.Add(new("phk", "", PHK, StackPush, false, 1)); //4B
		Disasm.Add(new("jmp", "${0:x4}", JMP, Absolute, false, 3)); //4C
		Disasm.Add(new("eor", "${0:x4}", EOR, Absolute, false, 3)); //4D
		Disasm.Add(new("lsr", "${0:x4}", LSR, Absolute, false, 3)); //4E
		Disasm.Add(new("eor", "${0:x6}", EOR, AbsoluteLong, false, 4)); //4F
		Disasm.Add(new("bvc", "${0:x4}", BVC, ProgramCounterRelative, false, 2)); //50
		Disasm.Add(new("eor", "(${0:x2}),y", EOR, DPIndirectIndexedY, false, 2)); //51
		Disasm.Add(new("eor", "(${0:x2})", EOR, DPIndirect, false, 2)); //52
		Disasm.Add(new("eor", "(sr,s),y", EOR, SRIndirectIndexedY, false, 2)); //53
		Disasm.Add(new("mvn", "srcbk,destbk", MVN, BlockMove, false, 3)); //54
		Disasm.Add(new("eor", "${0:x2},x", EOR, DPIndexedX, false, 2)); //55
		Disasm.Add(new("lsr", "${0:x2},x", LSR, DPIndexedX, false, 2)); //56
		Disasm.Add(new("eor", "[${0:x2}],y", EOR, DPIndirectLongIndexedY, false, 2)); //57
		Disasm.Add(new("cli", "", CLI, Implied, false, 1)); //58
		Disasm.Add(new("eor", "${0:x4},y", EOR, AbsoluteIndexedY, false, 3)); //59
		Disasm.Add(new("phy", "", PHY, StackPush, false, 1)); //5A
		Disasm.Add(new("tcd", "", TCD, Implied, false, 1)); //5B
		Disasm.Add(new("jml", "${0:x6}", JML, AbsoluteLong, false, 4)); //5C
		Disasm.Add(new("eor", "${0:x4},x", EOR, AbsoluteIndexedX, false, 3)); //5D
		Disasm.Add(new("lsr", "${0:x4},x", LSR, AbsoluteIndexedX, false, 3)); //5E
		Disasm.Add(new("eor", " ${0:x6},x", EOR, AbsoluteLongIndexedX, false, 4)); //5F
		Disasm.Add(new("rts", "", RTS, StackRTS, false, 1)); //60
		Disasm.Add(new("adc", "(${0:x2},x)", ADC, DPIndexedIndirectX, false, 2)); //61
		Disasm.Add(new("per", "label", PER, StackPCRelativeLong, false, 3)); //62
		Disasm.Add(new("adc", "sr,s", ADC, StackRelative, false, 2)); //63
		Disasm.Add(new("stz", "${0:x2}", STZ, DirectPage, false, 2)); //64
		Disasm.Add(new("adc", "${0:x2}", ADC, DirectPage, false, 2)); //65
		Disasm.Add(new("ror", "${0:x2}", ROR, DirectPage, false, 2)); //66
		Disasm.Add(new("adc", "[${0:x2}]", ADC, DPIndirectLong, false, 2)); //67
		Disasm.Add(new("pla", "", PLA, StackPull, false, 1)); //68
		Disasm.Add(new("adc", "#${0:x2}", ADC, ImmediateMemory, true, 2)); //69
		Disasm.Add(new("ror", "a", ROR, Accumulator, false, 1)); //6A
		Disasm.Add(new("rtl", "", RTL, StackRTL, false, 1)); //6B
		Disasm.Add(new("jmp", "(${0:x4})", JMP, AbsoluteIndirect, false, 3)); //6C
		Disasm.Add(new("adc", "${0:x4}", ADC, Absolute, false, 3)); //6D
		Disasm.Add(new("ror", "${0:x4}", ROR, Absolute, false, 3)); //6E
		Disasm.Add(new("adc", "${0:x6}", ADC, AbsoluteLong, false, 4)); //6F
		Disasm.Add(new("bvs", "${0:x4}", BVS, ProgramCounterRelative, false, 2)); //70
		Disasm.Add(new("adc", "( ${0:x2}),y", ADC, DPIndirectIndexedY, false, 2)); //71
		Disasm.Add(new("adc", "(${0:x2})", ADC, DPIndirect, false, 2)); //72
		Disasm.Add(new("adc", "(sr,s),y", ADC, SRIndirectIndexedY, false, 2)); //73
		Disasm.Add(new("stz", "${0:x2},x", STZ, DPIndexedX, false, 2)); //74
		Disasm.Add(new("adc", "${0:x2},x", ADC, DPIndexedX, false, 2)); //75
		Disasm.Add(new("ror", "${0:x2},x", ROR, DPIndexedX, false, 2)); //76
		Disasm.Add(new("adc", "[${0:x2}],y", ADC, DPIndirectLongIndexedY, false, 2)); //77
		Disasm.Add(new("sei", "", SEI, Implied, false, 1)); //78
		Disasm.Add(new("adc", "${0:x4},y", ADC, AbsoluteIndexedY, false, 3)); //79
		Disasm.Add(new("ply", "", PLY, StackPull, false, 1)); //7A
		Disasm.Add(new("tdc", "", TDC, Implied, false, 1)); //7B
		Disasm.Add(new("jmp", "(${0:x4},x)", JMP, AbsoluteIndexedIndirect, false, 3)); //7C
		Disasm.Add(new("adc", "${0:x4},x", ADC, AbsoluteIndexedX, false, 3)); //7D
		Disasm.Add(new("ror", "${0:x4},x", ROR, AbsoluteIndexedX, false, 3)); //7E
		Disasm.Add(new("adc", " ${0:x6},x", ADC, AbsoluteLongIndexedX, false, 4)); //7F
		Disasm.Add(new("bra", "${0:x4}", BRA, ProgramCounterRelative, false, 2)); //80
		Disasm.Add(new("sta", "(${0:x2},x)", STA, DPIndexedIndirectX, false, 2)); //81
		Disasm.Add(new("brl", "label", BRL, ProgramCounterRelativeLong, false, 3)); //82
		Disasm.Add(new("sta", "sr,s", STA, StackRelative, false, 2)); //83
		Disasm.Add(new("sty", "${0:x2}", STY, DirectPage, false, 2)); //84
		Disasm.Add(new("sta", "${0:x2}", STA, DirectPage, false, 2)); //85
		Disasm.Add(new("stx", "${0:x2}", STX, DirectPage, false, 2)); //86
		Disasm.Add(new("sta", "[${0:x2}]", STA, DPIndirectLong, false, 2)); //87
		Disasm.Add(new("dey", "", DEY, Implied, false, 1)); //88
		Disasm.Add(new("bit", "#${0:x2}", BIT, ImmediateMemory, true, 2)); //89
		Disasm.Add(new("txa", "", TXA, Implied, false, 1)); //8A
		Disasm.Add(new("phb", "", PHB, StackPush, false, 1)); //8B
		Disasm.Add(new("sty", "${0:x4}", STY, Absolute, false, 3)); //8C
		Disasm.Add(new("sta", "${0:x4}", STA, Absolute, false, 3)); //8D
		Disasm.Add(new("stx", "${0:x4}", STX, Absolute, false, 3)); //8E
		Disasm.Add(new("sta", "${0:x6}", STA, AbsoluteLong, false, 4)); //8F
		Disasm.Add(new("bcc", "${0:x4}", BCC, ProgramCounterRelative, false, 2)); //90
		Disasm.Add(new("sta", "(${0:x2}),y", STA, DPIndirectIndexedY, false, 2)); //91
		Disasm.Add(new("sta", "(${0:x2})", STA, DPIndirect, false, 2)); //92
		Disasm.Add(new("sta", "(sr,s),y", STA, SRIndirectIndexedY, false, 2)); //93
		Disasm.Add(new("sty", "${0:x2},x", STY, DPIndexedX, false, 2)); //94
		Disasm.Add(new("sta", "_${0:x2}_x", STA, DPIndexedX, false, 2)); //95
		Disasm.Add(new("stx", "${0:x2},y", STX, DPIndexedY, false, 2)); //96
		Disasm.Add(new("sta", "[${0:x2}],y", STA, DPIndirectLongIndexedY, false, 2)); //97
		Disasm.Add(new("tya", "", TYA, Implied, false, 1)); //98
		Disasm.Add(new("sta", "${0:x4},y", STA, AbsoluteIndexedY, false, 3)); //99
		Disasm.Add(new("txs", "", TXS, Implied, false, 1)); //9A
		Disasm.Add(new("txy", "", TXY, Implied, false, 1)); //9B
		Disasm.Add(new("stz", "${0:x4}", STZ, Absolute, false, 3)); //9C
		Disasm.Add(new("sta", "${0:x4},x", STA, AbsoluteIndexedX, false, 3)); //9D
		Disasm.Add(new("stz", "${0:x4},x", STZ, AbsoluteIndexedX, false, 3)); //9E
		Disasm.Add(new("sta", " ${0:x6},x", STA, AbsoluteLongIndexedX, false, 4)); //9F
		Disasm.Add(new("ldy", "#${0:x2}", LDY, ImmediateIndex, true, 2)); //A0
		Disasm.Add(new("lda", "(${0:x2},x)", LDA, DPIndexedIndirectX, false, 2)); //A1
		Disasm.Add(new("ldx", "#${0:x2}", LDX, ImmediateIndex, true, 2)); //A2
		Disasm.Add(new("lda", "sr,s", LDA, StackRelative, false, 2)); //A3
		Disasm.Add(new("ldy", "${0:x2}", LDY, DirectPage, false, 2)); //A4
		Disasm.Add(new("lda", "${0:x2}", LDA, DirectPage, false, 2)); //A5
		Disasm.Add(new("ldx", "${0:x2}", LDX, DirectPage, false, 2)); //A6
		Disasm.Add(new("lda", "[${0:x2}]", LDA, DPIndirectLong, false, 2)); //A7
		Disasm.Add(new("tay", "", TAY, Implied, false, 1)); //A8
		Disasm.Add(new("lda", "#${0:x2}", LDA, ImmediateMemory, true, 2)); //A9
		Disasm.Add(new("tax", "", TAX, Implied, false, 1)); //AA
		Disasm.Add(new("plb", "", PLB, StackPull, false, 1)); //AB
		Disasm.Add(new("ldy", "${0:x4}", LDY, Absolute, false, 3)); //AC
		Disasm.Add(new("lda", "${0:x4}", LDA, Absolute, false, 3)); //AD
		Disasm.Add(new("ldx", "${0:x4}", LDX, Absolute, false, 3)); //AE
		Disasm.Add(new("lda", "${0:x6}", LDA, AbsoluteLong, false, 4)); //AF
		Disasm.Add(new("bcs", "${0:x4}", BCS, ProgramCounterRelative, false, 2)); //B0
		Disasm.Add(new("lda", "(${0:x2}),y", LDA, DPIndirectIndexedY, false, 2)); //B1
		Disasm.Add(new("lda", "(${0:x2})", LDA, DPIndirect, false, 2)); //B2
		Disasm.Add(new("lda", "(sr,s),y", LDA, SRIndirectIndexedY, false, 2)); //B3
		Disasm.Add(new("ldy", "${0:x2},x", LDY, DPIndexedX, false, 2)); //B4
		Disasm.Add(new("lda", "${0:x2},x", LDA, DPIndexedX, false, 2)); //B5
		Disasm.Add(new("ldx", "${0:x2},y", LDX, DPIndexedY, false, 2)); //B6
		Disasm.Add(new("lda", "[${0:x2}],y", LDA, DPIndirectLongIndexedY, false, 2)); //B7
		Disasm.Add(new("clv", "", CLV, Implied, false, 1)); //B8
		Disasm.Add(new("lda", "${0:x4},y", LDA, AbsoluteIndexedY, false, 3)); //B9
		Disasm.Add(new("tsx", "", TSX, Implied, false, 1)); //BA
		Disasm.Add(new("tyx", "", TYX, Implied, false, 1)); //BB
		Disasm.Add(new("ldy", "${0:x4},x", LDY, AbsoluteIndexedX, false, 3)); //BC
		Disasm.Add(new("lda", "${0:x4},x", LDA, AbsoluteIndexedX, false, 3)); //BD
		Disasm.Add(new("ldx", "${0:x4},y", LDX, AbsoluteIndexedY, false, 3)); //BE
		Disasm.Add(new("lda", " ${0:x6},x", LDA, AbsoluteLongIndexedX, false, 4)); //BF
		Disasm.Add(new("cpy", "#${0:x2}", CPY, ImmediateIndex, true, 2)); //C0
		Disasm.Add(new("cmp", "(${0:x2},x)", CMP, DPIndexedIndirectX, false, 2)); //C1
		Disasm.Add(new("rep", "#${0:x2}", REP, Immediate, true, 2)); //C2
		Disasm.Add(new("cmp", "sr,s", CMP, StackRelative, false, 2)); //C3
		Disasm.Add(new("cpy", "${0:x2}", CPY, DirectPage, false, 2)); //C4
		Disasm.Add(new("cmp", "${0:x2}", CMP, DirectPage, false, 2)); //C5
		Disasm.Add(new("dec", "${0:x2}", DEC, DirectPage, false, 2)); //C6
		Disasm.Add(new("cmp", "[${0:x2}]", CMP, DPIndirectLong, false, 2)); //C7
		Disasm.Add(new("iny", "", INY, Implied, false, 1)); //C8
		Disasm.Add(new("cmp", "#${0:x2}", CMP, ImmediateMemory, true, 2)); //C9
		Disasm.Add(new("dex", "", DEX, Implied, false, 1)); //CA
		Disasm.Add(new("wai", "", WAI, Implied, false, 1)); //CB
		Disasm.Add(new("cpy", "${0:x4}", CPY, Absolute, false, 3)); //CC
		Disasm.Add(new("cmp", "${0:x4}", CMP, Absolute, false, 3)); //CD
		Disasm.Add(new("dec", "${0:x4}", DEC, Absolute, false, 3)); //CE
		Disasm.Add(new("cmp", "${0:x6}", CMP, AbsoluteLong, false, 4)); //CF
		Disasm.Add(new("bne", "${0:x4}", BNE, ProgramCounterRelative, false, 2)); //D0
		Disasm.Add(new("cmp", "(${0:x2}),y", CMP, DPIndirectIndexedY, false, 2)); //D1
		Disasm.Add(new("cmp", "(${0:x2})", CMP, DPIndirect, false, 2)); //D2
		Disasm.Add(new("cmp", "(sr,s),y", CMP, SRIndirectIndexedY, false, 2)); //D3
		Disasm.Add(new("pei", "(${0:x2})", PEI, StackDPIndirect, false, 2)); //D4
		Disasm.Add(new("cmp", "${0:x2},x", CMP, DPIndexedX, false, 2)); //D5
		Disasm.Add(new("dec", "${0:x2},x", DEC, DPIndexedX, false, 2)); //D6
		Disasm.Add(new("cmp", "[${0:x2}],y", CMP, DPIndirectLongIndexedY, false, 2)); //D7
		Disasm.Add(new("cld", "", CLD, Implied, false, 1)); //D8
		Disasm.Add(new("cmp", "${0:x4},y", CMP, AbsoluteIndexedY, false, 3)); //D9
		Disasm.Add(new("phx", "", PHX, StackPush, false, 1)); //DA
		Disasm.Add(new("stp", "", STP, Implied, false, 1)); //DB
		Disasm.Add(new("jmp", "[${0:x4}]", JMP, AbsoluteIndirectLong, false, 3)); //DC
		Disasm.Add(new("cmp", "${0:x4},x", CMP, AbsoluteIndexedX, false, 3)); //DD
		Disasm.Add(new("dec", "${0:x4},x", DEC, AbsoluteIndexedX, false, 3)); //DE
		Disasm.Add(new("cmp", " ${0:x6},x", CMP, AbsoluteLongIndexedX, false, 4)); //DF
		Disasm.Add(new("cpx", "#${0:x2}", CPX, ImmediateIndex, true, 2)); //E0
		Disasm.Add(new("sbc", "(${0:x2},x)", SBC, DPIndexedIndirectX, false, 2)); //E1
		Disasm.Add(new("sep", "#${0:x2}", SEP, Immediate, true, 2)); //E2
		Disasm.Add(new("sbc", "sr,s", SBC, StackRelative, false, 2)); //E3
		Disasm.Add(new("cpx", "${0:x2}", CPX, DirectPage, false, 2)); //E4
		Disasm.Add(new("sbc", "${0:x2}", SBC, DirectPage, false, 2)); //E5
		Disasm.Add(new("inc", "${0:x2}", INC, DirectPage, false, 2)); //E6
		Disasm.Add(new("sbc", "[${0:x2}]", SBC, DPIndirectLong, false, 2)); //E7
		Disasm.Add(new("inx", "", INX, Implied, false, 1)); //E8
		Disasm.Add(new("sbc", "#${0:x2}", SBC, ImmediateMemory, true, 2)); //E9
		Disasm.Add(new("nop", "", NOP, Implied, false, 1)); //EA
		Disasm.Add(new("xba", "", XBA, Implied, false, 1)); //EB
		Disasm.Add(new("cpx", "${0:x4}", CPX, Absolute, false, 3)); //EC
		Disasm.Add(new("sbc", "${0:x4}", SBC, Absolute, false, 3)); //ED
		Disasm.Add(new("inc", "${0:x4}", INC, Absolute, false, 3)); //EE
		Disasm.Add(new("sbc", "${0:x6}", SBC, AbsoluteLong, false, 4)); //EF
		Disasm.Add(new("beq", "${0:x4}", BEQ, ProgramCounterRelative, false, 2)); //F0
		Disasm.Add(new("sbc", "(${0:x2}),y", SBC, DPIndirectIndexedY, false, 2)); //F1
		Disasm.Add(new("sbc", "(${0:x2})", SBC, DPIndirect, false, 2)); //F2
		Disasm.Add(new("sbc", "(sr,s),y", SBC, SRIndirectIndexedY, false, 2)); //F3
		Disasm.Add(new("pea", "${0:x4}", PEA, StackAbsolute, false, 3)); //F4
		Disasm.Add(new("sbc", "${0:x2},x", SBC, DPIndexedX, false, 2)); //F5
		Disasm.Add(new("inc", "${0:x2},x", INC, DPIndexedX, false, 2)); //F6
		Disasm.Add(new("sbc", "[${0:x2}],y", SBC, DPIndirectLongIndexedY, false, 2)); //F7
		Disasm.Add(new("sed", "", SED, Implied, false, 1)); //F8
		Disasm.Add(new("sbc", "${0:x4},y", SBC, AbsoluteIndexedY, false, 3)); //F9
		Disasm.Add(new("plx", "", PLX, StackPull, false, 1)); //FA
		Disasm.Add(new("xce", "", XCE, Implied, false, 1)); //FB
		Disasm.Add(new("jsr", "(${0:x4},x))", JSR, AbsoluteIndexedIndirect, false, 3)); //FC
		Disasm.Add(new("sbc", "${0:x4},x", SBC, AbsoluteIndexedX, false, 3)); //FD
		Disasm.Add(new("inc", "${0:x4},x", INC, AbsoluteIndexedX, false, 3)); //FE
		Disasm.Add(new("sbc", " ${0:x6},x", SBC, AbsoluteLongIndexedX, false, 4)); //FF
	}

	//Opcodes
	public const int ADC = 0x00;
	public const int AND = 0x01;
	public const int ASL = 0x02;
	public const int BCC = 0x03;
	public const int BCS = 0x04;
	public const int BEQ = 0x05;
	public const int BIT = 0x06;
	public const int BMI = 0x07;
	public const int BNE = 0x08;
	public const int BPL = 0x09;
	public const int BRA = 0x0A;
	public const int BRK = 0x0B;
	public const int BRL = 0x0C;
	public const int BVC = 0x0D;
	public const int BVS = 0x0E;
	public const int CLC = 0x0F;
	public const int CLD = 0x10;
	public const int CLI = 0x11;
	public const int CLV = 0x12;
	public const int CMP = 0x13;
	public const int COP = 0x14;
	public const int CPX = 0x15;
	public const int CPY = 0x16;
	public const int DEC = 0x17;
	public const int DEX = 0x18;
	public const int DEY = 0x19;
	public const int EOR = 0x1A;
	public const int INC = 0x1B;
	public const int INX = 0x1C;
	public const int INY = 0x1D;
	public const int JMP = 0x1E;
	public const int JML = 0x1F;
	public const int JSR = 0x20;
	public const int JSL = 0x21;
	public const int LDA = 0x22;
	public const int LDX = 0x23;
	public const int LDY = 0x24;
	public const int LSR = 0x25;
	public const int MVN = 0x26;
	public const int MVP = 0x27;
	public const int NOP = 0x28;
	public const int ORA = 0x29;
	public const int PEA = 0x2A;
	public const int PEI = 0x2B;
	public const int PER = 0x2C;
	public const int PHA = 0x2D;
	public const int PHB = 0x2E;
	public const int PHD = 0x2F;
	public const int PHK = 0x30;
	public const int PHP = 0x31;
	public const int PHX = 0x32;
	public const int PHY = 0x33;
	public const int PLA = 0x34;
	public const int PLB = 0x35;
	public const int PLD = 0x36;
	public const int PLP = 0x37;
	public const int PLX = 0x38;
	public const int PLY = 0x39;
	public const int REP = 0x3A;
	public const int ROL = 0x3B;
	public const int ROR = 0x3C;
	public const int RTI = 0x3D;
	public const int RTL = 0x3E;
	public const int RTS = 0x3F;
	public const int SBC = 0x40;
	public const int SEC = 0x41;
	public const int SED = 0x42;
	public const int SEI = 0x43;
	public const int SEP = 0x44;
	public const int STA = 0x45;
	public const int STP = 0x46;
	public const int STX = 0x47;
	public const int STY = 0x48;
	public const int STZ = 0x49;
	public const int TAX = 0x4A;
	public const int TAY = 0x4B;
	public const int TCD = 0x4C;
	public const int TCS = 0x4D;
	public const int TDC = 0x4E;
	public const int TRB = 0x4F;
	public const int TSB = 0x50;
	public const int TSC = 0x51;
	public const int TSX = 0x52;
	public const int TXA = 0x53;
	public const int TXS = 0x54;
	public const int TXY = 0x55;
	public const int TYA = 0x56;
	public const int TYX = 0x57;
	public const int WAI = 0x58;
	public const int WDM = 0x59;
	public const int XBA = 0x5A;
	public const int XCE = 0x5B;

	//AddrMode
	public const int ImmediateMemory = 0x00;
	public const int ImmediateIndex = 0x01;
	public const int DPIndexedIndirectX = 0x02;
	public const int StackRelative = 0x03;
	public const int DirectPage = 0x04;
	public const int DPIndirectLong = 0x05;
	public const int Absolute = 0x06;
	public const int AbsoluteLong = 0x07;
	public const int DPIndirectIndexedY = 0x08;
	public const int DPIndirect = 0x09;
	public const int SRIndirectIndexedY = 0x0A;
	public const int DPIndexedX = 0x0B;
	public const int DPIndirectLongIndexedY = 0x0C;
	public const int AbsoluteIndexedY = 0x0D;
	public const int AbsoluteIndexedX = 0x0E;
	public const int AbsoluteLongIndexedX = 0x0F;
	public const int Accumulator = 0x10;
	public const int ProgramCounterRelative = 0x11;
	public const int StackInterrupt = 0x12;
	public const int ProgramCounterRelativeLong = 0x13;
	public const int Implied = 0x14;
	public const int AbsoluteIndirect = 0x15;
	public const int AbsoluteIndexedIndirect = 0x16;
	public const int AbsoluteIndirectLong = 0x17;
	public const int DPIndexedY = 0x18;
	public const int BlockMove = 0x19;
	public const int StackAbsolute = 0x1A;
	public const int StackDPIndirect = 0x1B;
	public const int StackPCRelativeLong = 0x1C;
	public const int StackPush = 0x1D;
	public const int StackPull = 0x1E;
	public const int Immediate = 0x1F;
	public const int StackRTI = 0x20;
	public const int StackRTL = 0x21;
	public const int StackRTS = 0x22;
	public const int NoMode = 0x23;
}