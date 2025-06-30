import os
from bs4 import BeautifulSoup
import re


class Opcode:
    def __init__(self, op, oper, format, size):
        self.op = op
        self.oper = oper
        self.format = format
        self.size = size


opcodes = []
constop = []
listop = ["OpInfo00", "OpInfoCB"]
n = 0
j = 0
x = 0

with open("gb-opcodes-2.html", encoding="windows-1252") as rfile:
    soup = BeautifulSoup(rfile.read(), "html.parser")
    # tr = soup.find("table", {"id": "unprefixed-16-m"}).findAll("tr")
    # tr.extend(soup.find("table", {"id": "cbprefixed-16-m"}).findAll("tr"))
    tr = soup.find("table", {"id": ""}).findAll("tr")
    tr.extend(soup.find(string="RLC B").find_parent("table").findAll("tr"))

    for i in range(1, len(tr)):
        if i == 0x11:
            continue

        td = tr[i].contents

        for k in range(1, len(td)):
            op = ""
            oper = "N/A"
            size = '0'
            cycles = '0'
            cycles2 = '0'
            isjump = "false"

            st = td[k].contents
            if len(st) == 0 or st[0] == "\xa0":
                opcodes.append(Opcode("pre", "", "", 0))
                # print(
                #    f'{listop[n]}.Add(new Opcode("pre","{oper.lower()}",{byte},{cycles},{cycles2})); //{j:02X}'
                # )
                j += 1
                continue

            if (j == 0xea):
                ui=0

            format = ""
            sp = st[0].split(" ")
            op = sp[0]
            if len(sp) > 1:
                fs = sp[1].split(",")
                if len(fs) > 1 and "a16" in sp[1]:
                    oper = fs[1]
                else:
                    oper = sp[1]

                if "d8" in oper:
                    format = "x2"
                elif "r8" in oper:
                    format = "x4"
                elif "16" in oper:
                    format = "x4"  
                elif "16" in fs[0]:
                    format = "x4" 

                oper = oper.replace("d16", '')
                oper = oper.replace("a16", '')
                oper = oper.replace("d8", '')
                oper = oper.replace("r8", '')

            size = st[2][0]

            if op == "JP" or op == "JR" or op == "CALL" or op == "RET" or op == "RETI" or op == "RST":
                isjump = "true"

            if j == 256:
                n = 1
                j = 0
                print("")

            constop.append(op.upper())
            opcodes.append(Opcode(op, oper, format, size))

            # print(
            #    f'{listop[n]}.Add(new Opcode("{op.lower()}","{oper.lower()}",{byte},{cycles},{cycles2})); //{j:02X}'
            # )
            j += 1

n = 0
j = 0

constop.append("ERR")
constop = list(dict.fromkeys(constop))

with open(f'{os.path.dirname(os.getcwd())}/OpcodeInfo.cs', "w") as wf:
    wf.write("namespace Gmulator.Core.Gbc;\n")
    wf.write("public partial class GbcCpu\n{")

    wf.write("""
    public class Opcode(string name, string oper, string format, byte size)
    {
        public string Name = name;
        public string Oper = oper;
        public string Format = format;
        public byte Size = size;
    }
    \n\t""")

    wf.write("public static List<Opcode> OpInfo00;\n\t")
    wf.write("public static List<Opcode> OpInfoCB;\n\n\t")
    wf.write("public static void GenerateOpInfo()\n\t{\n\t\t")
    wf.write("OpInfo00 = [];\n\t\t")
    wf.write("OpInfoCB = [];\n\n")

    for s in opcodes:
        if j == 256:
            n = 1
            j = 0
            wf.write("")

        wf.write(
            f'\t\t{listop[n]}.Add(new Opcode("{s.op.lower()}", "{s.oper.lower()}", "{s.format.lower()}", {s.size})); //{j:02X}\n'
        )

        j += 1

    wf.write("\t}\n")

    # j = 0
    # print(f'\t\t\tswitch(op){{')
    # for s in constop:
    #     wf.write(f'\tprivate const int {s} = 0x{j:02X};\n')
    #     print(f'\t\t\tcase {s}:\n\t\t\tPC--;\n\t\t\tbreak;')
    #     j += 1

    wf.write("}\n\n")
    # print(f'}}')

# j = 0
# n = 0

# if not os.path.exists("../Core/Instructions.cs"):
#     with open("../Core/Instructions.cs", "w") as wf:
#         wf.write("namespace GbRG.Core;\n")
#         wf.write("public partial class Cpu\n{\n")

#         for s in opcodes:
#             if j == 256:
#                 n = 1
#                 j = 0
#                 wf.write("")

#             func = s.func
#             if func == "":
#                 func = f'{j:02X}'

#             wf.write(f'\tprivate void '
#             f'{func}_{j:02X}(byte op)\n\t'
#             f'{{\n\t\tError = $"{{PC:X4}} {{op:X2}}";'
#             f'\n\t\tState = Debugging;\n\t}}'
#             f'\n\n')

#             j += 1
#         wf.write("}")
