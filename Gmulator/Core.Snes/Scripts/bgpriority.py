
lines = []

with open("bgpriorities.txt", "r") as f:
    for line in f:
        lines.append([n.strip() for n in line.split()])

m = 0
for layer in lines:
    print(f'[{m}] = [',end='')
    for i in range(len(layer)):
        t = layer[i].replace('S','4')
        t = t[0].replace('1','0').replace('2','1').replace('3','2')
        if i < len(layer) - 1:
            print(f'{t}', end=', ')
        else:
            print(f'{t}', end='')
    print("],")  
    m += 1

print()

m = 0
for layer in lines:
    print(f'[{m}] = [',end='')
    for i in range(len(layer)):
        t = layer[i].replace('H','1').replace('L','0')
        t = t[1]
        if i < len(layer) - 1:
            print(f'{t}', end=', ')
        else:
            print(f'{t}', end='')
    print("],")  
    m += 1