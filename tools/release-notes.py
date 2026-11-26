#!/usr/bin/env python3
"""Prints the CHANGELOG.md section of a version as release notes (used by .github/workflows/release.yml).

  python3 tools/release-notes.py 0.6.0 [CHANGELOG.md]

Exits with 1 if the version has no section. Link references like [#47] become plain #47 (GitHub links them).
"""
import re
import sys

version = sys.argv[1].removeprefix("v")
path = sys.argv[2] if len(sys.argv) > 2 else "CHANGELOG.md"
text = open(path, encoding="utf-8").read()
m = re.search(rf"^## \[{re.escape(version)}\][^\n]*\n(?P<body>.*?)(?=^## \[|^\[[^\]]+\]: |\Z)", text, re.M | re.S)
if not m:
    sys.exit(f"no section for {version} in {path}")
body = re.sub(r"\[(#\d+)\](?!\()", r"\1", m.group("body")).strip()
print(body + f"\n\nAlle Änderungen: [CHANGELOG.md](https://github.com/vulture20/Video-Thumbnailer/blob/v{version}/CHANGELOG.md)")
