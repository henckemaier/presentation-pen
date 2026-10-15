<p align="center">
  <img src="assets/icon.png" width="96" alt="Ícone do PresentationPen">
</p>

<h1 align="center">PresentationPen</h1>

<p align="center">
  Desenhe, dê zoom, escreva e faça rascunhos <b>por cima da tela</b> durante apresentações e compartilhamentos de tela no Windows.
</p>

<p align="center">
  <img alt="Windows 10/11" src="https://img.shields.io/badge/Windows-10%202004%2B%20%7C%2011-0078D6">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4">
  <img alt="WPF" src="https://img.shields.io/badge/UI-WPF-5C2D91">
  <img alt="Licença MIT" src="https://img.shields.io/badge/licen%C3%A7a-MIT-green">
</p>

---

O PresentationPen fica na bandeja do sistema e, com um atalho, cobre a tela com uma camada transparente onde você pode desenhar, marcar, apontar com setas, escrever, ampliar uma região ou abrir um quadro branco. Tudo o que você desenha aparece para quem assiste ao seu compartilhamento (Teams, Meet, Zoom, Discord, OBS…), enquanto a **barra de ferramentas, os avisos e a lista de rascunhos ficam visíveis só para você**.

## Funcionalidades

- **Desenho livre** — caneta e marca-texto, 6 cores, espessura ajustável.
- **Formas** — linha, seta, retângulo e elipse (com `Shift`: ângulos de 45°, quadrado e círculo).
- **Texto** — clique e digite direto na tela.
- **Borracha** — apaga traços, formas e textos.
- **Desfazer/refazer** ilimitado e **limpar tudo**.
- **Zoom estilo ZoomIt** — amplia de 1× a 8× com a roda do mouse, a imagem acompanha o cursor e dá para desenhar por cima da área ampliada.
- **Quadro branco e quadro preto** — para explicar algo do zero, sem sair da apresentação.
- **Screenshots** — salva a tela **com os rascunhos** em PNG, copia para a área de transferência e mostra um aviso com miniatura.
- **Rascunhos para continuar depois** — salva os desenhos (com a imagem da tela que estava por trás) e reabre depois por uma lista com miniaturas; dá para renomear, e as alterações são salvas automaticamente ao sair.
- **Interface escondida do compartilhamento** — barra, avisos e lista de rascunhos não aparecem para a plateia (configurável).
- **Atalhos globais configuráveis**, ícone na bandeja e **início automático com o Windows**.
- **Multimonitor e DPI alto** — abre no monitor onde está o cursor.
- **Instalador em português** com escolha de pastas, atalhos e opções.

## Instalação

### Com o instalador (recomendado)

