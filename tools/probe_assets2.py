"""Extended probe with more filename heuristics."""
import json
import re
import ssl
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "data" / "mod-registry.json"
ctx = ssl.create_default_context()


def head_ok(url: str) -> bool:
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "probe"})
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=15) as resp:
            return resp.status < 400
    except urllib.error.HTTPError as e:
        return e.code in (200, 302)
    except Exception:
        return False


def path_tag(tag: str) -> str:
    t = tag.strip()
    return t[1:] if t.lower().startswith("v") else t


def file_tag(tag: str) -> str:
    t = tag.strip()
    return t if t.lower().startswith("v") else "v" + t


def get_live_tag(owner: str, repo: str) -> str | None:
    url = f"https://github.com/{owner}/{repo}/releases/latest"
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "probe"})
    with urllib.request.urlopen(req, context=ctx, timeout=15) as resp:
        loc = resp.geturl()
    m = re.search(r"/releases/tag/([^/?#]+)", loc or "")
    return m.group(1) if m else None


def build_candidates(entry: dict, tag: str) -> list[str]:
    owner, repo = entry["githubOwner"], entry["githubRepo"]
    pt, ft = path_tag(tag), file_tag(tag)
    tags = [pt, ft] if pt.lower() != ft.lower() else [pt]
    names: list[str] = []
    filters = entry.get("assetFilters") or {}

    for ch, filt in filters.items():
        if not filt:
            continue
        for pat in filt.get("patterns") or []:
            if pat in (".dll", ".zip"):
                if ch == "default" and pat == ".zip":
                    for t in tags:
                        names += [
                            f"{repo}-{t}.zip",
                            f"{repo}_{t}.zip",
                            f"{t}.zip",
                            "win-x64.zip",
                            f"{repo}-win-x64.zip",
                        ]
                continue
            if ch == "dll" and pat.endswith(".dll"):
                names.append(pat)
                continue
            if filt.get("exactMatch") and pat.endswith(".zip"):
                names.append(pat)
                continue
            for t in tags:
                names += [
                    f"{repo}-{t}-{pat}.zip",
                    f"{repo}-{pat}-{t}.zip",
                    f"{pat}-{t}.zip",
                    f"{t}-{pat}.zip",
                    f"{pat}.zip",
                ]
                if "steam" in pat.lower() or "itch" in pat.lower():
                    names += [
                        f"{repo}-{t}-x86-steam-itch.zip",
                        f"{repo}-{ft}-x86-steam-itch.zip",
                        f"TouMira-{ft}-x86-steam-itch.zip",
                    ]
                if "epic" in pat.lower() or "msstore" in pat.lower() or "ms" in pat.lower():
                    names += [
                        f"{repo}-{t}-x64-epic-msstore.zip",
                        f"{repo}-{ft}-x64-epic-msstore.zip",
                        f"TouMira-{ft}-x64-epic-msstore.zip",
                    ]

    return list(dict.fromkeys(names))


def probe(entry: dict, tag: str) -> list[tuple[str, str]]:
    owner, repo = entry["githubOwner"], entry["githubRepo"]
    pt, ft = path_tag(tag), file_tag(tag)
    tags = [pt, ft] if pt.lower() != ft.lower() else [pt]
    found = []
    for t in tags:
        base = f"https://github.com/{owner}/{repo}/releases/download/{t}/"
        for name in build_candidates(entry, tag):
            url = base + name
            if head_ok(url):
                found.append((name, url))
    for name in build_candidates(entry, tag):
        if name.endswith(".dll"):
            url = f"https://github.com/{owner}/{repo}/releases/latest/download/{name}"
            if head_ok(url):
                found.append((name, url))
    return found


def main() -> None:
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    test_ids = [
        "LaunchpadReloaded", "StellarRolesAU", "EndlessHostRoles", "AUnlocker",
        "PropHunt", "LotusContinued", "Impostor", "TheOtherRolesGMIA",
        "NewMod", "ExtremeRoles", "VanillaEnhancements", "UnlockDleks",
        "MoreGamemodes", "BetterAmongUs",
    ]
    for mid in test_ids:
        entry = next(m for m in registry["mods"] if m["id"] == mid)
        tag = get_live_tag(entry["githubOwner"], entry["githubRepo"])
        print(f"\n{mid} tag={tag}")
        if tag:
            res = probe(entry, tag)
            for n, u in res[:10]:
                print(f"  {n}")
        time.sleep(0.3)


if __name__ == "__main__":
    main()
