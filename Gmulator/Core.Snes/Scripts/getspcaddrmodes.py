modes = {

}

admodes = '''
#i	Immediate
X	Stack Operations X
d	Direct Page
d+X	X-Indexed Direct Page
d+Y	Y-Indexed Direct Page
(X)	Indirect
(X)+	Indirect Auto-Increment
dp, dp	Direct Page to D.P.
(X),(Y)	Indirect Page to I.P.
d, #i	Immediate Data to D.P.
dp. bit	Direct Page Bit
dp.bit, rel	Direct Page Bit Relative
mem. bit	Absolute Boolean Bit
!abs	Absolute
!abs+X	X-Indexed Absolute
!abs+Y	Y-Indexed Absolute
[d+X]	X-Indexed Indirect
[d]+Y	Indirect Y-Indexed 2
'''

g = list(filter(None, admodes.split("\n")))
for s in g:
    s = s.replace(" ", "")
    s = s.replace("-", "")
    s = s.replace(" to ", "to")
    s = s.replace("D.P.", "DP")
    s = s.replace("I.P.", "IP")
    s = s.split('\t')
    modes[f'{s[0]}'] = f'{s[1]}'

i = 0
for m in modes.values():
    print(f'{m} = {i}')
    i += 1

for k, v in modes.items():
    print(f'\t"{k}": "{v}",')
    i += 1
