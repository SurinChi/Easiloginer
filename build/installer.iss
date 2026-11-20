; EasiLoginer 安装脚本（Inno Setup 6）
; 生成单文件安装包：双击 → 向导（欢迎/目录/任务/进度/完成）→ 解压 → 启动。
; 编译：build\publish.cmd 第 7 步自动调用 ISCC，并通过 /DMyAppVersion=... 传入版本号。
; 单独编译时可用下面的默认值（与 Directory.Build.props 保持一致）。
;
; 设计要点：目标目录已存在时不再弹 Inno 默认的「文件夹已存在」警告，
; 而是明确提示「保留数据安装」：只替换程序文件，data\ 中的配置与档案保留。

#ifndef MyAppVersion
  #define MyAppVersion "0.2.0-beta.2"
#endif
#ifndef MyAppVersionNumeric
  #define MyAppVersionNumeric "0.2.0.0"
#endif

#define MyAppName "EasiLoginer"
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
; 始终显示目录页：升级时也要让用户看到「保留数据安装」的说明与确认
DisableDirPage=no
LicenseFile=
WizardStyle=modern
PrivilegesRequired=lowest
; 目录已存在时由下方 [Code] 统一提示「保留数据安装」，不使用 Inno 默认警告
DirExistsWarning=no
OutputDir=..\..\EasiLoginer-dist
OutputBaseFilename=EasiLoginer-Setup-{#MyAppVersion}
; 专属竖向品牌侧边栏与右上角小图标（328×628 源图按向导尺寸等比缩放）
WizardImageFile=installer-sidebar.bmp
WizardSmallImageFile=installer-small.bmp
WizardImageStretch=yes
SetupIconFile=..\src\EasiLoginer.Host\Assets\easiloginer.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
AppMutex=EasiLoginerMutex
; 运行中的旧版本由下方 [Code] 的 InitializeSetup 明确询问并结束；
; 不再让 Inno 在复制文件阶段弹“文件正在使用”的通用提示。
CloseApplications=no
RestartApplications=no
VersionInfoVersion={#MyAppVersionNumeric}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} {#MyAppVersion} 安装程序
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersionNumeric}

[Languages]
Name: "chinesesimplified"; MessagesFile: "languages\ChineseSimplified.isl"

[CustomMessages]
chinesesimplified.KeepDataTitle=保留数据安装
chinesesimplified.KeepDataMessage=目标文件夹已存在。继续安装将保留其中的配置与档案数据（data 目录），只替换程序文件。
chinesesimplified.KeepDataNote=升级或选择已存在的文件夹时，将保留 data 目录中的配置与档案，只替换程序文件。
chinesesimplified.ReadyKeepData=保留数据安装：本次安装只更新程序文件，data 目录中的配置与档案不会被删除。
chinesesimplified.CloseRunningMessage=检测到 EasiLoginer 正在运行。%n%n安装新版本前需要先结束它，否则程序文件可能被占用。%n%n是否现在结束 EasiLoginer 并继续安装？
chinesesimplified.CloseRunningFailed=无法结束正在运行的 EasiLoginer。请手动退出程序后重新运行安装包。

[Messages]
chinesesimplified.WelcomeLabel2=这将安装 [name] {#MyAppVersion} 到你的电脑。%n%n升级或选择已存在的文件夹时，安装程序会保留 data 目录中的配置与档案，只替换程序文件。%n%n继续前请关闭正在运行的 EasiLoginer。
chinesesimplified.SelectDirDesc=选择 EasiLoginer 的安装位置。
chinesesimplified.SelectDirLabel3=安装程序将把 [name] 安装到下面的文件夹；如果该文件夹已存在，将继续安装并保留其中的配置与档案数据。
chinesesimplified.SelectDirBrowseLabel=点击“下一步”继续；如需安装到其他文件夹，点击“浏览”。
chinesesimplified.ConfirmUninstall=确认要卸载 %1 吗？%n%n卸载会删除整个安装目录，包括 data 目录中的配置与档案；如需保留，请先备份。

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务："

[InstallDelete]
; 只清理旧版的程序 DLL；绝不删除 data 目录（配置与档案）。
; 升级时 7z 以 -y 覆盖程序文件，data\ 不在负载中，因此不会被清掉。
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
; 应用内更新会传 /RESTARTAPP：静默安装结束后自动拉起新版本
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: IsRestartRequested

[UninstallDelete]
; 负载解压出来的文件不在 Inno 的安装记录里，卸载时整目录清理
Type: filesandordirs; Name: "{app}"

[Code]
// 应用内静默更新由 UpdatePage 传入 /RESTARTAPP；安装结束后据此拉起新版本。
function IsRestartRequested: Boolean;
var
  Index: Integer;
begin
  Result := False;
  for Index := 1 to ParamCount do
  begin
    if CompareText(ParamStr(Index), '/RESTARTAPP') = 0 then
    begin
      Result := True;
      Exit;
    end;
  end;
end;

// 安装开始前检查旧版本是否还在运行：明确询问后结束它，避免 Inno 在复制文件阶段
// 才弹出“文件正在使用”的通用提示。静默安装不询问，直接结束旧进程。
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  Attempt: Integer;
begin
  Result := True;

  if not CheckForMutexes('EasiLoginerMutex') then
  begin
    Exit;
  end;

  Log('EasiLoginer is running before install');

  if WizardSilent then
  begin
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM EasiLoginer.exe',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Log(Format('taskkill exit code: %d', [ResultCode]));
  end
  else
  begin
    if MsgBox(ExpandConstant('{cm:CloseRunningMessage}'), mbConfirmation,
      MB_YESNO or MB_DEFBUTTON1) <> IDYES then
    begin
      Result := False;
      Exit;
    end;

    if Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM EasiLoginer.exe',
      '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
    begin
      Log(Format('taskkill exit code: %d', [ResultCode]));
    end
    else
    begin
      Log('taskkill could not be started');
    end;
  end;

  for Attempt := 1 to 10 do
  begin
    if not CheckForMutexes('EasiLoginerMutex') then
    begin
      Break;
    end;

    Sleep(300);
  end;

  if CheckForMutexes('EasiLoginerMutex') then
  begin
    Log('EasiLoginer is still running after taskkill');
    MsgBox(ExpandConstant('{cm:CloseRunningFailed}'), mbError, MB_OK);
    Result := False;
  end;
end;

// 设置已迁移到 HKCU 注册表；卸载时一并清掉，避免重装后残留旧设置。
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKCU, 'Software\EasiLoginer\Settings');
  end;
end;

// 在目录选择页显示一行说明，告诉用户已存在的目录会保留数据。
procedure InitializeWizard();
var
  Note: TNewStaticText;
begin
  Note := TNewStaticText.Create(WizardForm);
  Note.Parent := WizardForm.SelectDirPage;
  Note.Left := WizardForm.DirEdit.Left;
  Note.Top := WizardForm.DirBrowseButton.Top + WizardForm.DirBrowseButton.Height + ScaleY(10);
  Note.Width := WizardForm.DirEdit.Width;
  Note.Height := ScaleY(38);
  Note.AutoSize := False;
  Note.WordWrap := True;
  Note.Font.Color := clGray;
  Note.Caption := ExpandConstant('{cm:KeepDataNote}');
end;

// 目标文件夹已存在时，用「保留数据安装」代替 Inno 默认的「文件夹已存在」警告。
// 静默安装不弹窗，直接按保留数据的方式继续。
function NextButtonClick(CurPageID: Integer): Boolean;
var
  MessageText: String;
begin
  Result := True;

  if (CurPageID = wpSelectDir) and DirExists(WizardDirValue()) and (not WizardSilent) then
  begin
    MessageText := ExpandConstant('{cm:KeepDataTitle}') + '。' + #13#10#13#10 +
      ExpandConstant('{cm:KeepDataMessage}') + #13#10#13#10 +
      '目标位置：' + #13#10 + WizardDirValue() + #13#10#13#10 +
      '是否继续？';
    Result := MsgBox(MessageText, mbConfirmation, MB_YESNO or MB_DEFBUTTON1) = IDYES;
  end;
end;

// 就绪页再次强调保留数据，避免用户以为升级会清空配置。
function UpdateReadyMemo(
  Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine + ExpandConstant('{cm:ReadyKeepData}');

  if MemoTasksInfo <> '' then
  begin
    Result := Result + NewLine + NewLine + MemoTasksInfo;
  end;
end;
