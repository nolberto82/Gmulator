import os
from bs4 import BeautifulSoup
import re


class Opcode:
    def __init__(self, op, mode, id, imm, size, cycles, opid):
        self.op = op
        self.mode = mode
        self.id = id
        self.imm = imm
        self.size = size
        self.cycles = cycles
        self.opid = opid


opreplacers = {" ": "", "/": "", ",": "", "(": "", ")": ""}
adlist = ["ImmediateMemory", "ImmediateIndex"]

opcodes = []
oplist = []
listop = ["OpInfo"]
n = 0
j = 0
x = 0
i = 0

with open("opcodes.html", encoding="windows-1252") as rfile:
    soup = BeautifulSoup(rfile.read(), "html.parser")
    l = soup.find("table", {"id": ""}).findAll("tr")

    tr = []

    for c in l:
        s = list(c.text.split("\n"))
        s = [t.strip('    ') for t in s]

        if "ADC" in s[1]:
            ll=0

        if "JSL" in s[2] or "JML" in s[2]:
            s[1] = s[1].replace("JSR", "JSL") \
                .replace("JMP", "JML")            

        s.pop(2)
        s.pop(2)
        s = list(filter(None, s))

        tr.append(s)

    for s in tr:
        if i > 0:
            if "Immediate" in s:
                nn = 0

            s[0] = s[0].replace("dp", "${0:x2}") \
                .replace("#const", "#${0:x2}") \
                .replace("nearlabel", "${0:x4}") \
                .replace("addr", "${0:x4}") \
                .replace("long", "${0:x6}")

            mode = s[2]

            if mode == "" or mode == "2":
                mode = "NoMode"
            else:
                for k, v in opreplacers.items():
                    mode = mode.replace(k, v)

            imm = False
            if "Immediate" in mode:
                imm = True

            note = s[len(s)-1]
            size = s[len(s)-2]

            if "[12]" in size:
                mode = "ImmediateMemory"
            elif "[14]" in size:
                mode = "ImmediateIndex"

            opcodes.append(
                Opcode(s[0][:3], mode, s[0][4:], imm, size[:1], note[:1], int(s[1], 16)))
            if s[0][:3] not in oplist:
                oplist.append(s[0][:3])

            if mode not in adlist:
                adlist.append(mode)
        i += 1

opcodes.sort(key=lambda o: o.opid)

with open("../../../Gmulator/Core.Snes/SnesOpcodes.cs", "w") as f:
    f.write("using System.Text.Json.Serialization;\n\n")
    f.write("namespace Gmulator.Core.Snes;\n")
    f.write("public partial class SnesCpu\n{\n")
    f.write('''	public struct Opcode(string name, string oper, int mode, int id, bool imm, int size, int cycles)
    {
        public string Name = name;
        public string Oper = oper;
        public int Mode = mode;
        public int Id = id;
        public bool Immediate = imm;
        public int Size = size;
        public int Cycles = cycles;
    };\n''')

    f.write("\n")

    opid = 0
    f.write("\t[JsonIgnore]\n")
    f.write("\tpublic List<Opcode> Disasm = new();\n\n")
    f.write("\tpublic void CreateOpcodes() \n\t{\n")
    for l in opcodes:
        f.write(
            f'\t\tDisasm.Add(new("{l.op[:3].lower()}", "{l.id.lower()}", {l.mode}, {l.op}, {str(l.imm).lower()}, {l.size}, {l.cycles})); //{opid:02X}\n')
        opid += 1

    f.write("\t}\n\n\t//Opcodes\n")
    for i, l in enumerate(oplist):
        f.write(f"\tpublic const int {l.upper()} = 0x{i:02X};\n")

    f.write("\n\t//AddrMode\n")
    for i, a in enumerate(adlist):
        f.write(f"\tpublic const int {a} = 0x{i:02X};\n")

    f.write("}")

with open("switchcases.txt", "w") as f:
    f.write('switch (Disasm[op].Id)\n{')
    for i, l in enumerate(oplist):
        f.write('\n\tcase %s: break;' %
                l.replace('\t', '').replace('\n', '').upper())

    f.write('\n\n')

    f.write('switch (mode)\n{')
    adlist.sort()
    for i, l in enumerate(adlist):
        f.write('\n\tcase %s: return 0;' %
                l.replace('\t', '').replace('\n', ''))

    f.write('\n}\n')

    for i, l in enumerate(oplist):
        f.write(f'\n\tprivate void {l}()\n\t{{\t\n\n\t}}\n')

    f.write('\n}')
