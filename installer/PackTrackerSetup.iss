; ============================================================
; PackTracker - Inno Setup Script
; House Wolf Operations Shell for Star Citizen
;
; Build from repo root:
;   ISCC.exe /DAppVersion=0.1.5 installer\PackTrackerSetup.iss
;
; Notes:
; - Build your app first so ..\publish\ contains the full publish output
; - This script installs the whole publish folder into Program Files
; - SmartScreen / "Unknown Publisher" warnings are NOT removed by Inno flags.
;   To reduce/remove those warnings, sign both the app EXE and installer.
; ============================================================

#ifndef AppVersion
  #define AppVersion "0.3.1"
#endif

#define AppName        "PackTracker"
#define AppPublisher   "House Wolf"
#define AppURL         "https://github.com/BrokkrForgemaster/PackTracker"
#define AppExeName     "PackTracker.Presentation.exe"
#define AppDescription "House Wolf Operations Center"
#define AppIconFile    "..\PackTracker.Presentation\Assets\housewolf2.ico"

[Setup]
AppId={{A3F2C1D0-5E4B-4F8A-9C2D-1B0E7F3A8D56}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
AppCopyright=Copyright (C) House Wolf

; Cleaner Windows metadata
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer
VersionInfoProductName={#AppName}
VersionInfoCopyright=Copyright (C) House Wolf

; Install location
DefaultDirName={autopf}\HouseWolf\PackTracker
DefaultGroupName=House Wolf\PackTracker
AllowNoIcons=yes

; Output
OutputDir=output
OutputBaseFilename=PackTrackerSetup-{#AppVersion}

; Branding
SetupIconFile={#AppIconFile}
WizardStyle=modern
WizardImageFile=..\PackTracker.Presentation\Assets\HousewolfBanner_installer.bmp
WizardSmallImageFile=..\PackTracker.Presentation\Assets\Pack_Tracker_installer.bmp
WizardImageStretch=yes
WizardImageBackColor=$10141A

; Installer behavior
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
RestartIfNeededByRun=no

; Helps with upgrades/uninstalls when app is still running
CloseApplications=yes
RestartApplications=no

; These do not remove SmartScreen, but help installer quality/reputation signals
SetupLogging=yes
DisableDirPage=no
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousGroup=yes
UninstallDisplayIcon={app}\housewolf2.ico
UninstallDisplayName={#AppName} - {#AppDescription}

; Enable this once you are code-signing the installer/uninstaller
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; \
  Description: "Create a &desktop shortcut"; \
  GroupDescription: "Additional shortcuts:"

[Files]
; Primary publish output
Source: "..\publish\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs; \
  Check: DirExists(ExpandConstant('{src}\..\publish'))

; Failsafe for local dev builds
Source: "..\publish\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs; \
  Check: not DirExists(ExpandConstant('{src}\..\publish'))

; Explicitly ensure appsettings.json is included
Source: "..\PackTracker.Presentation\appsettings.json"; \
  DestDir: "{app}"; \
  Flags: ignoreversion

; Installer/app icon used for shortcuts and uninstall entry
Source: "{#AppIconFile}"; \
  DestDir: "{app}"; \
  Flags: ignoreversion

[Icons]
; Start menu shortcut
Name: "{group}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  IconFilename: "{app}\housewolf2.ico"; \
  Comment: "{#AppDescription}"

; Uninstall shortcut
Name: "{group}\Uninstall {#AppName}"; \
  Filename: "{uninstallexe}"

; Desktop shortcut
Name: "{autodesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  IconFilename: "{app}\housewolf2.ico"; \
  Comment: "{#AppDescription}"; \
  Tasks: desktopicon

[Run]
; Offer to launch after install
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove common runtime leftovers in install directory
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\*.tmp"
Type: files; Name: "{app}\*.bak"

; Remove Logs folder contents if your app creates them under {app}
Type: files; Name: "{app}\Logs\*"
Type: dirifempty; Name: "{app}\Logs"

; Remove empty folders that may remain after uninstall
Type: dirifempty; Name: "{app}\Assets"

; If your app writes runtime data under LocalAppData, clean it here too
; Uncomment if appropriate
; Type: filesandordirs; Name: "{localappdata}\PackTracker\Logs"
; Type: filesandordirs; Name: "{localappdata}\PackTracker\Cache"

[Code]
procedure InitializeWizard;
begin
  WizardForm.WelcomeLabel1.Caption :=
    'Welcome to PackTracker Setup';

  WizardForm.WelcomeLabel2.Caption :=
    'PackTracker is the House Wolf operations shell for Star Citizen - ' +
    'your unified hub for blueprint ops, trade intel, crafting logistics, and fleet coordination.' + #13#10 + #13#10 +
    'Version ' + ExpandConstant('{#AppVersion}') + #13#10 + #13#10 +
    'Close other applications before continuing, then click Next.';

  WizardForm.FinishedLabel.Caption :=
    'PackTracker has been installed on your computer.' + #13#10 + #13#10 +
    'The application may require PostgreSQL, Discord authentication, and user-specific API settings ' +
    'to be configured after first launch.' + #13#10 + #13#10 +
    'Click Finish to close Setup.';
end;