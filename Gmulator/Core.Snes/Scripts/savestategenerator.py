

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

s = []
print("Type variables: ")
while(True):
    line = input()
    if line:
        s.append(line)
    else:
        break

s = "\n".join(s)

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

            t = f'bw.Write({x[j+n:k].replace(";","").strip()}); '
            if "[]" in x[j:j+n]:
                t = f'WriteArray(bw,{x[j+n:k].replace(";","").strip()}); '

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
                t = f'{x[j+n:k].replace(";","").strip()} = ReadArray<{x[j:j+n-2]}>(br,{x[j+n:k].replace(";","").strip()}.Length); '
            else:
                t = f'{x[j+n:k].replace(";","").strip()} = br.Read{types[type]}(); '

            reads.append(t)

i = -1
for t in writes:
    if i % 2 > 0:
        print(f'{t}', end=" ")
    else:
        print(t)
    i += 1

print("\n")

i = -1
for t in reads:
    if i % 2 > 0:
        print(f'{t}', end=" ")
    else:
        print(t)
    i += 1

print("\n")
