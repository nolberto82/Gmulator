
s = '''    public int M7A; //211B
    public int M7B; //211C
    public int M7C; //211D
    public int M7D; //211E
    public int M7X; //211F
    public int M7Y; //2120
    public int CGADD; //2121
    public int CGDATA; //2122
    public int COLDATA; //2132
    public int MPYL; //2134
    public int MPYM; //2135
    public int MPYH; //2136
    public int SLHV; //2137
    public int OAMDATAREAD; //2138
    public int VMDATALREAD; //2139
    public int VMDATAHREAD; //213A
    public int CGDATAREAD; //213B
    public int OPHCT; //213C
    public int OPVCT; //213D
    public int STAT77; //213E
    public int STAT78; //213F


    public int NMITIMEN; ///4200
    public int WRIO; //4201
    public int HTIMEL; //4207
    public int HTIMEH; //4208
    public int VTIMEL; //4209
    public int VTIMEH; //420A
    public int MDMAEN; //420B
    public int HDMAEN; //420C
    public int RDNMI; //4210
    public int TIMEUP; //4211
    public int HVBJOY; //4212
    public int RDIO; //4213
    public int JOY1L; //4218
    public int JOY1H; //4219
    public int JOY2L; //421A
    public int JOY2H; //421B
    public int JOY3L; //421C
    public int JOY3H; //421D
    public int JOY4L; //421E
    public int JOY4H; //421F
    private bool CounterLatch;
    private bool OphctLatch;
    private bool OpvctLatch;
    private int MultiplyRes;
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
    if x.startswith("public") or x.startswith("private"):
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

        t = f'bw.Write({x[j+n:k].replace(";","").strip()});'
        if "[]" in x[j:j+n]:
            t = f'EmuState.WriteArray<{x[j:j+n-2]}>(bw,{x[j+n:k].strip()});'

        writes.append(t)

for x in s:
    if x.startswith("//") or x == '':
        continue
    if x.startswith("public") or x.startswith("private"):
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

        if "[]" in x[j:j+n]:
            t = f'{x[j+n:k].strip()} = EmuState.ReadArray<{x[j:j+n-2]}>(br,{x[j+n:k].strip()}.Length);'
        else:
            t = f'{x[j+n:k].replace(";","").strip()} = br.Read{types[x[j:j+n]]}();'

        reads.append(t)

i = 0
for t in writes:
    i += 1
    if i % 2 > 0:
        print(t, end=" ")
    else:
        print(t)

print("\n")

i = 0
for t in reads:
    i += 1
    if i % 2 > 0:
        print(t, end=" ")
    else:
        print(t)
