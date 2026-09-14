; DeS Seamless Co-op installer (Inno Setup 6.1+). Built by tools/build-release.ps1.
#define AppName "DeS Seamless Co-op"
#ifndef AppVersion
  #define AppVersion "1.0.0"
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
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=DesSeamlessCoop-Setup-{#AppVersion}
SetupIconFile=..\src\DesCoop.App\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
ptbr.DesktopIcon=Criar atalho na Área de Trabalho
en.DesktopIcon=Create a desktop shortcut
ptbr.VcRedist=Instalando o Visual C++ Runtime (necessário para o RPCS3)...
en.VcRedist=Installing the Visual C++ Runtime (required by RPCS3)...
ptbr.Launch=Abrir o DeS Seamless Co-op
en.Launch=Launch DeS Seamless Co-op

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:Launch}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; RPCS3, saves and the party server data live in {app}; they are kept on purpose (your save games).
Type: files; Name: "{app}\descoop-settings.json"

[Code]
var
  DownloadPage: TDownloadWizardPage;

function VcRuntimeOk: Boolean;
var
  Installed, Minor: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed)
    and (Installed = 1)
    and RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Minor', Minor)
    and (Minor >= 40);
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (CurPageID = wpReady) and not VcRuntimeOk then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('https://aka.ms/vs/17/release/vc_redist.x64.exe', 'vc_redist.x64.exe', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
      except
        { Not fatal: RPCS3 will tell the user if the runtime is missing. }
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
    WizardForm.StatusLabel.Caption := CustomMessage('VcRedist');
    ShellExec('runas', ExpandConstant('{tmp}\vc_redist.x64.exe'), '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, Code);
  end;
end;
