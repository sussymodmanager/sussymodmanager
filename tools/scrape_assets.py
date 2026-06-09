import re
import ssl
import urllib.request

ctx = ssl.create_default_context()

for owner, repo, tag in [
    ("OhMyGuus", "BetterCrewLink", "v3.1.4"),
    ("rewalo", "BetterCrewLink", "v3.1.5"),
    ("Impostor", "Impostor", "v1.10.6"),
    ("XtraCube", "PokemongUs", "1.0.1"),
    ("WanderingPix", "Emojis-in-the-mogus-chat", "2.0.0"),
]:
    url = f"https://github.com/{owner}/{repo}/releases/expanded_assets/{tag}"
    req = urllib.request.Request(url, headers={"User-Agent": "Mozilla/5.0"})
    try:
        html = urllib.request.urlopen(req, context=ctx, timeout=20).read().decode("utf-8", "replace")
        names = re.findall(r"/releases/download/[^/\"]+/([^\"?#]+)", html)
        print(f"{owner}/{repo}:")
        for name in dict.fromkeys(names):
            print(f"  {name}")
    except Exception as ex:
        print(f"{owner}/{repo}: {ex}")
