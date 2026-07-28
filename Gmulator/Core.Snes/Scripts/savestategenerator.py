
s = '''
    protected ushort _pc, _sp, _ra, _rx, _ry, _dpr;
    protected byte _ps, _dbr, _pbr;
    protected bool _emulationMode;
    public bool FastMem { get; set; }
    public bool NmiEnabled { get; set; }
    public bool IrqEnabled { get; set; }
    public bool IrqActive { get; set; }

    private ulong cycles;
'''

types = {
    "byte": "Byte",
    "sbyte": "SByte",
    "ushort": "UInt16",
    "short": "Int16",
    "int": "Int32",
    "uint": "UInt32",
    "long": "Int64",
    "ulong": "UInt64",
    "bool": "Boolean",
}

writes = []
reads = []
s = [x.strip() for x in s.split("\n")]

for x in s:
    if x.startswith("//") or x == '':
        continue
    w = x.split(", ")
    for x in w:
        if x.startswith("public") or x.startswith("private") or x.startswith("protected") or x.startswith("_"):
            n = x.find(" //")
            if n > -1:
                x = x[0:n]
            j = x.find(" ") + 1
            k = x.find("{")
            if k == -1:
                k = x.find(" = ")
                if k == -1:
                    k = len(x)
            n = x[j:k].find(" ")

            if n==-1:
                n=0

            t = f'bw.Write({x[j+n:k].replace(";","").strip()});'
            if "[]" in x[j:j+n]:
                t = f'WriteArray(bw,{x[j+n:k].replace(";","").strip()});'

            writes.append(t)

for x in s:
    if x.startswith("//") or x == '':
        continue
    w = x.split(", ")
    type = x.split()[1]    
    for x in w:
        if x.startswith("public") or x.startswith("private") or x.startswith("protected") or x.startswith("_"):
            n = x.find(" //")
            if n > -1:
                x = x[0:n]
            j = x.find(" ") + 1
            k = x.find("{")
            if k == -1:
                k = x.find(" = ")
                if k == -1:
                    k = len(x)
            n = x[j:k].find(" ")

            if n==-1:
                n=0

            if "[]" in x[j:j+n]:
                t = f'{x[j+n:k].replace(";","").strip()} = ReadArray<{x[j:j+n-2]}>(br,{x[j+n:k].replace(";","").strip()}.Length);'
            else:
                t = f'{x[j+n:k].replace(";","").strip()} = br.Read{types[type]}();'

            reads.append(t)

with open("savestate.txt", "w") as f:
    i = -1
    for t in writes:
        i += 1
        if i % 2 > 0:
            f.write(f'{t}\n')
        else:
            f.write(t)

    f.write("\n\n")

    i = -1
    for t in reads:
        i += 1
        if i % 2 > 0:
            f.write(f'{t}\n')
        else:
            f.write(t)
