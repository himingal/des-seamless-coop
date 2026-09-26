
--[DeS Co-op] ======================================================================================
--[DeS Co-op] Seamless co-op, appended by DeS Seamless Co-op. In Lua the last definition of a function
--[DeS Co-op] wins, so the functions below replace the game's own versions above; the originals are kept
--[DeS Co-op] as DESCOOP_* and still run for everyone who is not a summoned helper.
--[DeS Co-op] ======================================================================================

-- A boss dies. Retail sends the helper (white ghost) home: it plays the dissolve animation, hides the
-- character (BlockClearSynchroInvalid), waits for a load (SetLoadWait), freezes the menu, tells the host it
-- is leaving (4063) and then everybody leaves the room (BlockClear2_3Leave). Doing only part of that is what
-- left a helper invisible and stuck. Here the helper celebrates like the host and nothing is torn down.
DESCOOP_BlockClear2 = BlockClear2
function BlockClear2(proxy,param)
	if param:IsNetMessage() == true or proxy:IsWhiteGhost() == false
		or proxy:GetTempSummonParam() > 0 or proxy:IsInParty_FriendMember() == false then
		return DESCOOP_BlockClear2(proxy,param);
	end
	print("BlockClear2 begin (DeS Co-op: helper stays)");
	ClearBossId = param:GetParam2();
	proxy:LuaCallStart( 4055, 1 );
	proxy:SetClearSesiionCount();
	proxy:ClearBossGauge();
	proxy:SetClearBonus(ClearBossId);
	proxy:SetTextEffect(TEXT_TYPE_KillDemon);
	proxy:NotNetMessage_begin();
		proxy:OnKeyTime2( 4050, "BlockClear2_1", 5.0, 0, 2, once );
	proxy:NotNetMessage_end();
	print("BlockClear2 end (DeS Co-op)");
end

-- After the rating menu: no "I am leaving" broadcast and no room teardown, for host and helper alike.
function BlockClear2_2(proxy,param)
	print("BlockClear2_2 begin (DeS Co-op)");
	proxy:NotNetMessage_begin();
		proxy:OnKeyTime2(4050,"BlockClear2_3",CLEAR_TIMEOUT,0,0,once);
	proxy:NotNetMessage_end();
	MissionSuccessed(proxy,param);
	print("BlockClear2_2 end (DeS Co-op)");
end

function BlockClear2_3Leave(proxy,param)
	print("BlockClear2_3Leave (DeS Co-op: the room stays open)");
end

-- The helper stays in the host's world: release everything the clear had locked and heal up.
DESCOOP_BlockClear2_3 = BlockClear2_3
function BlockClear2_3(proxy,param)
	if proxy:IsWhiteGhost() == false or proxy:IsInParty_FriendMember() == false then
		return DESCOOP_BlockClear2_3(proxy,param);
	end
	print("BlockClear2_3 (DeS Co-op: helper stays)");
	proxy:SetSubMenuBrake( false );
	proxy:SetEventFlag( 4047, false );
	proxy:SetEventFlag( 4000, false );
	ClearBoss = false;
	proxy:RequestFullRecover();
end

-- Whenever the helper does go home (host died, host travelled, helper died, connection lost), the helper's
-- world keeps what the two did together instead of rolling it back, and the helper's game puts its sign
-- down again by itself once it is home, so the host only has to touch it.
function DesCoop_ReturnHome(proxy,param)
	if proxy:IsWhiteGhost() == true then
		proxy:SetFlagInitState(1);
		DESCOOP_REJOIN = true;
	else
		proxy:SetFlagInitState(2);
	end
	proxy:SetSummonedPos();
	proxy:SetDefaultMapUid(-1);
	proxy:WarpNextStageKick();
	proxy:SetChrTypeDataGreyNext();
end

function HostDead_1(proxy,param)
	print("HostDead_1 (DeS Co-op)");
	DesCoop_ReturnHome(proxy,param);
end

function OnLeave_Limit(proxy,param)
	print("OnLeave_Limit (DeS Co-op)");
	DesCoop_ReturnHome(proxy,param);
end

DESCOOP_PartyGhostDeath_2 = PartyGhostDeath_2
function PartyGhostDeath_2(proxy,param)
	if proxy:IsWhiteGhost() == true and ClearBoss == false then
		DesCoop_ReturnHome(proxy,param);
		return;
	end
	return DESCOOP_PartyGhostDeath_2(proxy,param);
end

DESCOOP_InGameStart = InGameStart
function InGameStart(proxy,param)
	DESCOOP_InGameStart(proxy,param);
	if DESCOOP_REJOIN == true and proxy:IsWhiteGhost() == false and proxy:IsBlackGhost() == false then
		DESCOOP_REJOIN = false;
		proxy:NotNetMessage_begin();
			proxy:OnKeyTime2( 4090, "DesCoop_Rejoin", 6.0, 0, 0, once );
		proxy:NotNetMessage_end();
	end
end

-- SpEffect 4 is the Join Sigil's own effect (requestSOS): the same sign as using the item.
function DesCoop_Rejoin(proxy,param)
	print("DesCoop_Rejoin: sign placed again");
	proxy:SetEventSpecialEffect( 10000, 4 );
end
