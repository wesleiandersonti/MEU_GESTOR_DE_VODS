#define MyAppName "MEU GESTOR DE VODS"
#define MyAppPublisher "Weslei Anderson TI"
#define MyAppExeName "MeuGestorVODs.exe"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{4A009EA2-7E58-40AE-AF0F-6D68C15A8A08}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\Meu Gestor de VODs
DefaultGroupName=MEU GESTOR DE VODS
DisableProgramGroupPage=no
OutputDir=..\installer-output
OutputBaseFilename=MeuGestorVODs-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
InfoBeforeFile=..\output\release-notes.txt

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "startmenuicon"; Description: "Criar atalhos no Menu Iniciar"; GroupDescription: "Atalhos:"; Flags: checkedonce
Name: "autostart"; Description: "Iniciar com o Windows"; GroupDescription: "Opcoes adicionais:"; Flags: unchecked

[Files]
Source: "..\output\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\MEU GESTOR DE VODS"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\Desinstalar MEU GESTOR DE VODS"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{autodesktop}\MEU GESTOR DE VODS"; Filename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MeuGestorVODs"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Executar MEU GESTOR DE VODS"; Flags: nowait postinstall skipifsilent
