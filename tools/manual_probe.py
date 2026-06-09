import ssl, urllib.request
ctx = ssl.create_default_context()

def try_urls(urls):
    for u in urls:
        req = urllib.request.Request(u, method='HEAD', headers={'User-Agent':'x'})
        try:
            with urllib.request.urlopen(req, context=ctx, timeout=12) as r:
                print('OK', u)
        except Exception as e:
            pass

# LaunchpadReloaded 0.3.8
try_urls([
 'https://github.com/All-Of-Us-Mods/LaunchpadReloaded/releases/download/0.3.8/LaunchpadReloaded.dll',
 'https://github.com/All-Of-Us-Mods/LaunchpadReloaded/releases/download/0.3.8/Launchpad.dll',
 'https://github.com/All-Of-Us-Mods/LaunchpadReloaded/releases/latest/download/LaunchpadReloaded.dll',
])

# StellarRolesAU v2026.6.8
try_urls([
 'https://github.com/Mr-Fluuff/StellarRolesAU/releases/download/v2026.6.8/StellarRolesAU-v2026.6.8.zip',
 'https://github.com/Mr-Fluuff/StellarRolesAU/releases/download/v2026.6.8/StellarRoles-v2026.6.8.zip',
 'https://github.com/Mr-Fluuff/StellarRolesAU/releases/download/v2026.6.8/StellarRolesAU.zip',
 'https://github.com/Mr-Fluuff/StellarRolesAU/releases/download/2026.6.8/StellarRolesAU-v2026.6.8.zip',
])

# EndlessHostRoles v7.5.1
try_urls([
 'https://github.com/Gurge44/EndlessHostRoles/releases/download/v7.5.1/EndlessHostRoles-v7.5.1_Steam.zip',
 'https://github.com/Gurge44/EndlessHostRoles/releases/download/v7.5.1/EndlessHostRoles-v7.5.1-Steam.zip',
 'https://github.com/Gurge44/EndlessHostRoles/releases/download/v7.5.1/EndlessHostRoles_Steam.zip',
 'https://github.com/Gurge44/EndlessHostRoles/releases/download/v7.5.1/EHR-v7.5.1_Steam.zip',
 'https://github.com/Gurge44/EndlessHostRoles/releases/download/v7.5.1/EndlessHostRoles-v7.5.1.zip',
])

# AUnlocker v1.3.0
try_urls([
 'https://github.com/astra1dev/AUnlocker/releases/download/v1.3.0/AUnlocker-v1.3.0-Steam_Itch.zip',
 'https://github.com/astra1dev/AUnlocker/releases/download/v1.3.0/AUnlocker-v1.3.0-SteamItchio.zip',
 'https://github.com/astra1dev/AUnlocker/releases/download/v1.3.0/AUnlocker_Steam_Itch.zip',
 'https://github.com/astra1dev/AUnlocker/releases/download/v1.3.0/AUnlocker-v1.3.0-EpicGames_MicrosoftStore_XboxApp.zip',
 'https://github.com/astra1dev/AUnlocker/releases/download/v1.3.0/AUnlocker.dll',
])

# PropHunt v2026.2.23
try_urls([
 'https://github.com/ugackMiner53/PropHunt/releases/download/v2026.2.23/PropHunt-v2026.2.23.zip',
 'https://github.com/ugackMiner53/PropHunt/releases/download/v2026.2.23/PropHunt.zip',
 'https://github.com/ugackMiner53/PropHunt/releases/download/v2026.2.23/PropHunt.dll',
])

# LotusContinued v1.7.2
try_urls([
 'https://github.com/Lotus-AU/LotusContinued/releases/download/v1.7.2/LotusContinued-v1.7.2.zip',
 'https://github.com/Lotus-AU/LotusContinued/releases/download/v1.7.2/LotusContinued-v1.7.2-Steam.zip',
 'https://github.com/Lotus-AU/LotusContinued/releases/download/v1.7.2/LotusContinued-v1.7.2-Epic.zip',
])

# Impostor v1.10.6
try_urls([
 'https://github.com/Impostor/Impostor/releases/download/v1.10.6/Impostor-1.10.6-win-x64.zip',
 'https://github.com/Impostor/Impostor/releases/download/v1.10.6/Impostor_1.10.6_win-x64.zip',
 'https://github.com/Impostor/Impostor/releases/download/v1.10.6/win-x64.zip',
 'https://github.com/Impostor/Impostor/releases/download/v1.10.6/Impostor-win-x64.zip',
])

# TheOtherRolesGMIA 1.3.7
try_urls([
 'https://github.com/dabao40/TheOtherRolesGMIA/releases/download/1.3.7/TheOtherRolesGMIA-1.3.7.zip',
 'https://github.com/dabao40/TheOtherRolesGMIA/releases/download/1.3.7/TheOtherRolesGMIA.zip',
 'https://github.com/dabao40/TheOtherRolesGMIA/releases/download/1.3.7/theotherrolesgmia.zip',
])

# Launchpad - list common dll names from repo
try_urls([
 'https://github.com/All-Of-Us-Mods/LaunchpadReloaded/releases/download/0.3.8/LaunchpadReloaded.dll',
 'https://github.com/All-Of-Us-Mods/LaunchpadReloaded/releases/download/0.3.8/Launchpad-Reloaded.dll',
])
