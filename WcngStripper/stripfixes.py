import re

MIN_FIX_LENGTH = 3
WELL_KNOWN_SUFFIXES = {"kit", "paw", "star"}

prefixes = set()
suffixes = set()
names = set()

def isValidFix(expr: str):
    # Bee^tle
    if expr.startswith("tl") : return False
    # Nutmeg
    if expr in {"meg"} : return False
    return True

with open("fixes.txt") as f:
    for l in f:
        l = l.strip()
        if l == "": continue
        if l.startswith("#"): continue
        if l[-1] == "-": prefixes.add(l[:-1].lower())
        elif l[0] == "-": suffixes.add(l[1:].lower())
        else: raise AssertionError()

with open("names.txt") as f:
    for l in f:
        l = re.sub(r"\(.*?\)", "", l).strip().lower()
        l = l.replace("-", "")      # One-eye
        if l == "": continue
        if " " in l: continue
        if (len(l) < 6): continue
        if l.startswith("#"): continue
        names.add(l)
        
print("Round 0")
for l in names:
    for suffix in WELL_KNOWN_SUFFIXES:
        if l.endswith(suffix):
            prefixes.add(l[:-len(suffix)])
            break

for rounds in range(10):
    anyNewFixes = False
    failedNames = []
    print("Round", rounds)
    for l in names:
        # Brute force
        # Find longest prefix
        fix = None
        for i in range(len(l) - MIN_FIX_LENGTH, MIN_FIX_LENGTH - 1, -1):
            if l[:i] in prefixes:
                fix = l[:i]
                break
        if fix != None:
            # The rest is suffix
            if len(l) - len(fix) > 3 and l[len(fix)] == "y":  # The prefix ends with y,
                fix += "y"          # counts y in.
            elif len(l) - len(fix) > 4 and l[len(fix) + 1] == "y":
                fix += l[len(fix)] + "y"
            suffix = l[len(fix):]
            assert suffix != "e"
            if not suffix in suffixes and isValidFix(suffix):
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
            assert prefix != "net"
            if not prefix in prefixes and isValidFix(prefix):
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

