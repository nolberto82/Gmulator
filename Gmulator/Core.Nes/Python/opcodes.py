import csv
import os

lines = []
oplist = []

with open("opcodes.csv", "r") as f:
    csvread = csv.reader(f)
    header = next(csvread)
    for line in csvread:
        lines.append(line[1:])
        oplist.append(line[1])

oplist = list(set(oplist))

adlist = '''impl,accu,imme,zerp,zerx,zery,abso,absx,absy,indx,indy,indi,rela,erro'''.split(
    ",")

with open("../../Core.Nes/OpcodeInfo.cs", "w") as f:
    f.write("namespace Gmulator.Core.Nes;\n")
    f.write("public partial class NesCpu\n{\n\t")

    f.write("//Opcodes\n")
    for i, l in enumerate(oplist):
        f.write(f"\tpublic const int {l.upper()} = 0x{i:02X};\n")

    f.write("\n\t//AddrMode\n")
    for i, a in enumerate(adlist):
        f.write(f"\tpublic const int {a.upper()} = 0x{i:02X};\n")

    f.write("\n")

    f.write('''	public struct Opcode(string name, int id, int mode, int size, int cycles, int extracycle)
    {
        public string Name = name;
        public int Id = id;
        public int Mode = mode;
        public int Size = size;
        public int Cycles = cycles;
        public int ExtraCycle = extracycle;
    }\n''')

    f.write("\n")

    # f.write('public static string Formats[]\n{' '\
    # \n\t{"%04X %-8.02X  %-3s"}, //impl' '\
    # \n\t{"%04X %-8.02X  %-3s A"}, //accu' '\
    # \n\t{"%04X %02X %-5.02X  %-3s #$%02X"}, //imme' '\
    # \n\t{"%04X %02X %-5.02X  %-3s $%02X = $%02X"}, //zerp' '\
    # \n\t{"%04X %02X %-5.02X  %-3s $%02X,X @ $%02X = $%02X"}, //zerx' '\
    # \n\t{"%04X %02X %-5.02X  %-3s $%02X,Y @ $%02X = $%02X"}, //zery' '\
    # \n\t{"%04X %02X %02X %-2.02X  %-3s $%04X = $%02X"}, //abso' '\
    # \n\t{"%04X %02X %02X %-2.02X  %-3s $%04X,X @ $%04X = $%02X"}, //absx' '\
    # \n\t{"%04X %02X %02X %-2.02X  %-3s $%04X,Y @ $%04X = $%02X"}, //absy' '\
    # \n\t{"%04X %02X %-5.02X  %-3s ($%02X,X) @ $%04X = $%02X"}, //indx' '\
    # \n\t{"%04X %02X %-5.02X  %-3s ($%02X),Y @ $%04X = $%02X"}, //indy' '\
    # \n\t{"%04X %02X %02X %-2.02X  %-3s ($%04X) @ $%04X = $%02X"}, //indi' '\
    # \n\t{"%04X %02X %-5.02X  %-3s $%04X = $%02X"}, //rela' '\
    # \n};\n\n')

    opid = 0
    f.write("\tpublic List<Opcode> Disasm = [];\n\n")
    f.write("\tpublic void CreateOpcodes() \n\t{\n")
    for l in lines:
        if l[1] == 'err':
            f.write(
                f"\t\tDisasm.Add(new(\"{l[0]}\", {l[0].upper()}, {l[1].upper()}, 1, {l[3]}, {l[4]})); //{opid:02X}\n")
        elif l[1] == 'accu':
            f.write(
                f"\t\tDisasm.Add(new(\"{l[0]} A\", {l[0].upper()}, {l[1].upper()}, 1, {l[3]}, {l[4]})); //{opid:02X}\n")
        else:
            f.write(
                f"\t\tDisasm.Add(new(\"{l[0]}\", {l[0].upper()}, {l[1].upper()}, {l[2]}, {l[3]}, {l[4]})); //{opid:02X}\n")
        opid += 1

    f.write("\t}\n}")

with open("switchcases.txt", "w") as f:
    f.write('switch ()\n{')

    for i, l in enumerate(oplist):
        f.write('\ncase opcid::%s:\n{\n\tbreak;\n}' %
                l.replace('\t', '').replace('\n', '').upper())

    f.write('\n}')
