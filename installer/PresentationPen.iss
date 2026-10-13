; Instalador do PresentationPen (Inno Setup 6)
; Compile com:  powershell -ExecutionPolicy Bypass -File build-installer.ps1
; (publica o app em ..\publish e depois roda o ISCC com a versão do .csproj)

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "PresentationPen"
#define AppExe "PresentationPen.exe"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{8F3C6A2E-5B1D-4E7A-9C2F-3D4B5A6E7F81}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppName}
VersionInfoVersion={#AppVersion}
VersionInfoDescription=Instalador do {#AppName}
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
; por padrão instala só para o usuário atual (sem pedir administrador); dá para escolher "todos os usuários"
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Windows 10 2004+: necessário para esconder a barra do compartilhamento de tela
MinVersion=10.0.19041
OutputDir=Output
OutputBaseFilename=PresentationPen-Setup-{#AppVersion}
SetupIconFile=..\assets\PresentationPen.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; fecha o PresentationPen se estiver aberto durante a instalação/atualização
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "startup"; Description: "Iniciar o PresentationPen junto com o Windows (fica na bandeja, pronto para usar)"; GroupDescription: "Inicialização:"
Name: "hidecapture"; Description: "Esconder a barra de ferramentas, os avisos e a lista de rascunhos de quem assiste ao compartilhamento de tela"; GroupDescription: "Compartilhamento de tela:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "Desenhar, dar zoom e fazer rascunhos na tela durante apresentações"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExe}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/F /IM {#AppExe}"; Flags: runhidden; RunOnceId: "StopPresentationPen"

[UninstallDelete]
Type: files; Name: "{app}\install-settings.json"

[Code]
const
  CSIDL_PERSONAL = $0005;
  CSIDL_MYPICTURES = $0027;
  DotNetUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0';

var
  FoldersPage: TInputDirWizardPage;
  HotkeysPage: TInputQueryWizardPage;
  DefaultScreenshots, DefaultDrafts: String;
  HotkeyKeys: array[0..4] of String;
  HotkeyDefaults: array[0..4] of String;

{ ------------------------------------------------------------ .NET }

function IsDotNetDesktop10Installed: Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), FindRec);
  if Result then
    FindClose(FindRec);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if (not IsDotNetDesktop10Installed) and (not WizardSilent) then
  begin
    if MsgBox('O PresentationPen precisa do ".NET 10 Desktop Runtime" (gratuito, da Microsoft), que não foi encontrado neste computador.' + #13#10#13#10 +
              'Deseja abrir a página de download agora? Baixe o item ".NET Desktop Runtime 10" para Windows x64.' + #13#10#13#10 +
              'A instalação do PresentationPen continua normalmente; ele funcionará depois que o .NET for instalado.',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', DotNetUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

{ ------------------------------------------------------------ settings.json existente }

{ Lê um valor simples ("Chave": "texto" ou "Chave": true) do settings.json do usuário. }
function ReadJsonValue(const Json, Key: String): String;
var
  P, I: Integer;
  Rest: String;
begin
  Result := '';
  P := Pos('"' + Key + '"', Json);
  if P = 0 then Exit;
  Rest := Copy(Json, P + Length(Key) + 2, MaxInt);
  P := Pos(':', Rest);
  if P = 0 then Exit;
  Rest := Trim(Copy(Rest, P + 1, MaxInt));
  if (Length(Rest) > 0) and (Rest[1] = '"') then
  begin
    I := 2;
    while (I <= Length(Rest)) and (Rest[I] <> '"') do
    begin
      if Rest[I] = '\' then I := I + 1;
      I := I + 1;
    end;
    Result := Copy(Rest, 2, I - 2);
    StringChangeEx(Result, '\\', '\', True);
    StringChangeEx(Result, '\"', '"', True);
  end
  else
  begin
    I := 1;
    while (I <= Length(Rest)) and (Rest[I] <> ',') and (Rest[I] <> '}') and (Rest[I] <> #13) and (Rest[I] <> #10) do
      I := I + 1;
    Result := Trim(Copy(Rest, 1, I - 1));
  end;
end;

function LoadUserSettings: String;
var
  Raw: AnsiString;
begin
  Result := '';
  if LoadStringFromFile(ExpandConstant('{userappdata}\PresentationPen\settings.json'), Raw) then
    Result := String(Raw);
end;

{ ------------------------------------------------------------ páginas }

procedure InitializeWizard;
var
  Settings, Value: String;
  I: Integer;
begin
  DefaultScreenshots := AddBackslash(GetShellFolderByCSIDL(CSIDL_MYPICTURES, False)) + 'PresentationPen';
  DefaultDrafts := AddBackslash(GetShellFolderByCSIDL(CSIDL_PERSONAL, False)) + 'PresentationPen\Rascunhos';

  HotkeyKeys[0] := 'HotkeyDraw';       HotkeyDefaults[0] := 'Ctrl+Shift+D';
  HotkeyKeys[1] := 'HotkeyZoom';       HotkeyDefaults[1] := 'Ctrl+Shift+Z';
  HotkeyKeys[2] := 'HotkeyWhiteboard'; HotkeyDefaults[2] := 'Ctrl+Shift+W';
  HotkeyKeys[3] := 'HotkeyBlackboard'; HotkeyDefaults[3] := 'Ctrl+Shift+K';
  HotkeyKeys[4] := 'HotkeyScreenshot'; HotkeyDefaults[4] := 'Ctrl+Shift+S';

  FoldersPage := CreateInputDirPage(wpSelectDir,
    'Pastas de trabalho', 'Onde o PresentationPen deve guardar seus arquivos?',
    'Escolha as pastas abaixo e clique em Avançar. Elas serão criadas se não existirem.',
    False, '');
  FoldersPage.Add('Screenshots (tela + desenhos):');
  FoldersPage.Add('Rascunhos (desenhos salvos para continuar depois):');
  FoldersPage.Values[0] := GetPreviousData('ScreenshotFolder', DefaultScreenshots);
  FoldersPage.Values[1] := GetPreviousData('DraftsFolder', DefaultDrafts);

  HotkeysPage := CreateInputQueryPage(FoldersPage.ID,
    'Atalhos de teclado', 'Atalhos globais (funcionam com qualquer programa aberto)',
    'Formato: Ctrl+Shift+Letra (também valem Alt e Win). Evite combinações que você já usa em outros programas.');
  HotkeysPage.Add('Desenhar na tela (liga/desliga):', False);
  HotkeysPage.Add('Zoom:', False);
  HotkeysPage.Add('Quadro branco:', False);
  HotkeysPage.Add('Quadro preto:', False);
  HotkeysPage.Add('Salvar screenshot:', False);

  { atualização: mostra os atalhos que o usuário já usa hoje }
  Settings := LoadUserSettings;
  for I := 0 to 4 do
  begin
    Value := ReadJsonValue(Settings, HotkeyKeys[I]);
    if Value = '' then
      Value := GetPreviousData(HotkeyKeys[I], HotkeyDefaults[I]);
    HotkeysPage.Values[I] := Value;
  end;
end;

procedure RegisterPreviousData(PreviousDataKey: Integer);
var
  I: Integer;
begin
  SetPreviousData(PreviousDataKey, 'ScreenshotFolder', FoldersPage.Values[0]);
  SetPreviousData(PreviousDataKey, 'DraftsFolder', FoldersPage.Values[1]);
  for I := 0 to 4 do
    SetPreviousData(PreviousDataKey, HotkeyKeys[I], HotkeysPage.Values[I]);
end;

function IsValidHotkey(const Value: String): Boolean;
var
  V: String;
begin
  V := Lowercase(Trim(Value));
  Result := (Pos('+', V) > 0) and
            ((Pos('ctrl', V) > 0) or (Pos('alt', V) > 0) or (Pos('win', V) > 0) or (Pos('shift', V) > 0)) and
            (Copy(V, Length(V), 1) <> '+');
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  I, J: Integer;
begin
  Result := True;
  if CurPageID = FoldersPage.ID then
  begin
    for I := 0 to 1 do
      if Trim(FoldersPage.Values[I]) = '' then
      begin
        MsgBox('Escolha uma pasta para cada item.', mbError, MB_OK);
        Result := False;
        Exit;
      end;
  end
  else if CurPageID = HotkeysPage.ID then
  begin
    for I := 0 to 4 do
    begin
      if not IsValidHotkey(HotkeysPage.Values[I]) then
      begin
        MsgBox('Atalho inválido: "' + HotkeysPage.Values[I] + '".' + #13#10 +
               'Use um modificador + uma tecla, por exemplo Ctrl+Shift+D.', mbError, MB_OK);
        Result := False;
        Exit;
      end;
      for J := 0 to I - 1 do
        if CompareText(Trim(HotkeysPage.Values[I]), Trim(HotkeysPage.Values[J])) = 0 then
        begin
          MsgBox('O atalho "' + HotkeysPage.Values[I] + '" está repetido.', mbError, MB_OK);
          Result := False;
          Exit;
        end;
    end;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
begin
  Result := MemoDirInfo + NewLine + NewLine +
    'Pastas:' + NewLine +
    Space + 'Screenshots: ' + FoldersPage.Values[0] + NewLine +
    Space + 'Rascunhos: ' + FoldersPage.Values[1] + NewLine + NewLine +
    'Atalhos:' + NewLine +
    Space + 'Desenhar: ' + HotkeysPage.Values[0] + NewLine +
    Space + 'Zoom: ' + HotkeysPage.Values[1] + NewLine +
    Space + 'Quadro branco: ' + HotkeysPage.Values[2] + NewLine +
    Space + 'Quadro preto: ' + HotkeysPage.Values[3] + NewLine +
    Space + 'Screenshot: ' + HotkeysPage.Values[4];
  if MemoTasksInfo <> '' then
    Result := Result + NewLine + NewLine + MemoTasksInfo;
end;

{ ------------------------------------------------------------ gravação das opções }

function JsonStr(const S: String): String;
var
  R: String;
begin
  R := S;
  StringChangeEx(R, '\', '\\', True);
  StringChangeEx(R, '"', '\"', True);
  Result := '"' + R + '"';
end;

function BoolStr(B: Boolean): String;
begin
  if B then Result := 'true' else Result := 'false';
end;

{ Pasta igual ao padrão é gravada vazia: assim cada usuário usa as próprias Imagens/Documentos. }
function FolderValue(const Value, Default: String): String;
begin
  if CompareText(RemoveBackslashUnlessRoot(Trim(Value)), RemoveBackslashUnlessRoot(Default)) = 0 then
    Result := ''
  else
    Result := RemoveBackslashUnlessRoot(Trim(Value));
end;

procedure WriteInstallSettings;
var
  Lines: TArrayOfString;
  I: Integer;
begin
  SetArrayLength(Lines, 12);
  Lines[0] := '{';
  Lines[1] := '  "ScreenshotFolder": ' + JsonStr(FolderValue(FoldersPage.Values[0], DefaultScreenshots)) + ',';
  Lines[2] := '  "DraftsFolder": ' + JsonStr(FolderValue(FoldersPage.Values[1], DefaultDrafts)) + ',';
  for I := 0 to 4 do
    Lines[3 + I] := '  "' + HotkeyKeys[I] + '": ' + JsonStr(Trim(HotkeysPage.Values[I])) + ',';
  Lines[8] := '  "HideToolbarFromCapture": ' + BoolStr(WizardIsTaskSelected('hidecapture')) + ',';
  Lines[9] := '  "StartWithWindows": ' + BoolStr(WizardIsTaskSelected('startup'));
  Lines[10] := '}';
  Lines[11] := '';
  SaveStringsToUTF8File(ExpandConstant('{app}\install-settings.json'), Lines, False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    WriteInstallSettings;
    ForceDirectories(FoldersPage.Values[0]);
    ForceDirectories(FoldersPage.Values[1]);
    { desmarcou "iniciar com o Windows" numa atualização: remove a entrada antiga }
    if not WizardIsTaskSelected('startup') then
      RegDeleteValue(HKCU, '{#RunKey}', '{#AppName}');
  end;
end;

{ ------------------------------------------------------------ desinstalação }

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Value: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { o próprio app também pode ter criado a entrada (menu da bandeja) }
    if RegQueryStringValue(HKCU, '{#RunKey}', '{#AppName}', Value) and
       (Pos(Lowercase(ExpandConstant('{app}')), Lowercase(Value)) > 0) then
      RegDeleteValue(HKCU, '{#RunKey}', '{#AppName}');
  end
  else if CurUninstallStep = usPostUninstall then
  begin
    if (not UninstallSilent) and
       (MsgBox('Remover também as configurações do PresentationPen (atalhos, pastas, preferências)?' + #13#10#13#10 +
               'Seus rascunhos e screenshots NÃO serão apagados.', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then
      DelTree(ExpandConstant('{userappdata}\PresentationPen'), True, True, True);
  end;
end;
