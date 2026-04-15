from enum import Enum
import os
from bs4 import BeautifulSoup
import re


class Opcode:
    def __init__(self, op, oper, mode, size, opid):
        self.op = op
        self.oper = oper
        self.mode = mode
        self.size = size
        self.opid = opid


oplist = []

with open("spc700ops.html", "r") as f:
    soup = BeautifulSoup(f.read(), "html.parser")
    x = soup.findAll("tr")
    n = 0
    for c in x:
        if n > 0:
            s = list(c.text.split("\n"))
            s = [t.strip('    ') for t in s]
            s = list(filter(None, s))
            oplist.append(list(filter(None, s)))
        n += 1

    for s in oplist:
        i = re.sub("([()#,])", "", s[0])
        i = i.split(" ")
        a = s[1].split(" ")

        if len(i) == 1:
            print(f'"{i[0]}"')
        elif len(i) == 2:
            print(f'"{i[0]} {i[1]}"')
        else:
            print(f'"{i[0]} {i[1]},{i[2]}", "{a[0]}')

    print('fswitch (op)\n\t')
    for s in oplist:
        if s[2] == "8E":
            u = 0
        print(f'case 0x{s[2]}:')
