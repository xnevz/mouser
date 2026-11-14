; Inno Setup Script for Mouser
; Produces a single-file installer (Mouser-Setup-<version>.exe)
; Sets up Windows Firewall rules, Start menu/Desktop shortcuts, and optional sign-in startup.

#ifndef AppVersion
#define AppVersion "0.9.0"
#endif

[Setup]
AppId={{9B78C9E4-2C1F-4B7C-8E28-8FD5E6D472B3}
AppName=Mouser
AppVersion={#AppVersion}
AppPublisher=Mouser Open Source Team
AppPublisherURL=https://github.com/xnevz/mouser
AppSupportURL=https://github.com/xnevz/mouser/issues
AppUpdatesURL=https://github.com/xnevz/mouser/releases
DefaultDirName={autopf}\Mouser
DefaultGroupName=Mouser
DisableProgramGroupPage=yes
PrivilegesRequired=admin
OutputDir=..\..\bin\dist
OutputBaseFilename=Mouser-Setup-{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Start Mouser automatically when I sign in"; GroupDescription: "Startup:"; Flags: checkedonce

[Files]
Source: "..\..\bin\dist\windows\Mouser.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\bin\dist\windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Mouser.exe,*.pdb"

[Icons]
Name: "{autoprograms}\Mouser"; Filename: "{app}\Mouser.exe"
Name: "{autodesktop}\Mouser"; Filename: "{app}\Mouser.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Mouser"; ValueData: """{app}\Mouser.exe"" --minimized"; Tasks: startupicon; Flags: uninsdeletevalue

[Run]
; Add inbound private firewall rule for local peer discovery and input streaming
Filename: "netsh.exe"; Parameters: "advfirewall firewall add rule name=""Mouser"" dir=in action=allow program=""{app}\Mouser.exe"" enable=yes profile=private"; Flags: runhidden
; Option to launch Mouser after installation completes
Filename: "{app}\Mouser.exe"; Description: "{cm:LaunchProgram,Mouser}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove inbound firewall rule on uninstall
Filename: "netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Mouser"""; Flags: runhidden

[Code]
// Helper function to optionally prompt user to keep or purge app settings and clipboard history
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\Mouser');
    if DirExists(DataDir) then
    begin
      if MsgBox('Do you want to delete all saved settings and clipboard history?' + #13#10 + DataDir, mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
