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

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
de.CliHint=Das Kommandozeilenwerkzeug vthumb.exe liegt im Programmordner.
en.CliHint=The command line tool vthumb.exe is in the program folder.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Video-Thumbnailer"; Filename: "{app}\VideoThumbnailer.exe"; Comment: "{cm:CliHint}"
Name: "{autodesktop}\Video-Thumbnailer"; Filename: "{app}\VideoThumbnailer.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\VideoThumbnailer.exe"; Description: "{cm:LaunchProgram,Video-Thumbnailer}"; Flags: nowait postinstall skipifsilent
