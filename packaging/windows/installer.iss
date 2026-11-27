; Inno Setup script for the Windows installer (built by .github/workflows/release.yml).
;   iscc /DAppVersion=0.5.0 /DNumericVersion=0.5.0 /DSourceDir=<publish dir> /DOutputDir=dist
;        /DOutputBase=Video-Thumbnailer-0.5.0-win-x64-setup /DArch=x64 packaging\windows\installer.iss
; Unsigned. Installs per user by default (no admin rights needed); the dialog offers "for all users".

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#ifndef NumericVersion
  #define NumericVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif

[Setup]
; Fixed id: updates and uninstall find earlier installations. Never change it.
AppId={{5C3A9E62-4B7D-4F0E-9D1A-7E2B8C6F4A31}
AppName=Video-Thumbnailer
AppVersion={#AppVersion}
AppVerName=Video-Thumbnailer {#AppVersion}
VersionInfoVersion={#NumericVersion}
AppPublisher=Thorsten Schröpel
AppCopyright=Copyright (C) 2026 Thorsten Schröpel
AppPublisherURL=https://github.com/vulture20/Video-Thumbnailer
AppSupportURL=https://github.com/vulture20/Video-Thumbnailer/issues
AppUpdatesURL=https://github.com/vulture20/Video-Thumbnailer/releases
DefaultDirName={autopf}\Video-Thumbnailer
DefaultGroupName=Video-Thumbnailer
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
LicenseFile={#SourcePath}\..\..\LICENSE
SetupIconFile={#SourcePath}\..\..\src\VideoThumbnailer.Gui\Assets\app.ico
UninstallDisplayIcon={app}\VideoThumbnailer.exe
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBase}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
; Preset files (*.vtpreset) open with a double-click (#33); per user under HKCU, for all users under HKLM ("HKA").
ChangesAssociations=yes

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
de.CliHint=Das Kommandozeilenwerkzeug vthumb.exe liegt im Programmordner.
en.CliHint=The command line tool vthumb.exe is in the program folder.
de.AssocPreset=Preset-Dateien (.vtpreset) mit Video-Thumbnailer öffnen
en.AssocPreset=Open preset files (.vtpreset) with Video-Thumbnailer
de.PresetType=Video-Thumbnailer-Preset
en.PresetType=Video-Thumbnailer preset
de.AssocSheet=Sheet-Begleitdateien (.vtsheet) und Raster (.vtgrid) mit Video-Thumbnailer öffnen
en.AssocSheet=Open sheet sidecar files (.vtsheet) and grids (.vtgrid) with Video-Thumbnailer
de.SheetType=Video-Thumbnailer-Sheet
en.SheetType=Video-Thumbnailer sheet
de.GridType=Video-Thumbnailer-Raster
en.GridType=Video-Thumbnailer grid

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "assocpreset"; Description: "{cm:AssocPreset}"
Name: "assocsheet"; Description: "{cm:AssocSheet}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Video-Thumbnailer"; Filename: "{app}\VideoThumbnailer.exe"; Comment: "{cm:CliHint}"
Name: "{autodesktop}\Video-Thumbnailer"; Filename: "{app}\VideoThumbnailer.exe"; Tasks: desktopicon

[Registry]
; ".vtpreset" belongs to the program; ".vtpreset.json" cannot (Windows only sees ".json"), so the program is only
; offered under "Open with" for JSON files, without becoming their default.
Root: HKA; Subkey: "Software\Classes\.vtpreset"; ValueType: string; ValueName: ""; ValueData: "VideoThumbnailer.Preset"; Flags: uninsdeletevalue; Tasks: assocpreset
Root: HKA; Subkey: "Software\Classes\.vtpreset"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-vtpreset+json"; Flags: uninsdeletevalue; Tasks: assocpreset
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Preset"; ValueType: string; ValueName: ""; ValueData: "{cm:PresetType}"; Flags: uninsdeletekey; Tasks: assocpreset
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Preset\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\VideoThumbnailer.exe,0"; Tasks: assocpreset
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Preset\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoThumbnailer.exe"" ""%1"""; Tasks: assocpreset
Root: HKA; Subkey: "Software\Classes\.json\OpenWithProgids"; ValueType: string; ValueName: "VideoThumbnailer.Preset"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assocpreset
Root: HKA; Subkey: "Software\Classes\Applications\VideoThumbnailer.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "Video-Thumbnailer"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\VideoThumbnailer.exe\SupportedTypes"; ValueType: string; ValueName: ".vtpreset"; ValueData: ""
; Sheet sidecars (#38) open the viewer (#39), grids (#36) are added to the own grids.
Root: HKA; Subkey: "Software\Classes\.vtsheet"; ValueType: string; ValueName: ""; ValueData: "VideoThumbnailer.Sheet"; Flags: uninsdeletevalue; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\.vtsheet"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-vtsheet+json"; Flags: uninsdeletevalue; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Sheet"; ValueType: string; ValueName: ""; ValueData: "{cm:SheetType}"; Flags: uninsdeletekey; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Sheet\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\VideoThumbnailer.exe,0"; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Sheet\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoThumbnailer.exe"" ""%1"""; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\.vtgrid"; ValueType: string; ValueName: ""; ValueData: "VideoThumbnailer.Grid"; Flags: uninsdeletevalue; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\.vtgrid"; ValueType: string; ValueName: "Content Type"; ValueData: "application/x-vtgrid+json"; Flags: uninsdeletevalue; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Grid"; ValueType: string; ValueName: ""; ValueData: "{cm:GridType}"; Flags: uninsdeletekey; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Grid\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\VideoThumbnailer.exe,0"; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\VideoThumbnailer.Grid\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoThumbnailer.exe"" ""%1"""; Tasks: assocsheet
Root: HKA; Subkey: "Software\Classes\Applications\VideoThumbnailer.exe\SupportedTypes"; ValueType: string; ValueName: ".vtsheet"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\VideoThumbnailer.exe\SupportedTypes"; ValueType: string; ValueName: ".vtgrid"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\VideoThumbnailer.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\VideoThumbnailer.exe"" ""%1"""

[Run]
Filename: "{app}\VideoThumbnailer.exe"; Description: "{cm:LaunchProgram,Video-Thumbnailer}"; Flags: nowait postinstall skipifsilent
