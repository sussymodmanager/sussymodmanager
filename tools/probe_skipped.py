import ssl, urllib.request
ctx = ssl.create_default_context()

def try_urls(urls):
    for u in urls:
        req = urllib.request.Request(u, method='HEAD', headers={'User-Agent':'x'})
        try:
            with urllib.request.urlopen(req, context=ctx, timeout=12) as r:
                print('OK', u.split('/')[-1], u)
        except Exception:
            pass

try_urls([
 'https://github.com/Limeau/TownofHost-Optimized/releases/download/v3.0.0/TOHO.dll',
 'https://github.com/Limeau/TownofHost-Optimized/releases/download/v3.0.0/TownofHost-Optimized.dll',
 'https://github.com/Limeau/TownofHost-Optimized/releases/latest/download/TOHO.dll',
 'https://github.com/OhMyGuus/BetterCrewLink/releases/download/v3.1.4/Better-CrewLink-Setup-3.1.4.exe.zip',
 'https://github.com/OhMyGuus/BetterCrewLink/releases/download/v3.1.4/Better-CrewLink-3.1.4.zip',
 'https://github.com/OhMyGuus/BetterCrewLink/releases/download/v3.1.4/BetterCrewLink-3.1.4.zip',
 'https://github.com/rewalo/BetterCrewLink/releases/download/v3.1.5/Better-CrewLink-3.1.5.zip',
 'https://github.com/Devs-Us/Cursed-Among-Us/releases/download/v1.1.0/CursedAmongUs.dll',
 'https://github.com/Impostor/Impostor/releases/download/v1.10.6/Impostor_1.10.6_win-x64.zip',
 'https://github.com/LimeShep/Town-Of-Us/releases/download/v7.1.0/TownOfUs.dll',
 'https://github.com/LimeShep/Town-Of-Us/releases/download/v7.1.0/SyzyfowyTownOfUs.dll',
 'https://github.com/XtraCube/PokemongUs/releases/download/1.0.1/PokeLobby.dll',
 'https://github.com/XtraCube/PokemongUs/releases/download/1.0.1/PokemongUs.dll',
 'https://github.com/WanderingPix/Emojis-in-the-mogus-chat/releases/download/2.0.0/EmojisInTheChat.dll',
 'https://github.com/WanderingPix/Emojis-in-the-mogus-chat/releases/download/2.0.0/Emojis-in-the-mogus-chat.dll',
])
