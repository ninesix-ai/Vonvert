# -*- coding: utf-8 -*-
"""Cross-check localization keys: C# Strings property definitions vs usages."""
import re, sys, json, io, os

ROOT = os.path.dirname(os.path.abspath(__file__))

def read(path):
    with io.open(path, "r", encoding="utf-8", newline="") as f:
        return f.read()

# 1. Collect defined keys from Strings*.cs
strings_files = []
for dirpath, dirnames, filenames in os.walk(os.path.join(ROOT, "Vonvert.App")):
    dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "ref")]
    for fn in filenames:
        if fn.startswith("LocalizationManager") and fn.endswith(".cs"):
            strings_files.append(os.path.join(dirpath, fn))

defined = {}
for sf in strings_files:
    src = read(sf)
    for m in re.finditer(r'public\s+string\s+(\w+)\s*=>\s*G\(\)', src):
        defined[m.group(1)] = sf

print(f"Defined keys: {len(defined)} in {len(strings_files)} files")

# 2. Collect usages across all cs/xaml (excluding bin/obj/ref)
usage_pattern = re.compile(r'\b(\w+)\b')
xaml_key_pattern = re.compile(r'Path=(\w+)')
cs_key_pattern = re.compile(r'\bL\.(\w+)\b|\bLocalizationManager\.Instance\.(\w+)\b|\bL\b\[(\w+)\]')

used = set()
xaml_files = []
cs_files = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "ref", ".git", "publish")]
    for fn in filenames:
        p = os.path.join(dirpath, fn)
        if fn.endswith(".xaml"):
            xaml_files.append(p)
        elif fn.endswith(".cs"):
            cs_files.append(p)

# XAML: Path=Xxx within a binding that references LocalizationManager
xaml_src = "\n".join(read(p) for p in xaml_files)
# Handle both explicit Path=X and implicit {Binding X} shorthands:
# find binding markup extensions whose Source is LocalizationManager.Instance
for bm in re.finditer(r'\{Binding\s+([^{}]*?)\}', xaml_src):
    body = bm.group(1)
    if 'LocalizationManager' not in body:
        continue
    # strip known keywords
    for kw in ('Mode=OneWay', 'Mode=TwoWay', 'Mode=OneTime', 'Source=', 'x:Static', 'local:', 'LocalizationManager.Instance',
               'UpdateSourceTrigger=PropertyChanged', 'FallbackValue', 'TargetNullValue', 'IsAsync=True'):
        body = body.replace(kw, ' ')
    # remove punctuation
    body = re.sub(r'[=,{}]', ' ', body)
    for tok in body.split():
        if re.fullmatch(r'\w+', tok) and not tok[0].isdigit():
            used.add(tok)

# C#: L.Xxx / LocalizationManager.Instance.Xxx / L["Xxx"]
# Also collect every quoted CamelCase literal: several subsystems resolve keys at
# runtime by string (SoundboardStatusPolicy.Presentation.TextKey, MonitorLabels' plain
# vocabulary keys, MonitorFirstRunGuide.StepKeys, GetUiString(name)), so they have no
# `public string X => G()` property. Counting those as orphans meant the report claimed
# live UI strings were dead -- and a "clean up the orphans" pass would have deleted them.
cs_literals = set()
for p in cs_files:
    src = read(p)
    for m in cs_key_pattern.finditer(src):
        for g in m.groups():
            if g:
                used.add(g)
    for m in re.finditer(r'"([A-Za-z][A-Za-z0-9_]{2,})"', src):
        cs_literals.add(m.group(1))

# 3. Report defined-but-unused keys
unused = {k: v for k, v in sorted(defined.items()) if k not in used}
print(f"\nUsed keys: {len(used)}")
print(f"\n=== DEFINED BUT UNUSED ({len(unused)}) ===")
for k, v in unused.items():
    print(f"  {k}  <- {os.path.basename(v)}")

# 4. JSON keys check
# The language list is parsed from the C# source of truth instead of being restated
# here; a hardcoded copy is exactly how a newly added translation file would silently
# go unchecked (guarded by test L10N-09).
def supported_languages():
    src = read(os.path.join(ROOT, "Vonvert.App", "UIServices", "LocalizationManager.cs"))
    m = re.search(r"SupportedLanguages\s*=\s*\{([^}]*)\}", src)
    if not m:
        raise SystemExit("could not parse SupportedLanguages from LocalizationManager.cs")
    return tuple(re.findall(r'"([^"]+)"', m.group(1)))


SUPPORTED_LANGUAGES = supported_languages()
print(f"\nSupported languages: {', '.join(SUPPORTED_LANGUAGES)}")

for lang in SUPPORTED_LANGUAGES:
    jp = os.path.join(ROOT, "Vonvert.App", "Translations", lang + ".json")
    if not os.path.exists(jp):
        print(f"\n=== {lang}.json: not present in this build — skipped ===")
        continue
    with io.open(jp, "r", encoding="utf-8") as f:
        data = json.load(f)
    ui = data.get("ui", {})
    json_keys = set(ui.keys())
    # keys in JSON but not defined in C# strings, split by whether code still reaches them
    orphan_all = sorted(json_keys - set(defined.keys()))
    dynamic = [k for k in orphan_all if k in cs_literals]
    dead = [k for k in orphan_all if k not in cs_literals]
    missing = sorted(set(defined.keys()) - json_keys)
    print(f"\n=== {lang}.json: ui keys={len(json_keys)}, orphan={len(orphan_all)} "
          f"(resolved-by-string {len(dynamic)}, dead {len(dead)}), "
          f"missing(from C#)={len(missing)} ===")
    if dead:
        print("  DEAD JSON keys:", ", ".join(dead))
    if missing:
        print("  missing JSON keys:", ", ".join(missing))
