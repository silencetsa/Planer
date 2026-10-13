#define AppName "Планер"
#define AppVersion "1.0.1"
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
UninstallDisplayIcon={app}\{#AppExe}
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

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Открыть планер"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

; Personal data is stored separately in %LOCALAPPDATA%\Planer and is never removed by this installer.
