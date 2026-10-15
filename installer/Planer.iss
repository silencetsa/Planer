#define AppName "Планер"
#define AppVersion "1.1.2"
#define AppExe "Planer.App.exe"

[Setup]
AppId={{A316F590-BB3F-43E1-81C8-861EA1BC03BA}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Planer
DefaultDirName={localappdata}\Programs\Planer
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist
OutputBaseFilename=Planer-{#AppVersion}-win-x64-setup
SetupIconFile=..\src\Planer.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Assets\AppIcon-{#AppVersion}.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "Создать ярлык на рабочем столе"; GroupDescription: "Ярлыки:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\src\Planer.App\Assets\AppIcon.ico"; DestDir: "{app}\Assets"; DestName: "AppIcon-{#AppVersion}.ico"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\AppIcon-{#AppVersion}.ico"; AppUserModelID: "Planer.Desktop"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; IconFilename: "{app}\Assets\AppIcon-{#AppVersion}.ico"; AppUserModelID: "Planer.Desktop"; Check: CreateOrUpdateDesktopShortcut

[Run]
Filename: "{app}\{#AppExe}"; Description: "Открыть планер"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

; Personal data is stored separately in %LOCALAPPDATA%\Planer and is never removed by this installer.

[Code]
procedure SHChangeNotify(EventID: Longint; Flags: Cardinal; Item1: String; Item2: Longint);
  external 'SHChangeNotify@shell32.dll stdcall';

function CreateOrUpdateDesktopShortcut: Boolean;
begin
  { Refresh a previously created shortcut even if the optional task was cleared on an update. }
  Result := WizardIsTaskSelected('desktopicon') or FileExists(ExpandConstant('{autodesktop}\{#AppName}.lnk'));
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Shortcut: String;
begin
  if CurStep = ssPostInstall then
  begin
    { SHCNE_UPDATEITEM; SHCNF_PATHW | SHCNF_FLUSH. Notify only the planner shortcuts. }
    Shortcut := ExpandConstant('{autodesktop}\{#AppName}.lnk');
    if FileExists(Shortcut) then SHChangeNotify($2000, $1005, Shortcut, 0);
    Shortcut := ExpandConstant('{autoprograms}\{#AppName}.lnk');
    if FileExists(Shortcut) then SHChangeNotify($2000, $1005, Shortcut, 0);
  end;
end;
