; ============================================================
; PackTracker — Inno Setup Script
; House Wolf Operations Shell for Star Citizen
;
; Build from repo root:
;   ISCC.exe /DAppVersion=0.1.4 installer\PackTrackerSetup.iss
;
; The GitHub Actions workflow passes AppVersion automatically.
; Version format: MAJOR.MINOR.PATCH  (no leading zeros, e.g. 0.1.4)
;
; Desktop icon note:
;   IconFilename must be a .ico file.  Convert HWiconnew.png → HWiconnew.ico
;   once (Paint / ImageMagick / any online converter), then place the .ico in
;   PackTracker.Presentation\Assets\ alongside the PNG.
; ============================================================

#ifndef AppVersion
  #define AppVersion "0.1.5"
#endif

#define AppName        "PackTracker"
#define AppPublisher   "House Wolf"
#define AppURL         "https://github.com/BrokkrForgemaster/PackTracker"
#define AppExeName     "PackTracker.Presentation.exe"
#define AppDescription "House Wolf Operations Shell"
#define AppIconFile    "..\PackTracker.Presentation\Assets\HWiconnew.ico"

[Setup]
AppId={{A3F2C1D0-5E4B-4F8A-9C2D-1B0E7F3A8D56}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
AppCopyright=Copyright © House Wolf

; Install location
DefaultDirName={autopf}\HouseWolf\PackTracker
DefaultGroupName=House Wolf\PackTracker
AllowNoIcons=yes

; Output
OutputDir=output
OutputBaseFilename=PackTrackerSetup-{#AppVersion}

; Branding — wizard uses the existing housewolf2.ico; shortcuts use the new icon
SetupIconFile=..\PackTracker.Presentation\Assets\housewolf2.ico
WizardStyle=modern
WizardImageFile=..\PackTracker.Presentation\Assets\HousewolfBanner.bmp
WizardSmallImageFile=..\PackTracker.Presentation\Assets\HWiconnew.png
WizardImageStretch=yes
; Dark background behind the banner image — matches app theme (#1A1410)
WizardImageBackColor=$10141A

; Installer behavior
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; Uninstall entry in Add/Remove Programs
UninstallDisplayIcon={app}\HWiconnew.ico
UninstallDisplayName={#AppName} — {#AppDescription}

; Restart not needed for a standalone exe
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; \
  Description: "Create a &desktop shortcut"; \
  GroupDescription: "Additional shortcuts:"

[Files]
; All publish output — app cannot be single-file due to WebView2 native libs
Source: "..\publish\*"; \
  DestDir: "{app}"; \
  Flags: ignoreversion recursesubdirs createallsubdirs

; Desktop/shortcut icon — convert HWiconnew.png to HWiconnew.ico before building
Source: "..\PackTracker.Presentation\Assets\HWiconnew.ico"; \
  DestDir: "{app}"; \
  Flags: ignoreversion

[Icons]
; Start menu
Name: "{group}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  IconFilename: "{app}\HWiconnew.ico"; \
  Comment: "{#AppDescription}"

Name: "{group}\Uninstall {#AppName}"; \
  Filename: "{uninstallexe}"

; Desktop (optional task)
Name: "{autodesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  IconFilename: "{app}\HWiconnew.ico"; \
  Comment: "{#AppDescription}"; \
  Tasks: desktopicon

[Run]
; Offer to launch after install
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[Code]
// ── Custom welcome & finish page text ──────────────────────────────────────
procedure InitializeWizard;
begin
  // Welcome page
  WizardForm.WelcomeLabel1.Caption :=
    'Welcome to PackTracker Setup';

  WizardForm.WelcomeLabel2.Caption :=
    'PackTracker is the House Wolf operations shell for Star Citizen — ' +
    'your unified hub for blueprint ops, trade intel, crafting logistics, and fleet coordination.' + #13#10 + #13#10 +
    'Version ' + ExpandConstant('{#AppVersion}') + #13#10 + #13#10 +
    'Close other applications before continuing, then click Next.';

  // Finish page
  WizardForm.FinishedLabel.Caption :=
    'PackTracker has been installed on your computer.' + #13#10 + #13#10 +
    'The application requires a PostgreSQL database and Discord authentication ' +
    'to be configured on first launch.' + #13#10 + #13#10 +
    'Click Finish to close Setup.';
end;
