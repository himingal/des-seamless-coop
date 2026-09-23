# Overnight autonomous session — status log

Branch `true-seamless`. User is asleep; authorized full autonomous work on the PC overnight, told me
to self-direct, keep progress, and auto-resume after usage limits. Goal: **enhanced ("turbinado")
co-op** — loot for both, instant regroup — plus a **self-test rig** (two instances on one PC so I can
co-op with myself and actually validate in-game).

This file is updated every loop iteration so there is a readable trail by morning.

## Hard constraints discovered
- **Disk: ~7.5 GB free on C: (98% full).** The RPCS3 folder is 7.9 GB → **cannot duplicate it.** Second
  instance must use **directory junctions** (share firmware/binaries at ~0 extra space) + its own small
  config / PS3 user / RPCN account / cache. **I monitor `df` every iteration and ABORT the rig if free
  space drops below 3 GB** — never fill the user's system disk.
- Two concurrent DeS emulators on RX 6600 / Xeon E5-2650 v3 is heavy; expect low FPS. Fine for logic
  testing (summon, teardown, loot), not for performance.
- Drive each game window with `PostMessage(WM_KEYDOWN/WM_KEYUP)` to its hwnd (no focus theft) — proven
  technique from the LBP2 work.

## Plan (self-directed, revisable)
1. [done] Loot for both: `DoubleLoot` ON by default (host drops the dupe to the phantom). 29/29 tests pass.
2. [in progress] Self-test rig: instance2 via junctions, own RPCN account, both DeS booting side by side.
3. Automate host+join to in-game via PostMessage; confirm the party connects (server.log: ghosts/signs).
4. With a working 2P rig, test & iterate on loot + instant-regroup for real.
5. Fallbacks if the rig is infeasible (disk/perf): finish all code + in-game tutorial improvements and
   stage everything with heavy logging for the user's own 2P test with his friend.

## Log
- 2026-09-22 ~20:4x — Branch created. RE done (true seamless = EBOOT-level, documented in
  TRUE-SEAMLESS-RE.md). User picked "enhanced co-op". DoubleLoot enabled by default + committed.
  Found disk near-full; rig will use junctions. Starting rig setup.
