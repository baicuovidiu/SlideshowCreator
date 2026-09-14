#define AppName "Slideshow Creator"
#define AppVersion "0.1.4"
#define AppExeName "SlideshowCreator.exe"

[Setup]
AppId={{8F1D8A8F-773B-46A3-B975-4FBF5DA821E9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Stefan Ovidiu
DefaultDirName={localappdata}\Programs\Slideshow Creator
DefaultGroupName={#AppName}
OutputDir=OUTPUT
OutputBaseFilename=SlideshowCreator-Setup-0.1.4
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}
SetupLogging=yes

[Files]
Source: "src\SlideshowCreator\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Creeaza pictograma pe Desktop"; GroupDescription: "Pictograme:"; Flags: checkedonce

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Porneste Slideshow Creator"; Flags: nowait postinstall skipifsilent
