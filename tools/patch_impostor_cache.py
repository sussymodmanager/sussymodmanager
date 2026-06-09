"""Patch Impostor cache entry with known latest asset."""
import json
from datetime import datetime, timezone
from pathlib import Path

CACHE = Path(__file__).resolve().parents[1] / "data" / "mod-cache.json"
cache = json.loads(CACHE.read_text(encoding="utf-8"))
tag = "v1.10.6"
path_tag = "1.10.6"
asset_name = f"Impostor-Server_{path_tag}_win-x64.zip"
asset_url = f"https://github.com/Impostor/Impostor/releases/download/{tag}/{asset_name}"
release = {
    "tag_name": tag,
    "published_at": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
    "prerelease": False,
    "assets": [{"name": asset_name, "browser_download_url": asset_url}],
}
cache.setdefault("mods", {})["Impostor"] = {
    "cachedETag": None,
    "cachedReleaseData": json.dumps(release, separators=(",", ":")),
    "lastChecked": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
}
CACHE.write_text(json.dumps(cache, separators=(",", ":")), encoding="utf-8")
print("patched Impostor ->", tag)
