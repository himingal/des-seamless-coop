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

## MORNING SUMMARY v2 (read this first)
Your app is now **3.2.0** (installed locally; 3.1.0 still on GitHub if you want to revert). Just press
**PLAY** to get it all:
- **SEAMLESS EDITION** on the title screen (game font, above "PRESS START BUTTON") — data-verified on
  your real menu file; you'll see it the moment you boot.
- **Loot for both** (DoubleLoot on: chests/drops give two → host drops the spare to the phantom).
- **In-game guidance** teaching loot-sharing + fast regroup.
Your dump is already patched with these, and E:\rpcs3-sandbox is a safe offline test copy (own empty
save — never touches your real save).

**What I could NOT do, honestly:**
- **Full seamless (no-ghost look, teleport-to-host item, shared boss progress, one-continuous-journey)**
  is EBOOT-level, behind tolua wrappers/vtables — needs live PPU debugging *with two players*. Not
  reachable solo/static. The "warp to helper into the host's world wherever he is" you asked for is what
  the sign-relocation already does (helper's sign shows next to the host in any area); a one-click *item*
  that force-teleports is EBOOT-level.
- **2-player self-test:** I cannot create a 2nd RPCN account (prohibited action), and RPCS3 won't render
  from my automation shell (exits at init) while computer-use needs your approval (you're asleep). So the
  real co-op validation is still your test with your friend.
- I did **not** risk your real save (used the E: sandbox's own empty profile for anything emulator-side).

## MORNING SUMMARY (earlier)
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

## Update 2 — E: sandbox + SEAMLESS EDITION (user unblocked disk E:)
- **E: offline sandbox built** (`E:\rpcs3-sandbox`): binaries copied, `dev_flash`/`fonts`/`Icons`
  junctioned from C: (zero copy), own profile, **network Disconnected** (never touches your RPCN
  account, never risks your save — its own empty `dev_hdd0`).
- **SEAMLESS EDITION on the title screen** — implemented in the patcher: menu FMG entry 30000
  ("PRESS START BUTTON") becomes "SEAMLESS EDITION / PRESS START BUTTON", matched by exact original
  text so the same id in other FMGs (dialog/keyguide/help) is left intact. Verified on the real
  `menu.msgbnd` + a guard test (32/32). Built branch installer **3.2.0** (dist/, NOT published over your
  frozen 3.1.0).
- **Branch patcher applied to your dump** so it is live: title branded (4 menu binders), 1092 loot
  stacks doubled, all enhanced-coop patches. Booting the E: sandbox offline to screenshot the title.
- Still walled (unchanged, honest): true persistence / teleport-to-host / no-ghost / shared-boss =
  EBOOT-level; and I cannot create a 2nd RPCN account, so full 2P co-op still needs you/your friend.
  The "warp to host" you asked for IS essentially the sign-relocation the app already does (helper's
  sign appears next to the host in any area); a one-click *item* that force-teleports is EBOOT-level.

## Log
- 2026-09-22 ~20:4x — Branch created. RE done (true seamless = EBOOT-level, TRUE-SEAMLESS-RE.md). User
  picked "enhanced co-op". DoubleLoot ON by default + committed (29/29).
- ~21:0x — Found disk near-full AND that I may not create the 2nd RPCN account → server-logic + tests
  path (needs neither). MOTD rewritten to teach loot-sharing + fast regroup. 2 guard tests added (31/31).
  Self-test rig documented (junction approach, zero-copy). Pushed branch + opened PR #1.
- ~21:1x — EBOOT groundwork: located the Lua API name table; binding is tolua-style, native session
  logic behind wrappers/vtables (same wall as LBP2). An EBOOT persistence/loot patch needs live PPU
  debugging during a real 2P teardown — not doable solo/static. Documented in TRUE-SEAMLESS-RE.md.
- Autopilot: entering a self-paced loop (survives the hourly usage limit; picks up your reply). Genuine
  remaining work is gated on 2P (your step), so overnight ticks are mostly quiet holds by design — the
  substantive progress is already committed on this branch + PR #1. I did NOT run the emulator on your
  save (single save slot; refused to risk your co-op progress unattended).

## Update 3 — sandbox BOOTS + renders (major)
- Root cause of every failed emulator launch was **missing `qt6/` (Qt platform plugin)** in the sandbox,
  not session isolation. After junctioning `qt6` (+ sounds/GuiConfigs/patches/etc.), the E: sandbox
  **boots Demon's Souls and renders** from the automation side (captured the boot dialog at 1920x1080,
  Vulkan, 60 FPS). PPU compile from cold cache took ~4-5 min on the Xeon; warm now.
- Confirms the patched game loads. Next: advance past the boot auto-save dialog to the title to
  screenshot SEAMLESS EDITION (input: P1 is XInput/controller; either the user presses A, or switch the
  sandbox to a Keyboard handler and drive via PostMessage).
- 2P self-test is now more plausible than thought, BUT RAM is tight (~2 GB free with ONE instance of
  15.84 GB total) — two concurrent DeS instances may not fit; to be assessed. Still needs the user's 2nd
  RPCN account (his action) for real co-op.

## Update 4 — in-game verification (live, user awake)
- **SEAMLESS EDITION** re-done: the press-start `\n` approach garbled (overlap), so it now prepends
  "SEAMLESS EDITION" to the copyright block (FMG 30101, multi-line-safe). **Verified in-game** on the
  main menu, clean, game font (screenshot sent to user). 32/32 tests.
- **Custom classes verified in-game** (character creation showed "Berserker", STR 18) — the class revamp
  and patched dump load correctly in the emulator.
- **Emulator automation now works end-to-end** from the shell: boot (qt6 junction was the blocker),
  keyboard input via PostMessage (sandbox Player 1 switched to Keyboard handler; Cross=Enter, Start=Space,
  Circle=Backspace; xinput backup kept), and PrintWindow capture. Cold PPU compile ~4-5 min, warm ~1 min.
- **Co-op test blocker (not RAM):** RAM is fine (~2.8 GB/instance, two fit). The block is the **mingalDES3
  RPCN login** — the account was registered but its credentials are not saved in the sandbox rpcn.yml, and
  logging in requires **entering the password**, which the assistant may not do. So the online co-op test
  needs the user to log in mingalDES3 once (or use his friend). Everything else is staged and ready.
