# True Seamless Co-op — RE findings & plan

Investigation on branch `true-seamless` (2026-09-22). Goal the user asked for: a phantom that
**persists** across boss kills and host death, **picks up items**, and **travels to the Nexus with
the host** — "true" seamless like the Elden Ring Seamless Co-op mod.

## What the engine actually does (evidence)

Decrypted the retail EBOOT (`rpcs3 --decrypt`): it is **stripped** — 0 symbols, so no named-function
RE. Pivoted to the Lua event layer (`script/m*.luabnd`, plain-text source, 451 `proxy:` primitives).

Demon's Souls co-op is a **one-block guest session**, not a shared world:

- The blue/white phantom is loaded into the **host's** world for a **single block**.
- On boss/area clear the phantom runs `BlockClear2 → … → BlockClear2_3 → proxy:WarpNextStageKick()`
  (`m01` line ~2777): it is **kicked home** and turned into a grey ghost (`SetChrTypeDataGreyNext`).
- On host death the phantom runs `HostDead_1 → proxy:WarpNextStageKick()` (line ~1591): same kick.
- The host and the black-phantom paths call `proxy:LeaveSession()` on clear (lines 2621/2829/etc.),
  and the whole sequence is coordinated over the P2P link (`IsNetMessage`, `NotNetMessage_begin/end`).

There is **no scripted primitive that carries a guest into the host's next block.** `WarpNextStage(dst,…)`
(used 80×) warps a player through *their own* progression; it is not "bring the phantom along." The
session itself is P2P and is torn down on every transition at the **engine (EBOOT) level**, below Lua.

## Why the earlier "persistent co-op" glitched

v3.0.0 just commented out `WarpNextStageKick()`. The phantom stayed, but the **host** still ran its
own clear path (`LeaveSession`, block load) and the net session was torn down underneath — so the
phantom was left orphaned in a half-unloaded world. That is inherent, not a bug we can patch in Lua.

## The hard conclusion

- **True persistence (survive boss/death, shared Nexus, shared world) = an EBOOT-level rewrite of the
  session/warp/loot code.** That is the DeS equivalent of the Elden Ring Seamless mod — a large native
  project built on a mature modding framework (ModEngine, years of ER RE). DeS-on-RPCS3 has none of
  that, the EBOOT is stripped, and it **cannot be validated without two players in-game** (only the
  user + friend can test). This is not realistically shippable as a one-person, un-testable effort.

## What IS achievable (delivers the user's 3 wants, in feel)

An **"enhanced co-op"** that makes the disconnect invisible instead of removing it:

1. **Items for both (`pegar itens`)** — grant the phantom the block-clear reward + mirror key drops
   via the server/param layer (`AddBlockClearBonus`, `DivideRest`, `GetSoloClearBonus`, ItemLot). Needs
   prototyping + a 2P test, but is param/server-side, not EBOOT.
2. **Regroup instantly / Nexus together (`ir pro nexus junto`)** — lean into the clean kick: on
   teardown the phantom auto-re-places its sign (server already relocates it next to the host), so
   both are back together in one tap, and both can sit in the Nexus and re-party in seconds. No orphan
   glitch because we are *not* fighting the teardown.
3. Everything already shipped (sign next to host, full-HP soul form, Cyanide Pill, infinite Eyes).

This is robust, testable in small pieces, and won't reproduce the "zoado" state.

## EBOOT groundwork (for a future, testable attempt)

Located the Lua API in the decrypted EBOOT (PS3 PPU = 32-bit pointers):
- Method-name strings sit in `.rodata` ~`0x16d_xxx`–`0x16e_xxx` (e.g. `WarpNextStageKick`@`0x16e2838`,
  `WarpNextStage`@`0x16e1748`, `SummonSuccess`@`0x16dc258`), each referenced once from a **name table in
  `.data`** (~`0x18d_xxx`). The entry after the name pointer points at *another rodata string*, not a
  function descriptor — i.e. this is a **tolua-style binding**: names in one table, dispatch through
  generated wrappers, and the real C++ session/warp logic behind them (very likely virtual dispatch).
- This is the **same wall the LBP2 RE hit**: static analysis can't cross the wrappers/vtables. Pinning
  the native teardown/warp/loot functions needs a **live breakpoint in RPCS3's PPU debugger while a
  co-op session actually tears down** — which needs two players in-game. So an EBOOT persistence/loot
  patch is only reachable once 2P testing exists (2nd RPCN account, or the friend), not solo/static.

## Recommendation

Spend the week on the **enhanced co-op** (loot-sharing + instant auto-regroup), not on a blind EBOOT
session rewrite. I can build it on this branch and stage each piece with heavy logging for the 2P test.
