# Compiles every patched global_event.lua (LuaJIT via lupa) and simulates the co-op teardown paths against a
# recording fake proxy. Usage: python simulate_seamless.py <dir with one folder per map (m01..m08) holding the
# extracted luabnd files>. Needs: pip install lupa.
import sys, glob, os
from lupa.luajit21 import LuaRuntime

HARNESS = r'''
function make_world(role)
  local w = { calls = {}, queue = {}, flags = {} }
  local answers = {
    IsWhiteGhost = role == "white", IsLivePlayer = role == "live", IsGreyGhost = role == "grey",
    IsBlackGhost = role == "black", IsHost = role == "live", IsClient = role ~= "live",
    IsInParty_FriendMember = true, IsInParty = true, GetTempSummonParam = 0, IsAlive = true,
    GetLocalPlayerId = 1, IsAliveMotion = false, IsReviveWait = false, IsEventAnim = false,
    GetBlockId = 1, IsSession = true,
  }
  w.answers = answers
  local proxy = {}
  setmetatable(proxy, { __index = function(t, name)
    return function(self, ...)
      local args = {...}
      table.insert(w.calls, { name = name, args = args })
      if name == "SetEventFlag" then w.flags[args[1]] = args[2] end
      if name == "IsCompleteEvent" then return w.flags[args[1]] == true end
      if name == "OnKeyTime2" then table.insert(w.queue, args[2]) end
      if name == "OnPlayerAssessMenu" then table.insert(w.queue, args[3]) end
      if name == "OnRegistFunc" then table.insert(w.queue, "REGIST:" .. tostring(args[2])) end
      if name == "OnTextEffectEnd" then table.insert(w.queue, args[3]) end
      if name == "OnRequestMenuEnd" then table.insert(w.queue, args[2]) end
      if name == "OnChrAnimEnd" then table.insert(w.queue, "ANIMEND:" .. tostring(args[4])) end
      local a = answers[name]
      if a ~= nil then return a end
      return nil
    end
  end })
  w.proxy = proxy
  return w
end

function make_param(net)
  local p = {}
  setmetatable(p, { __index = function(t, name)
    return function(self)
      if name == "IsNetMessage" then return net end
      if name == "GetParam2" then return 123 end
      if name == "GetPlayID" then return 0 end
      return 0
    end
  end })
  return p
end

function run(w, fname)
  local p = make_param(false)
  _G[fname](w.proxy, p)
  local steps = 0
  while #w.queue > 0 and steps < 50 do
    local f = table.remove(w.queue, 1)
    if string.sub(f, 1, 7) ~= "REGIST:" and string.sub(f, 1, 7) ~= "ANIMEND" then
      _G[f](w.proxy, p)
    else
      table.insert(w.calls, { name = f, args = {} })
    end
    steps = steps + 1
  end
end

function called(w, name, arg1)
  for _, c in ipairs(w.calls) do
    if c.name == name and (arg1 == nil or c.args[1] == arg1) then return true end
  end
  return false
end

function trace(w)
  local t = {}
  for _, c in ipairs(w.calls) do
    local a = {}
    for i, v in ipairs(c.args) do a[i] = tostring(v) end
    table.insert(t, c.name .. "(" .. table.concat(a, ",") .. ")")
  end
  return table.concat(t, " ")
end
'''

def load_map(folder):
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute("print = function() end")
    for name in ["event_define.lua", "global_event.lua"]:
        path = os.path.join(folder, name)
        if not os.path.exists(path): continue
        raw = open(path, "rb").read()
        out = bytearray(); i = 0
        while i < len(raw):
            b = raw[i]
            if (0x81 <= b <= 0x9F or 0xE0 <= b <= 0xFC) and i + 1 < len(raw):
                out.append(b); out.append(raw[i+1])
                if raw[i+1] == 0x5C: out.append(0x5C)   # SJIS trail byte 0x5C: keep it a literal backslash
                i += 2; continue
            out.append(b); i += 1
        src = bytes(out).decode("latin-1")
        fn = lua.eval("function(s, n) local f, e = loadstring(s, n); if not f then return e end; f(); return nil end")
        err = fn(src, name)
        if err: raise SystemExit(f"{folder}/{name}: {err}")
    lua.execute(HARNESS)
    return lua

