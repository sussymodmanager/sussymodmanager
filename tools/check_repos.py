import urllib.request, ssl, json
ctx = ssl.create_default_context()
repos = [
 ("DigiWorm0","LevelImposter"),
 ("Dolly1016","Nebula"),
 ("Zeo666","AllTheRoles"),
 ("xChipseq","ChaosTokens"),
 ("All-Of-Us-Mods","MiraAPI"),
]
for owner, repo in repos:
    url = f"https://github.com/{owner}/{repo}/releases/latest"
    req = urllib.request.Request(url, method="HEAD", headers={"User-Agent": "test"})
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=15) as r:
            print(f"{owner}/{repo}: final={r.geturl()}")
    except Exception as ex:
        print(f"{owner}/{repo}: {ex}")

    api = f"https://api.github.com/repos/{owner}/{repo}/releases/latest"
    try:
        req2 = urllib.request.Request(api, headers={"User-Agent": "test", "Accept": "application/vnd.github+json"})
        with urllib.request.urlopen(req2, context=ctx, timeout=15) as r2:
            data = json.loads(r2.read())
            assets = [a["name"] for a in data.get("assets", [])]
            print(f"  tag={data.get('tag_name')} assets={assets[:8]}")
    except Exception as ex:
        print(f"  api: {ex}")
