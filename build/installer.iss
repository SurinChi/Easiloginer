; EasiLoginer 安装脚本（Inno Setup 6）
; 生成单文件安装包：双击 → 向导（欢迎/目录/任务/进度/完成）→ 解压 → 启动。
; 编译：build\publish.cmd 第 6 步自动调用 ISCC。

#define MyAppName "EasiLoginer"
#define MyAppVersion "0.1.2"
#define MyAppPublisher "SurinChi"
#define MyAppExeName "EasiLoginer.exe"
#define MyAppId "{{6B1F3C24-8E7A-4D69-9A62-3F5C0E1D9A71}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/SurinChi/Easiloginer
AppSupportURL=https://github.com/SurinChi/Easiloginer
AppUpdatesURL=https://github.com/SurinChi/Easiloginer/releases
DefaultDirName={localappdata}\Programs\EasiLoginer
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
LicenseFile=
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=..\..\EasiLoginer-dist
OutputBaseFilename=EasiLoginer-Setup-{#MyAppVersion}
SetupIconFile=..\src\EasiLoginer.Host\Assets\easiloginer.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=EasiLoginerMutex
CloseApplications=yes
RestartApplications=no
VersionInfoVersion=0.1.2.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} 安装程序
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："; Flags: checkedonce

[InstallDelete]
; 升级安装时清理旧版本残留的文件，避免与新版本混用
Type: filesandordirs; Name: "{app}\*.dll"

[Files]
; 程序本体先以单个 7z 负载随安装包分发（LZMA2 + BCJ，比 Inno 自身的压缩更小），
; 用户选好目录后再解压，符合"先选路径、后解压"的流程。
Source: "..\..\EasiLoginer-dist\payload.7z"; DestDir: "{app}"; DestName: "payload.7z"; Flags: nocompression deleteafterinstall
Source: "..\..\EasiLoginer-dist\tools\7z.exe"; DestDir: "{app}\.setup"; Flags: deleteafterinstall
Source: "..\..\EasiLoginer-dist\tools\7z.dll"; DestDir: "{app}\.setup"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\.setup\7z.exe"; Parameters: "x ""{app}\payload.7z"" -o""{app}"" -y"; Flags: runhidden waituntilterminated; StatusMsg: "正在解压程序文件…"
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 负载解压出来的文件不在 Inno 的安装记录里，卸载时整目录清理
Type: filesandordirs; Name: "{app}"

