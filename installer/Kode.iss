#define AppName "Kode"
#define AppVersion "1.0.0"
#define AppExe "Kode.exe"

[Setup]
AppId={{B6F3E1A2-4C7D-4E8B-9A15-3D2F6C8E0A71}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Groupe KEYCE
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\Output
OutputBaseFilename=Kode-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; GroupDescription: "Raccourcis :"

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Lancer Kode"; Flags: nowait postinstall skipifsilent