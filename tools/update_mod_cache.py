"""Rebuild bundled mod-cache.json from live GitHub redirects + asset probing."""
import json
import re
import ssl
import time
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REGISTRY = ROOT / "data" / "mod-registry.json"
CACHE = ROOT / "data" / "mod-cache.json"
ctx = ssl.create_default_context()


def path_tag(tag: str) -> str:
    t = tag.strip()
    return t[1:] if t.lower().startswith("v") else t


def file_tag(tag: str) -> str:
    t = tag.strip()
    return t if t.lower().startswith("v") else "v" + t


def head_ok(url: str) -> bool:
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "SussyModManager-cache"})
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=20) as resp:
            return resp.status < 400
    except urllib.error.HTTPError as e:
        return e.code in (200, 302)
    except Exception:
        return False


def get_redirect_tag(owner: str, repo: str) -> str | None:
    url = f"https://github.com/{owner}/{repo}/releases/latest"
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "SussyModManager-cache"})
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=20) as resp:
            loc = resp.url
    except Exception:
        return get_atom_tag(owner, repo)

    m = re.search(r"/releases/tag/([^/?#]+)", loc or "")
    if m:
        return m.group(1)
    return get_atom_tag(owner, repo)


def get_atom_tag(owner: str, repo: str) -> str | None:
    url = f"https://github.com/{owner}/{repo}/releases.atom"
    req = urllib.request.Request(url, headers={"User-Agent": "SussyModManager-cache"})
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=20) as resp:
            xml = resp.read()
        root = ET.fromstring(xml)
        ns = {"a": "http://www.w3.org/2005/Atom"}
        entry = root.find("a:entry", ns)
        if entry is None:
            return None
        link = entry.find("a:link", ns)
        href = link.attrib.get("href", "") if link is not None else ""
        m = re.search(r"/releases/tag/([^/?#]+)", href)
        return m.group(1) if m else None
    except Exception:
        return None


def build_repo_names(entry: dict, path_t: str, file_t: str, tag_token: str) -> list[str]:
    repo = entry["githubRepo"]
    names = []

    specials = {
        "TOU-Mira": [
            f"TouMira-{file_t}-x86-steam-itch.zip",
            f"TouMira-{file_t}-x64-epic-msstore.zip",
            "TownOfUsMira.dll",
        ],
        "StellarRolesAU": ["StellarRoles.Steam.zip", "StellarRoles.EpicGames.zip", "StellarRoles.dll"],
        "EndlessHostRoles": [
            f"EHR.v{tag_token}_Steam.zip",
            f"EHR.v{tag_token}_Epic-Games_Microsoft-Store.zip",
            "EHR.dll",
        ],
        "AUnlocker": [
            f"AUnlocker_{file_t}_Steam_Itch.zip",
            f"AUnlocker_{file_t}_EpicGames_MicrosoftStore_XboxApp.zip",
            f"AUnlocker_{file_t}.dll",
            "AUnlocker.dll",
        ],
        "TownofHost-Optimized": ["TOHO.dll"],
        "BetterCrewLink": ["Better-CrewLink-Unpacked-x64.zip"],
        "Impostor": [
            f"Impostor-Server_{tag_token}_win-x64.zip",
            f"Impostor-Server_{file_t}_win-x64.zip",
        ],
        "PokemongUs": ["PokeLobby.dll"],
        "Emojis-in-the-mogus-chat": ["Emojis.dll"],
        "Town-Of-Us": [
            f"Syzyfowy.Town.Of.Us.{tag_token}.x32.zip",
            f"Syzyfowy.Town.Of.Us.{tag_token}.x64.zip",
            f"Syzyfowy.Town.Of.Us.{tag_token}.c432.Desktop.dll",
        ],
        "Cursed-Among-Us": ["CursedAmongUs.dll"],
        "LotusContinued": [
            f"Lotus.v{tag_token}.Steam.zip",
            f"Lotus.v{tag_token}.Epic+MicrosoftStore.zip",
            "Lotus.dll",
        ],
        "LevelImposter": ["LevelImposter.zip"],
        "Impostor": ["win-x64.zip", f"Impostor-{tag_token}-win-x64.zip"],
        "LaunchpadReloaded": ["LaunchpadReloaded.dll"],
        "BetterAmongUs-Public": [f"BAU-SteamItchio-{file_t}.zip", f"BAU-EpicMsStore-{file_t}.zip"],
        "MoreGamemodes": ["More-Gamemodes-SteamItchio.zip", "More-Gamemodes-EpicMsstore.zip"],
        "ExtremeRoles": [f"STEAM_ONLY_ExtremeRoles-{file_t}.zip", f"ExtremeRoles-{file_t}.zip"],
    }
    names.extend(specials.get(repo, []))
    names.extend([f"{repo}.zip", f"{repo}-{tag_token}.zip", f"{repo}.dll"])

    filters = entry.get("assetFilters") or {}
    for ch, filt in filters.items():
        if not filt:
            continue
        for pat in filt.get("patterns") or []:
            if pat in (".dll", ".zip"):
                if ch == "dll" and pat == ".dll":
                    names.append(f"{repo}.dll")
                continue
            if ch == "dll" and pat.endswith(".dll"):
                names.append(pat)
                continue
            if filt.get("exactMatch") and pat.endswith(".zip"):
                names.append(pat)
                continue
            names.extend([
                f"{repo}-{tag_token}-{pat}.zip",
                f"{repo}-{file_t}-{pat}.zip",
                f"{pat}.zip",
            ])
            if any(x in pat.lower() for x in ("steam", "itch")):
                names.append(f"{repo}-{tag_token}-x86-steam-itch.zip")
            if any(x in pat.lower() for x in ("epic", "msstore", "microsoft")):
                names.append(f"{repo}-{tag_token}-x64-epic-msstore.zip")

    return list(dict.fromkeys(names))


