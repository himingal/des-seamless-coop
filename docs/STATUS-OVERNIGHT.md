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

## Rig reality check (important)
Two-emulator self-testing needs **two RPCN accounts** (two online identities). **Creating an account is
a prohibited action for me** — I will not sign up the 2nd RPCN account even with permission. So true
in-emulator 2P testing needs the user (a 2nd RPCN account, or his friend). Combined with the near-full
disk, I am NOT booting two DeS emulators tonight.

**What I CAN test solo (and will):** the party-server / co-op *logic*, via the in-process integration
tests (`SignEndToEndTests` spins a real `DesServer` and drives host+friend over loopback — no RPCN, no
emulator, no account). All new server behaviors get covered there. In-game behavior is staged with
heavy logging for the user's real 2P test. I will also leave a ready-to-run second-instance launcher
(junctions, zero-copy) so the user only has to plug in a 2nd RPCN account later.

## MORNING SUMMARY (read this first)
Delivered on branch `true-seamless` (all tested, 31/31 green, committed):
1. **Loot for both** — `DoubleLoot` ON by default: every chest/drop gives two, the host drops the spare
   to the phantom, so you both keep the items. Souls were already shared. (Boldwin also still sells the
   rarities so nothing is ever permanently missed.)
2. **In-game guidance** — the co-op notice you see on connecting now teaches the loot-sharing (drop the
   dupe) and the fast regroup (Cyanide Pill → Blue Eye Stone → host touches the sign).
3. **Guard tests** — lock the enhanced-coop defaults and the guidance so they can't silently regress.
4. **RE write-up** (`TRUE-SEAMLESS-RE.md`) + **self-test rig guide** (`SELF-TEST-RIG.md`).

Honest ceiling: "true" persistence (phantom survives boss/host-death, shared Nexus/world) is **engine
(EBOOT) level** and not reachable via Lua/server — proven, documented. What ships here makes the
disconnect quick and invisible instead of removing it. **Please test the loot-sharing + guidance with
your friend**; that is the real validation I could not do solo (2nd RPCN account = a step only you can do).

## Log
- 2026-09-22 ~20:4x — Branch created. RE done (true seamless = EBOOT-level, TRUE-SEAMLESS-RE.md). User
  picked "enhanced co-op". DoubleLoot ON by default + committed (29/29).
- ~21:0x — Found disk near-full AND that I may not create the 2nd RPCN account → server-logic + tests
  path (needs neither). MOTD rewritten to teach loot-sharing + fast regroup. 2 guard tests added (31/31).
  Self-test rig documented (junction approach, zero-copy). Pushing branch + opening PR for review.
