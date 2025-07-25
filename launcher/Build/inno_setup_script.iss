[Setup]
AppName=R5Reloaded
AppVersion=1.2.1
WizardStyle=modern
DefaultDirName=C:\Program Files\R5Reloaded
DefaultGroupName=R5Reloaded
UninstallDisplayIcon={app}\R5R Launcher\launcher.exe
Compression=lzma2
SolidCompression=yes
OutputDir=..\bin\Publish
PrivilegesRequired=admin
UninstallFilesDir={app}\R5R Launcher\
SetupIconFile=..\Assets\launcher_x64.ico
AppPublisher=R5Reloaded
AppPublisherURL=https://r5reloaded.com
OutputBaseFilename=R5RLauncher-Setup
UsePreviousGroup=no
UsePreviousAppDir=no

[Files]
Source: "..\bin\Publish\launcher.exe"; DestDir: "{app}\R5R Launcher\"

[Icons]
Name: "{group}\R5Reloaded"; Filename: "{app}\R5R Launcher\launcher.exe"

[Dirs]
Name: "{app}"; Permissions: users-full
Name: "{app}\R5R Launcher"; Permissions: users-full
Name: "{app}\R5R Library"; Permissions: users-full