"""Validate stable release tags before using them in paths or build properties."""
import os
import re
import sys


def parse_tag(tag):
    match = re.fullmatch(r"v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)", tag)
    if not match:
        raise ValueError("Release-Tag muss vMAJOR.MINOR.PATCH entsprechen (z. B. v1.0.0).")
    major, minor, patch = map(int, match.groups())
    if major > 2099 or minor > 999 or patch > 999:
        raise ValueError("Versionsgrenzen: MAJOR <= 2099; MINOR und PATCH <= 999.")
    code = major * 1_000_000 + minor * 1_000 + patch
    if code == 0:
        raise ValueError("v0.0.0 ist keine veröffentlichbare Version.")
    return tag[1:], code


if __name__ == "__main__":
    try:
        version, code = parse_tag(sys.argv[1])
    except (ValueError, IndexError) as error:
        sys.exit(str(error))
    result = f"version={version}\nandroid_code={code}\n"
    if os.environ.get("GITHUB_OUTPUT"):
        with open(os.environ["GITHUB_OUTPUT"], "a", encoding="utf-8") as output:
            output.write(result)
    print(result, end="")
