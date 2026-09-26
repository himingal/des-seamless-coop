# Two-instance self-test rig (for validating co-op on one PC)

Lets one PC run **two** Demon's Souls clients so co-op can be tested without a second person.
Claude cannot finish this autonomously because it requires **creating a second RPCN account**, which is
a prohibited action for the assistant — that one step is yours. Everything else is scripted below.

> Disk note (2026-09-22): C: had only ~7.5 GB free. **Do not copy the 7.9 GB RPCS3 folder.** Use the
> junction approach below (shares firmware/binaries at zero extra space). Keep an eye on free space.

## 1. Make a second instance root (zero-copy)
RPCS3 is portable — all data lives next to the exe. A second instance needs its **own** `config`,
`dev_hdd0`, `dev_hdd1`, `cache`, `log`; everything else can be a **junction** into the first install.

In an **elevated** PowerShell (junctions/hardlinks need it), with
`$SRC = "C:\Users\migue\Downloads\rpcs3-v0.0.42-19989-2003f240_win64_msvc"` and
`$P2  = "C:\Users\migue\rpcs3-p2"`:

```powershell
New-Item -ItemType Directory -Force $P2 | Out-Null
# Big shared, read-only dirs -> junctions (no extra disk):
foreach ($d in 'dev_flash','dev_flash2','dev_flash3','dev_bdvd','fonts','Icons','GuiConfigs','patches') {
  if (Test-Path "$SRC\$d") { cmd /c mklink /J "$P2\$d" "$SRC\$d" | Out-Null }
}
# Binaries -> hardlinks (same volume, no extra disk):
Get-ChildItem -File $SRC | ForEach-Object { cmd /c mklink /H "$P2\$($_.Name)" "$($_.FullName)" | Out-Null }
# Its own writable state (copy config so it inherits your graphics settings):
Copy-Item "$SRC\config" "$P2\config" -Recurse
New-Item -ItemType Directory -Force "$P2\dev_hdd0","$P2\dev_hdd1","$P2\cache","$P2\log" | Out-Null
```

## 2. Give the second instance its own online identity (YOUR step)
1. Launch `"$P2\rpcs3.exe"` once, boot to the RPCS3 UI.
2. Create a **second RPCN account** there (RPCS3 → your usual RPCN sign-up), different NPID from
   `mingalDES`. This is the step the assistant cannot do.
3. Confirm `"$P2\config\rpcn.yml"` now holds the new NPID/token (not `mingalDES`).

## 3. Run both, co-op with yourself
- Instance 1 (your normal install / the launcher's PLAY) = **HOST**. Host a party.
- Instance 2 (`"$P2\rpcs3.exe"` booting the DeS disc at `C:\ROM RPCS3\Demons Souls (USA)`) = **HELPER**.
  Point it at the party (it connects to `127.0.0.1` — both are on this PC).
- Two full emulators are heavy (RX 6600 / Xeon E5-2650 v3); expect low FPS. Fine for logic testing.
- Drive each window with `PostMessage(WM_KEYDOWN/WM_KEYUP)` to its hwnd so neither steals focus.

## 4. Cleanup (safe)
Remove **only** `$P2`. Delete junctions individually first so nothing follows a link into `$SRC`:
```powershell
Get-ChildItem $P2 -Directory | Where-Object { $_.LinkType -eq 'Junction' } | ForEach-Object { cmd /c rmdir "$($_.FullName)" }
Remove-Item $P2 -Recurse -Force
```

Once the second account exists, the assistant can drive both instances to test summon / boss teardown /
loot-sharing for real.
