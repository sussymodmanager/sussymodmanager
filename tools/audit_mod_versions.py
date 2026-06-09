#!/usr/bin/env python3
"""Compare bundled mod-cache tags against GitHub /releases/latest redirects."""
import json
import re
import ssl
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "data" / "mod-registry.json"
CACHE = ROOT / "data" / "mod-cache.json"


def get_cached_tag(mod_id: str, cache: dict) -> str | None:
    entry = cache.get("mods", {}).get(mod_id)
    if not entry:
        return None
    crd = entry.get("cachedReleaseData")
    if not crd:
        return None
    try:
        return json.loads(crd).get("tag_name")
    except json.JSONDecodeError:
        return None


def get_live_tag(owner: str, repo: str) -> str:
    url = f"https://github.com/{owner}/{repo}/releases/latest"
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "SussyModManager-Audit/1.0"})
    ctx = ssl.create_default_context()
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=20) as resp:
            loc = resp.geturl()
    except urllib.error.HTTPError as e:
        if e.code in (301, 302, 303, 307, 308):
            loc = e.headers.get("Location", "")
        elif e.code == 404:
            return "NO_RELEASES"
        else:
            return f"HTTP_{e.code}"
    except Exception as ex:
        return f"ERROR:{type(ex).__name__}"

    m = re.search(r"/releases/tag/([^/?#]+)", loc or "")
    return m.group(1) if m else f"NO_TAG:{loc}"


def normalize_tag(t: str | None) -> str | None:
    if not t or t.startswith(("HTTP", "ERROR", "NO_")):
        return t
    t = t.strip()
    return t[1:] if t.lower().startswith("v") else t


def main() -> None:
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    cache = json.loads(CACHE.read_text(encoding="utf-8"))

    rows = []
    github_mods = []
    for m in registry["mods"]:
        owner = m.get("githubOwner")
        repo = m.get("githubRepo")
        if not owner or not repo:
            continue
        github_mods.append(m)

    for m in github_mods:
        mod_id = m["id"]
        owner = m["githubOwner"]
        repo = m["githubRepo"]
        cached = get_cached_tag(mod_id, cache)
        live = get_live_tag(owner, repo)
        cached_disp = cached or "MISSING"

        if live.startswith(("HTTP", "ERROR", "NO_RELEASES", "NO_TAG")):
            status = live
        elif cached is None:
            status = "MISSING_CACHE"
        elif normalize_tag(cached) == normalize_tag(live):
            status = "MATCH"
        else:
            status = "MISMATCH"

        rows.append((mod_id, f"{owner}/{repo}", cached_disp, live, status))
        time.sleep(0.25)

    print("modId|repo|cachedTag|liveRedirectTag|status")
    print("-" * 90)
    for row in rows:
        print("|".join(row))

    match = sum(1 for r in rows if r[4] == "MATCH")
    mismatch = sum(1 for r in rows if r[4] == "MISMATCH")
    missing = sum(1 for r in rows if r[4] == "MISSING_CACHE")
    errors = sum(1 for r in rows if r[4] not in ("MATCH", "MISMATCH", "MISSING_CACHE"))

    print()
    print(f"Total GitHub mods: {len(rows)}")
    print(f"MATCH: {match}, MISMATCH: {mismatch}, MISSING_CACHE: {missing}, OTHER: {errors}")


if __name__ == "__main__":
    main()
