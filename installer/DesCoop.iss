; DeS Seamless Co-op installer (Inno Setup 6). Built by tools/build-release.ps1.
; Installs the app and opens it, handing over the chosen game folder. RPCS3 is downloaded by the user from
; rpcs3.net and pointed to in the app; the app itself only downloads the PS3 firmware. No setup step runs.
#define AppName "DeS Seamless Co-op"
#ifndef AppVersion
  #define AppVersion "2.0.0"
#endif
#define AppExe "DesCoop.exe"

[Setup]
AppId={{6B2E1C57-4A7E-4C0B-9E55-DE5C00B0A001}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=himingal
AppPublisherURL=https://github.com/himingal/des-seamless-coop
DefaultDirName={autopf}\DeS Seamless Coop
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
OutputDir=..\dist
OutputBaseFilename=DesSeamlessCoop-Setup-{#AppVersion}
SetupIconFile=..\src\DesCoop.App\app.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
WizardImageFile=wizard.bmp,wizard-200.bmp
WizardSmallImageFile=wizard-small.bmp,wizard-small-200.bmp
WizardImageStretch=no
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Messages]
WelcomeLabel1=Welcome, Slayer of Demons
WelcomeLabel2=This installs [name/ver].%n%nWhen it opens, get RPCS3 from rpcs3.net and point the app to it (one click). The app downloads the official PS3 firmware for you and sets up your Demon's Souls copy for seamless co-op on PLAY.%n%nYou need your own Demon's Souls dump and an internet connection.
FinishedHeadingLabel=The Nexus awaits
FinishedLabel=Open the app, point it to RPCS3 (get it at rpcs3.net) and your Demon's Souls folder, install the PS3 firmware and create your free RPCN account, then host or join a party and press PLAY.

[CustomMessages]
DesktopIcon=Create a desktop shortcut
Preparing=Downloading RPCS3 and the PS3 firmware, patching the game (a few minutes)...
Launch=Launch DeS Seamless Co-op

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
; Experimental sandbox shortcut (separate profile; never touches the stable co-op).
Name: "{autoprograms}\{#AppName} - Seamless (TEST)"; Filename: "{app}\{#AppExe}"; Parameters: "--seamless"
Name: "{autodesktop}\{#AppName} - Seamless (TEST)"; Filename: "{app}\{#AppExe}"; Parameters: "--seamless"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--game ""{code:GamePath}"""; Description: "{cm:Launch}"; Flags: nowait postinstall

[UninstallDelete]
; RPCS3, your saves and the party server data live in {app}\rpcs3 and are kept on purpose.
Type: files; Name: "{app}\descoop-settings.json"
Type: files; Name: "{app}\crash.log"

[Code]
var
  DownloadPage: TDownloadWizardPage;
  GamePage: TInputQueryWizardPage;
  BrowseButton: TNewButton;

procedure BrowseClick(Sender: TObject);
var
  Dir: String;
begin
  Dir := GamePage.Values[0];
  if BrowseForFolder('Select your Demon''s Souls folder (the one that contains PS3_GAME):', Dir, False) then
    GamePage.Values[0] := Dir;
end;

function VcRuntimeOk: Boolean;
var
  Installed, Minor: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed)
    and (Installed = 1)
    and RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Minor', Minor)
    and (Minor >= 40);
end;

function LooksLikeGame(Dir: String): Boolean;
begin
  Result := FileExists(AddBackslash(Dir) + 'PS3_GAME\USRDIR\EBOOT.BIN')
    or FileExists(AddBackslash(Dir) + 'USRDIR\EBOOT.BIN')
    or FileExists(AddBackslash(Dir) + 'EBOOT.BIN');
end;

procedure InitializeWizard;
begin
  { An input query page (not an InputDir page) so the folder can be left empty. }
  GamePage := CreateInputQueryPage(wpSelectDir,
    'Your Demon''s Souls',
    'Where is your game?',
    'Select the folder of your Demon''s Souls dump (the one that contains PS3_GAME, e.g. BLUS30443).' + #13#10 +
    'Don''t have it here yet? Leave it empty and pick it later in the app.');
  GamePage.Add('Game folder (optional):', False);
  GamePage.Values[0] := ExpandConstant('{param:GAME|}');
  GamePage.Edits[0].Width := GamePage.SurfaceWidth - ScaleX(90);
  BrowseButton := TNewButton.Create(GamePage);
  BrowseButton.Parent := GamePage.Surface;
  BrowseButton.Caption := 'Browse...';
  BrowseButton.Left := GamePage.SurfaceWidth - ScaleX(80);
  BrowseButton.Top := GamePage.Edits[0].Top - ScaleY(1);
  BrowseButton.Width := ScaleX(80);
  BrowseButton.Height := GamePage.Edits[0].Height + ScaleY(2);
  BrowseButton.OnClick := @BrowseClick;
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function GamePath(Param: String): String;
begin
  Result := Trim(GamePage.Values[0]);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = GamePage.ID) and (GamePath('') <> '') and not LooksLikeGame(GamePath('')) then
    Result := MsgBox('No PS3_GAME\USRDIR\EBOOT.BIN was found in that folder.' + #13#10 + 'Continue anyway?', mbConfirmation, MB_YESNO) = IDYES;

  if (CurPageID = wpReady) and not VcRuntimeOk then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('https://aka.ms/vs/17/release/vc_redist.x64.exe', 'vc_redist.x64.exe', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        Log('VC++ download failed: ' + GetExceptionMessage);
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
begin
  if (CurStep = ssPostInstall) and FileExists(ExpandConstant('{tmp}\vc_redist.x64.exe')) then
  begin
    WizardForm.StatusLabel.Caption := 'Installing the Visual C++ Runtime (required by RPCS3)...';
    ShellExec('runas', ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code);
  end;
end;