fails = 0
def check(label, ok, w=None, lua=None):
    global fails
    print(("  ok   " if ok else "  FAIL ") + label)
    if not ok:
        fails += 1
        if w is not None: print("       trace:", lua.globals().trace(w)[:1500])

root = sys.argv[1] if len(sys.argv) > 1 else "out"
for folder in sorted(glob.glob(os.path.join(root, "m0*"))):
    print(f"== {folder}")
    lua = load_map(folder)
    g = lua.globals()
    if not g.DESCOOP_BlockClear2:
        check("seamless block present", False); continue

    # Helper (white ghost, in party) kills a boss.
    w = g.make_world("white"); g.run(w, "BlockClear2")
    for bad in ["SetDrawEnable", "SetLoadWait", "LeaveSession", "WarpNextStageKick", "SetChrTypeDataGreyNext"]:
        check(f"helper boss clear: no {bad}", not g.called(w, bad), w, lua)
    check("helper boss clear: no 4063 leave broadcast", not g.called(w, "CustomLuaCallStart", 4063), w, lua)
    check("helper boss clear: no dissolve animation watcher", "REGIST:Check_BlockClearAnim" not in g.trace(w), w, lua)
    check("helper boss clear: rating menu shown", g.called(w, "OnPlayerAssessMenu"), w, lua)
    check("helper boss clear: menu released", g.called(w, "SetSubMenuBrake", False), w, lua)
    check("helper boss clear: flag 4047 cleared", w.flags[4047] is not True, w, lua)
    check("helper boss clear: flag 4000 cleared", w.flags[4000] is not True, w, lua)
    check("helper boss clear: ClearBoss reset", g.ClearBoss is not True, w, lua)

    # Host (alive, in party) kills a boss.
    w = g.make_world("live"); g.run(w, "BlockClear2")
    check("host boss clear: room not left", not g.called(w, "LeaveSession"), w, lua)
    check("host boss clear: no warp", not g.called(w, "WarpNextStageKick"), w, lua)
    check("host boss clear: flags released", w.flags[4047] is not True and w.flags[4000] is not True, w, lua)
    check("host boss clear: no room lock", not g.called(w, "LockSession"), w, lua)

    # Host dies -> helper goes home keeping progress, then re-places its sign at home.
    w = g.make_world("white"); g.DESCOOP_REJOIN = False; g.run(w, "HostDead_1")
    check("host death: helper keeps progress (mode 1)", g.called(w, "SetFlagInitState", 1) and not g.called(w, "SetFlagInitState", 2), w, lua)
    check("host death: helper sent home", g.called(w, "WarpNextStageKick"), w, lua)
    w2 = g.make_world("grey"); g.run(w2, "InGameStart")
    check("back home: sign placed again (SpEffect 4)", g.called(w2, "SetEventSpecialEffect", 10000) and "SetEventSpecialEffect(10000,4)" in g.trace(w2), w2, lua)
    w3 = g.make_world("grey"); g.run(w3, "InGameStart")
    check("next load: no extra sign", "SetEventSpecialEffect(10000,4)" not in g.trace(w3), w3, lua)

    # Invader kicked -> still rolled back.
    w = g.make_world("black"); g.run(w, "OnLeave_Limit")
    check("invader leave: rollback kept (mode 2)", g.called(w, "SetFlagInitState", 2) and not g.called(w, "SetFlagInitState", 1), w, lua)

    # Helper dies (not after a boss) -> home with progress.
    w = g.make_world("white"); g.ClearBoss = False; g.run(w, "PartyGhostDeath_2")
    check("helper death: home with progress", g.called(w, "WarpNextStageKick") and g.called(w, "SetFlagInitState", 1), w, lua)

print("FAILURES:", fails)
sys.exit(1 if fails else 0)
