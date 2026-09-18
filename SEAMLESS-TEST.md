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
- Everything the stable app already does: sign relocated next to the host, full-HP
  soul form, infinite Ephemeral Eyes, stay-in-soul-form.
- Loot doubling is off (raw single pickups).

## What does NOT work (and why)

These are the real "seamless" behaviours, and they live in the **PS3 game
executable (EBOOT)** and in each player's **own save file** — places the launcher,
the reimplemented server and param edits cannot reach:

| Wanted | Where it lives | Status |
|---|---|---|
| Summon / be summoned in **any form** | EBOOT summon handshake (client-side form check) | ❌ needs EBOOT patch |
| **No session teardown** on boss / host death | EBOOT sends the phantom home on boss clear | ❌ needs EBOOT patch |
| **Both** players pick up world items | pickup writes the **host's** save; the guest's inventory is a separate save on their PC | ❌ needs save sync |
| **Shared boss progression** | boss-defeat flags are per-save on each PC | ❌ needs save sync |

Doing these properly means reverse-engineering the Demon's Souls EBOOT and hooking
game code at runtime (the way the Elden Ring Seamless Co-op mod hooks the PC exe),
plus an inventory/flag sync channel that doesn't exist yet. Guessing PPU patch
addresses without verified codes **crashes RPCS3 and can corrupt saves**, so this
build does not ship fake patches — the plumbing is here, the verified game-code
patches are not.

**Bottom line:** treat this as a scratchpad for the seamless experiment, not a build to hand to a friend.
