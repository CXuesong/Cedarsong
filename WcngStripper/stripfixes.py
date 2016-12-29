MIN_FIX_LENGTH = 3

prefixes = set()
suffixes = set()

with open("fixes.txt") as f:
    for l in f:
        l = l.strip()
        if l == "": continue
        if l.startswith("#"): continue
        if l[-1] == "-": prefixes.add(l[:-1].lower())
        elif l[0] == "-": suffixes.add(l[1:].lower())
        else: raise(AssertionError())

for rounds in range(10):
    anyNewFixes = False
    failedNames = []
    print("Round", rounds)
    with open("names.txt") as f:
        for l in f:
            l = l.strip().lower()
            if l == "": continue
            if l.startswith("#"): continue
            # We do not process anything other than clan cat names.
            if " " in l: continue
            # Brute force
            # Find longest prefix
            fix = None
            for i in range(len(l) - MIN_FIX_LENGTH - 1, MIN_FIX_LENGTH - 1, -1):
                if l[:i] in prefixes:
                    fix = l[:i]
                    break
            if fix != None:
                # The rest is suffix
                suffix = l[len(fix):]
                if not suffix in suffixes:
                    suffixes.add(suffix)
                    anyNewFixes = True
                continue
            # Find longest suffix
            fix = None
            for i in range(len(l) - MIN_FIX_LENGTH, MIN_FIX_LENGTH - 1, -1):
                if l[-i:] in suffixes:
                    fix = l[-i:]
                    break
            if fix != None:
                # The rest is prefix
                prefix = l[:-len(fix)]
                if not prefix in prefixes:
                    prefixes.add(prefix)
                    anyNewFixes = True
                continue
            # Cannot split the name
            failedNames.append(l)
    if not anyNewFixes:
        print("No more -fixes detected.")
        for l in failedNames:
            print("Cannot parse:", l)
        break

with open("prefixes.txt", "w") as f:
    for fix in sorted(prefixes):
        print(fix.capitalize(), file=f)
with open("suffixes.txt", "w") as f:
    for fix in sorted(suffixes):
        print(fix, file=f)

