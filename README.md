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
- **Game tweaks** for your own dump — one fixed, always-on set, applied automatically when you press PLAY (the originals are backed up).
  - **10 new starting classes** replace the vanilla ones, two per focus, every weapon one-hand-equippable and unisex armor (fits a male or female character):
    Strength (Berserker, Warrior), Dexterity (Samurai, Swordsman), Strength/Dexterity (Knight, Squire), Faith (Cleric, Battle Priest) and Intelligence (Mage, Battle Mage).
  - **Start with both co-op stones**, and the **Stone of Ephemeral Eyes is infinite** so you can turn human to host any time.
  - **Stay in soul form**: no automatic revival after a boss.
  - **Soul form keeps full HP**; **passive MP regeneration** (~1 MP/s) with any chest armor.
  - **Double loot**: every world pickup and drop gives twice as much.
  - **Half-price merchants**, **+25% souls**, **upgrade stones drop ~25%**, **Pure Bladestone 15%**, **+50% equip load**.
  - **Crystal Lizards die in one hit**; the **Red and Blue dragons have half the HP**.
- **World tendency selector** for the host: Pure White, White, Normal, Black or Pure Black.
- **Automatic RPCS3 setup**: RPCN, server redirection, UPnP, skipping the intro videos and registering the game.
- **RPCN account creation inside the app**.
- **Dedicated server mode**: `DesCoop.exe --server --name "My Party"`, for a VPS or an always-on PC.
- **Public server**: one click to play on *The Archstones* with everyone.

## Limits

To place a blue summon sign you must be in **soul form** — this is a rule of the game's executable, not something the tweaks can change (a sign placed in body form is only visible to its owner). So for co-op: the **helper** stays a soul and places the sign; the **host** turns human (one Stone of Ephemeral Eyes) and summons them. The game also ends the session when a boss or the host dies; that logic lives in the PS3 executable too. This project makes rejoining fast (the sign appears next to the host, and you never get auto-reverted out of soul form), but it doesn't remove the disconnect.

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
