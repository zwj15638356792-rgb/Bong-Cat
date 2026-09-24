; Compile with native-pet/package.ps1 and Inno Setup 6.
#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif
#ifndef BuildDir
  #define BuildDir SourcePath + "..\output\Dafeiyu-Jointed"
#endif
#ifndef InstallerDir
  #define InstallerDir SourcePath + "..\output\installer"
#endif
#ifndef AppIcon
  #define AppIcon SourcePath + "..\output\jointed-checks\app.ico"
#endif

[Setup]
AppId={{BFBFC240-046F-4C80-94C0-EFBCA1DD3030}
AppName=大肥鱼桌宠
AppVersion={#AppVersion}
AppPublisher=Dafeiyu contributors
AppPublisherURL=https://github.com/zwj15638356792-rgb/Bong-Cat
AppSupportURL=https://github.com/zwj15638356792-rgb/Bong-Cat/issues
DefaultDirName={localappdata}\Programs\Dafeiyu
DefaultGroupName=大肥鱼桌宠
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
MinVersion=10.0
OutputDir={#InstallerDir}
OutputBaseFilename=Dafeiyu-Setup-{#AppVersion}
SetupIconFile={#AppIcon}
UninstallDisplayIcon={app}\Dafeiyu.exe
UninstallDisplayName=大肥鱼桌宠
VersionInfoVersion={#AppVersion}.0
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=Dafeiyu.exe
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#BuildDir}\Dafeiyu.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildDir}\LICENSE-BongoCat.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\大肥鱼桌宠"; Filename: "{app}\Dafeiyu.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\Dafeiyu.exe"; Description: "启动大肥鱼桌宠"; Flags: nowait postinstall skipifsilent


[Code]
function RunValueTargetsThisApp(Value: String): Boolean;
var
  Executable, Expected: String;
  EndPos: Integer;
begin
  Value := Trim(Value);
  Executable := '';
  if Copy(Value, 1, 1) = '"' then
  begin
    Delete(Value, 1, 1);
    EndPos := Pos('"', Value);
    if EndPos > 0 then
      Executable := Copy(Value, 1, EndPos - 1);
  end
  else
  begin
    EndPos := Pos(' ', Value);
    if EndPos > 0 then
      Executable := Copy(Value, 1, EndPos - 1)
    else
      Executable := Value;
  end;
  Expected := ExpandConstant('{app}\Dafeiyu.exe');
  Result := CompareText(Executable, Expected) = 0;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupValue: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
      'DafeiyuDesktopPet', StartupValue) then
      if RunValueTargetsThisApp(StartupValue) then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run',
          'DafeiyuDesktopPet');
end;
