#define MyAppName "BossKey"
#define MyAppPublisher "BossKey"
#define MyAppExeName "BossKey.App.exe"

#ifndef MyAppVersion
  #define MyAppVersion "1.3.2"
#endif

#ifndef OutputBaseName
  #define OutputBaseName "BossKey-Setup"
#endif

#ifndef DotNetDesktopRuntimeVersionPrefix
  #define DotNetDesktopRuntimeVersionPrefix "8.0."
#endif

#ifndef DotNetDesktopRuntimeDownloadUrl
  #define DotNetDesktopRuntimeDownloadUrl "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
#endif

#ifndef DotNetDesktopRuntimeInstallerName
  #define DotNetDesktopRuntimeInstallerName "windowsdesktop-runtime-8-win-x64.exe"
#endif

#ifndef SourceDir
  #error SourceDir is not defined.
#endif

#ifndef SourceIcon
  #error SourceIcon is not defined.
#endif

#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
AppId={{9B52D2AF-4964-4A07-A212-3A9A9D22F6D9}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename={#OutputBaseName}
OutputDir={#OutputDir}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UsedUserAreasWarning=no
SetupIconFile={#SourceIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
#ifexist "Languages\ChineseSimplified.isl"
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
#endif

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "Start with Windows"; GroupDescription: "Additional options"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BossKey.App"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DotNetRuntimeRegKey = 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  DotNetRuntimeVersionPrefix = '{#DotNetDesktopRuntimeVersionPrefix}';
  DotNetRuntimeDownloadUrl = '{#DotNetDesktopRuntimeDownloadUrl}';
  DotNetRuntimeInstallerName = '{#DotNetDesktopRuntimeInstallerName}';

function IsDesktopRuntimeInstalled: Boolean;
var
  SubKeyNames: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if not RegGetSubkeyNames(HKLM, DotNetRuntimeRegKey, SubKeyNames) then
    Exit;

  for I := 0 to GetArrayLength(SubKeyNames) - 1 do
  begin
    if Pos(DotNetRuntimeVersionPrefix, SubKeyNames[I]) = 1 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

function InstallDesktopRuntime: Boolean;
var
  ResultCode: Integer;
  CommandLine: string;
begin
  Result := True;
  if IsDesktopRuntimeInstalled then
    Exit;

  if MsgBox(
      'BossKey requires .NET Desktop Runtime 8.0. Setup will now download and install it automatically.',
      mbInformation,
      MB_OKCANCEL) <> IDOK then
  begin
    Result := False;
    Exit;
  end;

  CommandLine :=
    '-NoProfile -ExecutionPolicy Bypass -Command "' +
    '$ErrorActionPreference = ''Stop''; ' +
    '$url = ''' + DotNetRuntimeDownloadUrl + '''; ' +
    '$out = Join-Path $env:TEMP ''' + DotNetRuntimeInstallerName + '''; ' +
    'Invoke-WebRequest -Uri $url -OutFile $out; ' +
    '$p = Start-Process -FilePath $out -ArgumentList ''/install'',''/quiet'',''/norestart'' -Wait -PassThru; ' +
    'exit $p.ExitCode"';

  if not Exec('powershell.exe', CommandLine, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('Failed to start .NET Desktop Runtime installer.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if ResultCode <> 0 then
  begin
    MsgBox(
      Format('.NET Desktop Runtime installer exited with code %d.', [ResultCode]),
      mbError,
      MB_OK);
    Result := False;
    Exit;
  end;

  if not IsDesktopRuntimeInstalled then
  begin
    MsgBox('.NET Desktop Runtime 8.0 is still missing after installation.', mbError, MB_OK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  if InstallDesktopRuntime then
    Result := ''
  else
    Result := '.NET Desktop Runtime 8.0 installation is required to continue.';
end;
