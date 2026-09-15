from pathlib import Path

script_path = Path('.github/scripts/fix_owner_actions_refunds.py')
source = script_path.read_text(encoding='utf-8')
old = '''    n = s.count(old)\n    if n != 1:\n        raise SystemExit(f"{label}: expected 1 match, found {n}")\n    p.write_text(s.replace(old, new, 1), encoding="utf-8-sig")'''
new = '''    n = s.count(old)\n    if label == "owner total paid insert" and n == 2:\n        before, sep, after = s.rpartition(old)\n        p.write_text(before + new + after, encoding="utf-8-sig")\n        return\n    if label == "fee cap" and n == 2:\n        p.write_text(s.replace(old, new, 1), encoding="utf-8-sig")\n        return\n    if n != 1:\n        raise SystemExit(f"{label}: expected 1 match, found {n}")\n    p.write_text(s.replace(old, new, 1), encoding="utf-8-sig")'''
if old not in source:
    raise SystemExit('Could not patch helper function')
exec(compile(source.replace(old, new, 1), str(script_path), 'exec'))
