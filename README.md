<p align="center"><img src="docs/banner.png" alt="DeS Seamless Co-op" width="760"></p>

<h1 align="center">DeS Seamless Co-op</h1>
<p align="center"><b>Seamless co-op for Demon's Souls on RPCS3.</b><br>One of you hosts, the other pastes a code, and you play together for as long as you want.</p>

<p align="center"><a href="../../releases/latest"><b>⬇ Download the installer</b></a></p>

---

## Setup (once)

1. Run **`DesSeamlessCoop-Setup.exe`** and point it at your Demon's Souls folder.
   It downloads RPCS3 and the official PS3 firmware, patches the game and configures everything by itself.
   <p align="center"><img src="docs/setup.png" alt="Automatic setup" width="460"></p>
2. Open the app and create your free **RPCN** account (RPCS3's PlayStation Network). It's a small form, and a token arrives by email.

## Play

| Host | Friend |
|---|---|
| **Host a Party**, then tell your friend the **party name + password** it shows | **Join a Party**, type that name + password and click **Join** |
| **PLAY** | **PLAY** |

In game, the helper uses the **Blue Eye Stone** anywhere. Their sign shows up **right next to the host, in whatever area the host is in**. Touch it and you're together.
After a boss, use the Blue Eye Stone again and keep going.

No VPN, no port forwarding and no router setup. The app connects directly when it can (LAN, VPN, UPnP). If it can't, it goes through a free public relay by itself. The party name defaults to your RPCN name, and the next time you open the app it hosts or rejoins automatically.

## What it does

- **Built-in party server.** It reimplements the Demon's Souls online server and runs inside the host's app, so there's no VPS to rent.
  - Blue signs from party members are **moved next to the host in any area**, using the positions the game itself reports.
  - No level range. US, EU and JP copies share one world. Messages, bloodstains and wandering ghosts all work.
  - A live list shows who is online, where they are, and who has a sign down or is in co-op.
- **Game tweaks** for your own dump. Each one is a checkbox, the originals are backed up, and **Restore** undoes everything.
  - The **Blue Eye Stone works in body form**, so nobody has to die to go back to helping.
  - The **Stone of Ephemeral Eyes is never consumed**, so the host can always get their body back to summon.
  - **Soul form keeps full HP**: dying no longer halves your max HP, so soul form plays like body form.
  - **Stay in soul form**: no automatic revival after a boss (as host or as a blue phantom going home), so the Blue Eye Stone is always ready. The Stone of Ephemeral Eyes still revives you when you want to summon.
  - **Start with both co-op stones**: every new character carries a Blue Eye Stone and a Stone of Ephemeral Eyes.
  - **10 new starting classes** replace the vanilla ones, with new names, stats and full kits. Every weapon is usable one-handed from the start and Soul Levels stay between 1 and 9:
    Sellsword, Sentinel, Tracker, Friar, Sorcerer, Blade Dancer, Juggernaut, Nightblade, Crusader and Exiled Heir. Hover the option in the app to see each kit.
  - **Half-price merchants**: everything sold for souls costs 50% (items bought with boss souls are unchanged).
  - **Pure Bladestone** drops from the Shrine of Storms skeletons **15%** of the time instead of 0.5%.
  - **+50% equip load and item burden**: weapons, armor, rings and items weigh a third less.
- **World tendency selector** for the host: Pure White, White, Normal, Black or Pure Black. The party server pushes every world toward it.
- **Game status badge** that shows whether Demon's Souls is running.
- **Automatic RPCS3 setup**: RPCN, server redirection, UPnP, skipping the intro videos and registering the game.
- **RPCN account creation inside the app**, with no digging through RPCS3's menus.
- **Dedicated server mode**: `DesCoop.exe --server --name "My Party"`, for a VPS or an always-on PC.
- **Public server**: one click to play on *The Archstones* with everyone.

## Limits

The game still ends the session when a boss dies or the host dies. That logic lives in the PS3 executable. This project turns rejoining into two clicks (Blue Eye Stone in body form, plus a sign that appears next to the host), but it doesn't prevent the disconnect.

## Build

```powershell
git clone --recursive https://github.com/himingal/des-seamless-coop
powershell -File tools/build-release.ps1 -Version 1.1.0   # tests + single-file exe + installer in dist/
```

## Credits

- Server protocol: [DeSSE](https://github.com/ymgve/desse) by ymgve and [dessego](https://github.com/danmrichards/dessego)
- [RPCS3](https://rpcs3.net) and [RPCN](https://github.com/RipleyTom/rpcn)
- [SoulsFormatsNEXT](https://github.com/soulsmods/SoulsFormatsNEXT) (GPL-3.0) and paramdefs from [Paramdex](https://github.com/soulsmods/Paramdex)
- [The Archstones](https://thearchstones.com)
- [Cinzel](https://github.com/NDISCOVER/Cinzel-Font) typeface (SIL OFL 1.1)

Fan project, not affiliated with FromSoftware, Bluepoint, Sony or the RPCS3 team. It ships no game files: use your own copy. Licensed under GPL-3.0.
