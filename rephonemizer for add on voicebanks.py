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
                new_line = line.replace('Y', '0')
                f.write(new_line)