1. Baixe o `PresentationPen-Setup-x.y.z.exe` na página de [Releases](https://github.com/henckemaier/presentation-pen/releases).
2. Execute e siga o assistente. Nele você escolhe:
   - para quem instalar (só você — sem pedir administrador — ou todos os usuários) e a pasta de instalação;
   - as pastas de **screenshots** e de **rascunhos**;
   - os **atalhos de teclado** globais;
   - **iniciar com o Windows**, **esconder a interface do compartilhamento** e **ícone na área de trabalho**.
3. Se o computador não tiver o **.NET 10 Desktop Runtime**, o instalador avisa e abre a [página oficial de download](https://dotnet.microsoft.com/download/dotnet/10.0) (escolha *.NET Desktop Runtime 10 — Windows x64*).

Para desinstalar, use *Configurações → Aplicativos → Aplicativos instalados → PresentationPen*. A desinstalação **nunca apaga** seus rascunhos e screenshots; ela pergunta se deve remover as configurações.

### Requisitos

- Windows 10 versão 2004 (build 19041) ou mais recente, ou Windows 11 — x64.
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## Como usar

Depois de aberto, o PresentationPen fica como um ícone vermelho na bandeja do sistema (perto do relógio). Clique nele ou use um atalho global.

### Atalhos globais

Funcionam com qualquer programa em primeiro plano (e podem ser trocados no instalador ou nas configurações).

| Atalho | Ação |
|---|---|
| `Ctrl+Shift+D` | Liga/desliga o modo desenho |
| `Ctrl+Shift+Z` | Zoom |
| `Ctrl+Shift+W` | Quadro branco |
| `Ctrl+Shift+K` | Quadro preto |
| `Ctrl+Shift+S` | Salva um screenshot (tela + desenhos) |

### No modo desenho

| Tecla | Ação |
|---|---|
| `P` · `H` · `X` | Caneta · marca-texto · borracha |
| `L` · `A` · `R` · `E` | Linha · seta · retângulo · elipse (segure `Shift` para reto/quadrado) |
| `T` | Texto (`Esc` ou `Ctrl+Enter` para concluir) |
| `1` a `6` | Cores: vermelho, verde, azul, amarelo, laranja, preto (branco no quadro preto) |
| `+` / `-` ou roda do mouse | Espessura |
| `Ctrl+Z` / `Ctrl+Y` | Desfazer / refazer |
| `Delete` | Limpar tudo |
| `W` / `K` | Quadro branco / quadro preto |
| `Z` | Zoom |
| `Ctrl+S` | Salvar rascunho |
| `Ctrl+O` | Abrir rascunho |
| `B` | Mostrar/ocultar a imagem de tela salva com o rascunho |
| `F1` | Lista de atalhos |
| `Esc` | Sair do modo desenho |

### No zoom

| Ação | Efeito |
|---|---|
| Roda do mouse | Aproxima/afasta (1× a 8×) |
| Mover o mouse | Navega pela imagem ampliada |
| Começar a desenhar | Trava a imagem automaticamente (ela não anda enquanto você mira o próximo traço) |
| `Espaço` ou botão direito | Solta/trava a imagem para navegar |
| `Ctrl` + roda | Espessura do traço |

O zoom é feito sobre uma captura congelada da tela (como no ZoomIt), então dá para desenhar em cima da área ampliada. O traço mantém na tela a espessura escolhida em qualquer nível de zoom, e com a imagem travada a roda do mouse aproxima mantendo parado o ponto sob o cursor.

### Rascunhos

- `Ctrl+S` salva os desenhos em um arquivo `.ppen` (por padrão em `Documentos\PresentationPen\Rascunhos`), junto com a imagem da tela que estava por trás e uma miniatura.
- `Ctrl+O` (ou *Abrir rascunho...* no menu da bandeja) abre a lista de rascunhos. Nela:
  - **Renomear**: botão *Renomear*, `F2` ou clique direito;
  - **Mostrar a imagem da tela salva**: abre o rascunho sobre a tela original em vez da tela atual (depois de aberto, alterne com `B`).
- Depois de abrir ou salvar um rascunho, `Ctrl+S` grava nele mesmo, e ao sair com `Esc` as alterações são salvas automaticamente.
- Depois de *Limpar tudo*, o próximo `Ctrl+S` cria um rascunho novo (não sobrescreve o anterior).

> **Privacidade:** o arquivo `.ppen` guarda uma imagem da tela no momento em que foi salvo — inclusive outras janelas abertas. Tenha isso em mente antes de compartilhar um rascunho.

### Screenshots

`Ctrl+Shift+S` salva a tela inteira do monitor atual **com os desenhos** em `Imagens\PresentationPen` e copia a imagem para a área de transferência. Um aviso no canto mostra a miniatura, com botões para abrir a imagem ou a pasta.

## Configurações

Pelo menu da bandeja: *Editar configurações...* abre o arquivo `%AppData%\PresentationPen\settings.json`; depois de salvar, use *Recarregar configurações*.

| Opção | Padrão | Descrição |
|---|---|---|
| `HotkeyDraw`, `HotkeyZoom`, `HotkeyWhiteboard`, `HotkeyBlackboard`, `HotkeyScreenshot` | `Ctrl+Shift+D/Z/W/K/S` | Atalhos globais (formato `Ctrl+Shift+Letra`; também valem `Alt` e `Win`) |
| `ScreenshotFolder` | `""` | Pasta dos screenshots (vazio = `Imagens\PresentationPen`) |
| `CopyScreenshotToClipboard` | `true` | Copia o screenshot para a área de transferência |
| `ScreenshotSound` | `false` | Toca um som ao salvar (desligado porque iria junto se você compartilha o áudio do PC) |
| `DraftsFolder` | `""` | Pasta dos rascunhos (vazio = `Documentos\PresentationPen\Rascunhos`) |
| `AutoSaveOpenDraftOnClose` | `true` | Ao sair do modo desenho, salva as alterações no rascunho aberto |
| `KeepDrawingsOnClose` | `false` | Mantém os desenhos na tela ao fechar e reabrir o modo desenho |
| `HideToolbarFromCapture` | `true` | Esconde barra, avisos e lista de rascunhos do compartilhamento de tela e dos screenshots |
| `DefaultPenSize` | `4` | Espessura inicial (1 a 30) |

Erros inesperados são registrados em `%AppData%\PresentationPen\error.log`.

## Compilando a partir do código

Pré-requisitos: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) e, para o instalador, [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`winget install JRSoftware.InnoSetup`).

```powershell
# compilar e rodar
dotnet run --project src/PresentationPen

# gerar o executável (arquivo único, em .\publish)
dotnet publish src/PresentationPen/PresentationPen.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o publish

# gerar o instalador (em .\installer\Output) — publica e compila o .iss
powershell -ExecutionPolicy Bypass -File build-installer.ps1
```

A versão vem da tag `<Version>` em `src/PresentationPen/PresentationPen.csproj`. O ícone (`assets/PresentationPen.ico`) é gerado por `tools/make-icon.ps1`.

### Estrutura

```
src/PresentationPen/
  App.xaml(.cs)            inicialização, instância única, log de erros
  AppController.cs         liga bandeja, atalhos, overlay, screenshots e rascunhos
  Overlay/                 janela transparente de desenho, formas, desfazer, zoom
  Toolbar/                 barra de ferramentas flutuante
  Notifications/           aviso de canto e lista de rascunhos
  Services/                atalhos globais, bandeja, captura de tela, configurações,
                           rascunhos (.ppen), inicialização com o Windows, opções do instalador
  Interop/NativeMethods.cs chamadas Win32 (RegisterHotKey, BitBlt, SetWindowDisplayAffinity…)
installer/PresentationPen.iss  script do instalador (Inno Setup 6)
build-installer.ps1            publica o app e gera o instalador
tools/make-icon.ps1            gera o ícone multi-tamanho
```

### Como funciona

- **Overlay:** uma janela WPF sem borda e *topmost* cobre o monitor do cursor. A transparência é feita pela composição do DWM (`DwmExtendFrameIntoClientArea`), acelerada por GPU — em vez de uma janela em camadas (`AllowsTransparency`), que reenviaria a tela inteira a cada quadro e deixava o zoom pesado. Os traços usam `InkCanvas`; formas e textos ficam num `Canvas` por cima.
- **Esconder do compartilhamento:** a barra, os avisos e a lista de rascunhos usam `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` (Windows 10 2004+), que os remove de qualquer captura de tela, enquanto o overlay com os desenhos continua sendo capturado normalmente.
- **Atalhos globais:** `RegisterHotKey` numa janela *message-only*.
- **Captura de tela:** `BitBlt` com `CAPTUREBLT`, que inclui janelas em camadas como o overlay.
- **Rascunhos (`.ppen`):** arquivo zip com `draft.json` (formas e textos), `ink.isf` (traços no formato Ink Serialized Format), `background.png` (tela por trás) e `thumb.png` (miniatura).
- **Instalador → app:** o instalador grava `install-settings.json` ao lado do executável; na primeira execução após instalar/atualizar, o app copia essas opções para o `settings.json` do usuário.
- **Por que não é um "Serviço do Windows":** serviços rodam numa sessão isolada e não podem desenhar na tela do usuário. Por isso o app fica residente na bandeja e inicia junto com o Windows.

## Limitações conhecidas

- O zoom é sobre uma imagem congelada da tela, não ao vivo.
- A borracha apaga o traço, forma ou texto inteiro (não parte dele).
- O modo desenho abre em um monitor por vez (o que estiver sob o cursor).
- Atalhos globais já usados por outro programa não podem ser registrados — o app avisa e você pode trocá-los nas configurações.

## Licença

Distribuído sob a [licença MIT](LICENSE). Veja os componentes de terceiros em [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
