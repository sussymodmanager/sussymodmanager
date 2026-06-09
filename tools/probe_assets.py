"""Probe direct GitHub release download URLs for mod asset patterns."""
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
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "SussyModManager-probe"})
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


def probe_mod(entry: dict, tag: str) -> list[tuple[str, str]]:
    owner, repo = entry["githubOwner"], entry["githubRepo"]
    pt, ft = path_tag(tag), file_tag(tag)
    tags = [pt, ft] if pt.lower() != ft.lower() else [pt]
    found: list[tuple[str, str]] = []
    filters = entry.get("assetFilters") or {}

    dll_names = []
    for filt in filters.values():
        if not filt:
            continue
        for pat in filt.get("patterns") or []:
            if pat.endswith(".dll") and not pat.startswith("."):
                dll_names.append(pat)

    for ch, filt in filters.items():
        if not filt:
            continue
        patterns = filt.get("patterns") or []
        for pat in patterns:
            if pat in (".dll", ".zip"):
                continue
            if ch == "dll" and pat.endswith(".dll"):
                for t in tags:
                    url = f"https://github.com/{owner}/{repo}/releases/download/{t}/{pat}"
                    if head_ok(url):
                        found.append((pat, url))
                latest = f"https://github.com/{owner}/{repo}/releases/latest/download/{pat}"
                if head_ok(latest):
                    found.append((pat, latest))
                continue

            candidates = []
            for t in tags:
                candidates.extend([
                    f"{pat}.zip",
                    f"{repo}-{t}-{pat}.zip",
                    f"{repo}-{pat}-{t}.zip",
                    f"{repo}-{ft}-{pat}.zip",
                    f"{repo}-{pt}-{pat}.zip",
                    f"{pat}-{t}.zip",
                    f"{t}-{pat}.zip",
                ])
                if "steam" in pat.lower() or "itch" in pat.lower():
                    candidates.append(f"{repo}-{ft}-x86-steam-itch.zip")
                    candidates.append(f"{repo}-{pt}-x86-steam-itch.zip")
                    candidates.append(f"TouMira-{ft}-x86-steam-itch.zip")
                if "epic" in pat.lower() or "msstore" in pat.lower():
                    candidates.append(f"{repo}-{ft}-x64-epic-msstore.zip")
                    candidates.append(f"{repo}-{pt}-x64-epic-msstore.zip")
                    candidates.append(f"TouMira-{ft}-x64-epic-msstore.zip")

            for t in tags:
                base = f"https://github.com/{owner}/{repo}/releases/download/{t}/"
                for name in dict.fromkeys(candidates):
                    url = base + name
                    if head_ok(url):
                        found.append((name, url))

    for dll in dict.fromkeys(dll_names):
        latest = f"https://github.com/{owner}/{repo}/releases/latest/download/{dll}"
        if head_ok(latest):
            found.append((dll, latest))

    return found


def main() -> None:
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    test_ids = [
        "ChaosTokens", "AllTheRoles", "MiraAPI", "LaunchpadReloaded",
        "StellarRolesAU", "TownOfUs", "EndlessHostRoles", "AUnlocker",
        "PropHunt", "LotusContinued", "BetterPolus", "Impostor",
    ]
    for mid in test_ids:
        entry = next(m for m in registry["mods"] if m["id"] == mid)
        tag = get_live_tag(entry["githubOwner"], entry["githubRepo"])
        print(f"\n{mid} tag={tag}")
        if tag:
            for name, url in probe_mod(entry, tag):
                print(f"  OK {name} -> {url}")
        time.sleep(0.4)


if __name__ == "__main__":
    main()
