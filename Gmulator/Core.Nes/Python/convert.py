

modes = {
    'Implied     ': 'impl',
    'Immediate   ': 'imme',
    'Zero Page   ': 'zerp',
    'Zero Page,X ': 'zerx',
    'Zero Page,Y ': 'zery',
    'Absolute    ': 'abso',
    'Absolute,X  ': 'absx',
    'Absolute,Y  ': 'absy',
    '(Indirect,X)': 'indx',
    '(Indirect),Y': 'indy'
}

txt = '''
Absolute,Y  |DCP arg,Y  |$DB| 3 | 7
'''

txt = filter(None, txt.split('\n'))

for t in txt:
    mm = ""
    ec = 0
    if '*' in t:
        ec = 1
    for m in modes:
        if t[:12] == m:
            mm = modes[m]
            break
    t = t.replace('-', '0')
    print(
        f"0x{t[26:28].lower()},{t[13:16].lower()},{mm},{t[30:31]},{t[34:35]},{ec}")