def probe_assets(entry: dict, tag: str) -> list[dict]:
    owner, repo = entry["githubOwner"], entry["githubRepo"]
    pt, ft = path_tag(tag), file_tag(tag)
    tags = [pt, ft] if pt.lower() != ft.lower() else [pt]
    assets = []
    seen = set()

    for tag_token in tags:
        base = f"https://github.com/{owner}/{repo}/releases/download/{tag_token}/"
        for name in build_repo_names(entry, pt, ft, tag_token):
            url = base + name
            if url in seen:
                continue
            seen.add(url)
            if head_ok(url):
                assets.append({"name": name, "browser_download_url": url})

        for name in build_repo_names(entry, pt, ft, tag_token):
            if not name.endswith(".dll"):
                continue
            latest = f"https://github.com/{owner}/{repo}/releases/latest/download/{name}"
            if latest in seen:
                continue
            seen.add(latest)
            if head_ok(latest):
                assets.append({"name": name, "browser_download_url": latest})

    return assets


def build_release(tag: str, assets: list[dict]) -> dict:
    return {
        "tag_name": tag,
        "published_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        "prerelease": False,
        "assets": assets,
    }


def main() -> None:
    registry = json.loads(REGISTRY.read_text(encoding="utf-8"))
    cache = json.loads(CACHE.read_text(encoding="utf-8"))
    mods_cache = cache.setdefault("mods", {})

    updated = 0
    skipped = []

    for entry in registry["mods"]:
        if entry.get("source") == "thunderstore":
            continue
        owner = entry.get("githubOwner")
        repo = entry.get("githubRepo")
        if not owner or not repo:
            continue

        mod_id = entry["id"]
        tag = get_redirect_tag(owner, repo)
        if not tag:
            skipped.append(mod_id)
            continue

        assets = probe_assets(entry, tag)
        if not assets:
            skipped.append(mod_id)
            continue

        release = build_release(tag, assets)
        mods_cache[mod_id] = {
            "cachedETag": None,
            "cachedReleaseData": json.dumps(release, separators=(",", ":")),
            "lastChecked": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
        }
        updated += 1
        print(f"updated {mod_id} -> {tag} ({len(assets)} assets)")
        time.sleep(0.2)

    cache["version"] = "1.0"
    CACHE.write_text(json.dumps(cache, separators=(",", ":")), encoding="utf-8")
    print(f"\nDone: updated {updated}, skipped {len(skipped)}")
    if skipped:
        print("skipped:", ", ".join(skipped))


if __name__ == "__main__":
    main()
