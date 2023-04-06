input = 'oto'
output = input + '_rephonemized'

with open(input + '.ini', 'r') as f:
        lines = f.readlines()

# find the suffix
for line in lines:
        if '-E' in line or 'pfE' in line:
                suffix = line[line.index('E')+1:line.index(',')]

with open(output + '.ini', 'w') as f:
        for line in lines:
                # basic replacement
                new_line = line.replace('X', 'ch')
                f.write(new_line)
                # consonant duplication
                if '=n' in line: f.write(new_line.replace('=n', '=N'))
                if 'n'+suffix+',' in line: f.write(new_line.replace('n'+suffix+',', 'N'+suffix+','))
                if 'h'+suffix+',' in line: f.write(new_line.replace('h'+suffix+',', '-'+suffix+','))
                if 'S' in line:
                        f.write(new_line.replace('S', 'Z'))
                        f.write(new_line.replace('S', 'dZ'))
                # vowel duplication
                if 'E' in line: f.write(new_line.replace('E', 'oe'))
                if 'E' in line: f.write(new_line.replace('E', '@'))
                if 'Y' in line: f.write(new_line.replace('Y', '0'))
                # VC specific vowel duplication
                if '=o' in line: f.write(new_line.replace('=o', '=U'))
                if '=e' in line: f.write(new_line.replace('=e', '=I'))
                # CV specific vowel duplication
                if 'o'+suffix+',' in line: f.write(new_line.replace('o'+suffix+',', 'U'+suffix+','))
                if 'e'+suffix+',' in line: f.write(new_line.replace('e'+suffix+',', 'I'+suffix+','))
                # diphtong duplication
                if 'a'+suffix+',' in line:
                        f.write(new_line.replace('a'+suffix+',', 'aU'+suffix+','))
                        f.write(new_line.replace('a'+suffix+',', 'aI'+suffix+','))
                if 'O'+suffix+',' in line:
                        f.write(new_line.replace('O'+suffix+',', 'OI'+suffix+','))
                if '=o' in line:
                        f.write(new_line.replace('=o', '=aU'))
                if '=e' in line:
                        f.write(new_line.replace('=e', '=aI'))
                        f.write(new_line.replace('=e', '=OI'))
