#ifndef MyAppVersion
  #define MyAppVersion "0.9.18"
#endif

#ifndef MySourceDir
  #define MySourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{1EA677E9-8D85-4345-9427-A85E0D81D86A}
AppName=LfsunBrowser
AppVersion={#MyAppVersion}
AppPublisher=LfsunBrowser
DefaultDirName={localappdata}\LfsunBrowser
DefaultGroupName=LfsunBrowser
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#MyOutputDir}
OutputBaseFilename=LfsunBrowser-setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\LfsunBrowser.exe

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: unchecked

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\LfsunBrowser"; Filename: "{app}\LfsunBrowser.exe"
Name: "{autodesktop}\LfsunBrowser"; Filename: "{app}\LfsunBrowser.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LfsunBrowser.exe"; Description: "启动 LfsunBrowser"; Flags: nowait postinstall skipifsilent
