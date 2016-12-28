MIN_FIX_LENGTH = 3

prefixes = set()
suffixes = set()

newPrefixes = []
newSuffixes = []

with open("fixes.txt") as f:
    for l in f:
        l = l.strip()
        if l == "": continue
        if l.startswith("#"): continue
        if l[-1] == "-": prefixes.add(l[:-1].lower())
        elif l[0] == "-": suffixes.add(l[1:].lower())
        else: raise(AssertionError())

with open("names.txt") as f:
    for l in f:
        l = l.strip().lower()
        if l == "": continue
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
                newSuffixes.append(suffix)
            continue
        # Find longest suffix
        fix = None
        for i in range(MIN_FIX_LENGTH, len(l) - MIN_FIX_LENGTH + 1):
            if l[-i:] in suffixes:
                fix = l[-i:]
                break
        if fix != None:
            # The rest is prefix
            prefix = l[:-len(fix)]
            if not prefix in prefixes:
                prefixes.add(prefix)
                newPrefixes.append(prefix)
            continue
        # Cannot split the name
        print("Cannot split:", l)

with open("fixes.txt", "a") as f :
    print(file=f)
    print("# Collected fixes", file=f)
    if len(newPrefixes) > 0:
        print("\n# Prefixes", file=f)
        for fix in newPrefixes:
            print(fix.capitalize() + "-", file=f)
    if len(newSuffixes) > 0:
        print("\n# Suffixes", file=f)
        for fix in newSuffixes:
            print("-" + fix, file=f)