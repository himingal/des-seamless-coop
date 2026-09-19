# ⚠️ SEAMLESS (TEST) — TESTING, DO NOT DOWNLOAD

This branch (`seamless-test`) is an **experimental sandbox**. It is not a release.
**Do not download or install it expecting a finished feature.** The stable app on
`main` / in the GitHub releases is the one to play with.

## What this is

An attempt at a beefier, Elden-Ring-style "seamless" co-op for Demon's Souls on
RPCS3, kept completely separate from the stable co-op:

- Launched with `DesCoop.exe --seamless` (a separate **"… — Seamless (TEST)"**
  desktop/Start-menu shortcut).
- Uses its **own settings profile** (`descoop-settings.seamless.json`), seeded once
  from your stable profile so you don't reconfigure RPCS3/game/RPCN. Your stable
  `descoop-settings.json` is never touched.
- Applies a beefier patch **preset**: Blue Eye Stone usable in **body (human) form**,
  and duplicated loot **OFF**.

## What actually works here today

- Place a summon sign **without dying first** (Blue Eye Stone in human form).
- **Persistent co-op (NEW):** the summoned blue phantom is **not sent home after a
  boss** and **not sent home when the host dies**. Found by reverse-engineering the
  game: the teardown is a single `proxy:WarpNextStageKick();` call in the game's own
  Lua (`BlockClear2_3` = boss/area clear, `HostDead_1` = host death), so it is
  commented out in every `m*.luabnd` — a plain-Lua edit with a pristine backup,
  fully reversible, **no EBOOT memory patching**. Experimental: needs in-game
  testing with a friend (keeping the phantom past a boss transition may desync).
- Everything the stable app already does: sign relocated next to the host, full-HP
  soul form, infinite Ephemeral Eyes, stay-in-soul-form.
- Loot doubling is off (raw single pickups).
- **Blacksmith Ed bonus stock (NEW):** Ed also sells the world-tendency-locked
  rarities so a co-op run never misses them — Talisman of Beasts, Phosphorescent
  Pole, Dragon Bone Smasher, Magic Sword "Makoto", Istarelle, Blind, Large Sword of
  Moonlight, Blueblood Sword, Monk's Head Wrappings, Colorless Demon's Soul (10k
  each) and every Pure upgrade stone. Added as extra `ShopLineupParam` rows in Ed's
  own menu (ids 5005+), no tendency/flag gate. Pure param, pristine backup,
  reversible, no ESD (safer than the Noble Lady route).
- **MP regen at 1 per 2 s (NEW):** the passive mana regen ticks every 2 seconds in
  this profile instead of every second (stable keeps 1/s).

## What still does NOT work (and why)

The RE changed the picture: "no teardown" turned out to be a Lua call, so it's done
above. These three remain out of reach — they live in the **PS3 executable (EBOOT)**
or each player's **own save file**:

| Wanted | Where it lives | Status |
|---|---|---|
| Summon / be summoned in **any form** | EBOOT summon handshake (client-side form check) | ❌ needs EBOOT patch |
| **Both** players pick up world items | pickup writes the **host's** save; the guest's inventory is a separate save on their PC | ❌ needs save sync |
| **Shared boss progression** | boss-defeat flags are per-save on each PC | ❌ needs save sync |

The form check is C++ in the EBOOT; the two save-level ones need an inventory/flag
sync channel that doesn't exist yet. Guessing PPU patch addresses without verified
codes **crashes RPCS3 and can corrupt saves**, so this build ships no fake EBOOT
patches — only the verified, reversible Lua edits above.

## Reverse-engineering notes

The decrypted EBOOT (`rpcs3.exe --decrypt EBOOT.BIN`) keeps full debug symbols for
the online session state machine (`OnBeSummoned_White`, `SummonSuccess_White`,
`HostDead`, `BlockClear2_*`, `IsWhiteGhost`/`IsGreyGhost`/`IsBlackGhost`,
`WarpNextStageKick`, `SetSosSignPos`, `ClearSosSign`) and the original source paths
(`N:/DemonsSoul/Source/...`). The session logic that ends co-op is driven from the
per-map `global_event.lua` inside each `m*.luabnd`, which is plaintext and patchable
— that is how both "stay in soul form" and this "persistent co-op" work.

**Bottom line:** treat this as a scratchpad for the seamless experiment, not a build to hand to a friend.
