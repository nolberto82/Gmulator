from enum import Enum
import os
from bs4 import BeautifulSoup
import re


class Opcode:
    def __init__(self, op, oper, format, mode, size, opid):
        self.op = op
        self.oper = oper
        self.format = format
        self.mode = mode
        self.size = size
        self.opid = opid


addrmode = []
constlist = []

opreplacers = {" ": "", "/": "", ",": "", "(": "", ")": ""}

opcodes = []
oplist = []
admodes = []
tr = []
listop = ["OpInfo"]
n = 0
j = 0
x = 0
i = 0

with open("spcopcodes.txt", "r") as f:
    k = 0
    for s in f:
        s = s.rstrip()
        oplist.append(s.split(" "))
        oplist[k].insert(2, "")
        k += 1

for s in oplist:
    opcodes.append(Opcode(s[0], s[1], s[2], s[3], s[4], [s[5]]))

for s in opcodes:
    if s.mode not in addrmode:
        addrmode.append(s.mode)

for s in oplist:
    if s[0] not in constlist:
        constlist.append(s[0])

oplist.sort(key=lambda o: o[4])
constlist.sort()
addrmode.sort()

with open("../../../Gmulator/Core.Snes/SnesSpcInfo.cs", "w") as f:
    f.write("using System.Text.Json.Serialization;\n\n")
    f.write("namespace Gmulator.Core.Snes;\n")
    f.write("public partial class SnesSpc\n{\n")
    f.write('''	public struct Opcode(string name, string oper, string format, int id, int mode, int size)
    {
        public string Name = name;
        public string Oper = oper;
        public string Format = format;
        public int Id = id;
        public int Mode = mode;
        public int Size = size;
    };\n''')

    f.write("\n")

    opid = 0
    f.write("\t[JsonIgnore]\n")
    f.write("\tpublic List<Opcode> Disasm = new();\n\n")
    f.write("\tpublic void CreateOpcodes() \n\t{\n")
    for l in opcodes:
        l.oper = l.oper.replace("d.,","${0:x2},")
        l.oper = l.oper.replace(",r", ",${1:x4}")
        l.oper = l.oper.replace("d,#i", "${0:x2},#${1:x2}")
        l.oper = l.oper.replace("dd,ds", "${0:x2},${1:x2}")
        l.oper = l.oper.replace("Y,r", "${0:x2},${1:x4}")
        l.oper = l.oper.replace("d,r", "${0:x2},${1:x4}") 
        l.oper = l.oper.replace("m.b", "${0:x4}.{1}") 
        l.oper = l.oper.replace("#i", "#${0:x2}")
        l.oper = l.oper.replace("!a", "!${0:x4}")
        l.oper = l.oper.replace("r", "${0:x4}")
        l.oper = l.oper.replace("d", "${0:x2}")

        f.write(
            f'\t\tDisasm.Add(new("{l.op.lower()}", "{l.oper.lower()}", "{l.format}", {l.op.upper()}, {l.mode}, {l.size})); //{opid:02X}\n')
        opid += 1

    f.write("\t}\n\n\t//Opcodes\n")
    for i, l in enumerate(constlist):
        f.write(f"\tpublic const int {l.upper()} = 0x{i:02X};\n")

    f.write("\n\t//AddrMode\n")
    for i, a in enumerate(addrmode):
        f.write(f"\tpublic const int {a} = 0x{i:02X};\n")
        if a == "MOV":
            f.write(f'\n\tcase MOVR: break;')

    f.write("\t}")

with open("switchcases.txt", "w") as f:
    f.write('switch (Disasm[op].Id)\n{')
    for l in constlist:
        f.write('\n\tcase %s: break;' %
                l.replace('\t', '').replace('\n', '').upper())
        if l == "MOV":
            f.write(f'\n\tcase MOVR: break;')

    f.write('\n}\n\n')

    f.write('switch (op)\n{')
    for l in oplist:
        f.write(f'\n\tcase 0x{l[4]}: SetState(Debugging); break;')

    f.write('\n}\n\n')

    f.write('switch (mode)\n{')
    for l in addrmode:
        f.write('\n\tcase %s: return 0;' %
                l.replace('\t', '').replace('\n', ''))

    f.write('\n}\n')

    for i, l in enumerate(constlist):
        f.write(f'\n\tprivate void {l.capitalize()}()\n\t{{\t\n\n\t}}\n')

    f.write('\n}')
