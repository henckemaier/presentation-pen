# Avisos de terceiros

O código do PresentationPen é distribuído sob a [licença MIT](LICENSE). Ele não inclui código-fonte de terceiros, mas depende dos componentes abaixo para rodar ou para gerar o instalador.

## Em tempo de execução

### .NET 10 Desktop Runtime (WPF e Windows Forms)

- Projeto: https://github.com/dotnet/runtime, https://github.com/dotnet/wpf, https://github.com/dotnet/winforms
- Licença: MIT — https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- Uso: o app é *framework-dependent*: o runtime **não** é redistribuído neste repositório nem no instalador. O usuário o instala a partir do site oficial da Microsoft (https://dotnet.microsoft.com/download/dotnet/10.0).

### Fonte de ícones Segoe MDL2 Assets

- Fonte de sistema do Windows 10/11, usada pelos ícones da barra de ferramentas.
- **Não** é redistribuída: o app apenas a referencia pelo nome, e ela já vem instalada no Windows.

## Para gerar o instalador

### Inno Setup 6

- Projeto: https://jrsoftware.org/isinfo.php
- Licença: https://jrsoftware.org/files/is/license.txt
- Uso: ferramenta de compilação do instalador (`installer/PresentationPen.iss`). O executável gerado contém o código de instalação do Inno Setup, cuja licença permite o uso gratuito, inclusive comercial, de instaladores criados com ele.
- O arquivo de idioma `BrazilianPortuguese.isl` usado pelo instalador vem junto com a instalação do Inno Setup.
