# G-Windows

A desktop organizer for Windows 10/11. G-Windows groups your shortcuts, folders and files into **translucent "fences"** that sit on the desktop, snap to an adaptive grid, and react with smooth spring animations.

> Fences are *views*: they only hold references. G-Windows never moves, copies or deletes your files.

*Português: veja a [seção em português](#português) mais abaixo.*

## Features

- **Fences on a grid** – aligned in columns that adapt to each monitor's size; drag a fence to another column/row and it snaps into place. Per-fence "free movement" if you prefer to place it yourself.
- **Top-edge switch** – runs in the background; push the mouse to the top edge of any monitor (over the bare desktop) and click the switch to show or hide all fences.
- **Fence types** – manual, folder portal (mirrors a real folder), smart fences (recent files, largest files, untouched for 90 days, most-used folders — fed by Desktop, Downloads and Documents), and widgets (clock, notes).
- **Drag & drop or paste paths** – drop files/folders onto a fence, or paste paths (Ctrl+V). Network paths are supported.
- **Auto-capture rules** – send new desktop files to a fence by pattern (`*.pdf; *.docx`).
- **Profiles, snapshots, import/export** of layouts.
- **Command palette** (`Ctrl+Space`) and a global show/hide hotkey (`Ctrl+Alt+C`).
- Native Explorer context menu, hover previews, one-click open, per-fence tint, English / Portuguese UI.

## Privacy

100% local. No network access, no telemetry, no accounts. Settings live in `%AppData%\G-Windows\layout.json`.

## Install

1. Download a zip from the [Releases](../../releases) page:
   - `G-Windows-*-win-x64.zip` — small; requires the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0).
   - `G-Windows-*-win-x64-self-contained.zip` — larger; nothing else to install.
2. Extract anywhere and run `G-Windows.exe`. It lives in the system tray (right-click the icon for the menu, including *Start with Windows*).

## Build from source

```bash
dotnet build -c Release
dotnet test
dotnet publish src/GWindows.App -c Release -o publish --no-self-contained
```

Requires the .NET 8 SDK on Windows. Layout of the repo:

| Path | What |
|---|---|
| `src/GWindows.Core` | UI-independent logic: layout model/persistence, grid, rules, smart fences, spring physics |
| `src/GWindows.App` | WPF/WinForms app: fence windows, shell integration, tray, palette |
| `tests/GWindows.Core.Tests` | xUnit tests for the core |
| `docs/security-audit` | Security audit report (PDF) and the script that generates it |

## Security

A source-level security audit was done; see `docs/security-audit/relatorio-auditoria-seguranca.pdf` (Portuguese). Import layout files only from sources you trust: items may point to any path, including network shares.

## Code signing

Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/) — **application in progress**. Until it is approved, release binaries are unsigned and Windows SmartScreen may show a warning. See the [code signing policy](CODE_SIGNING_POLICY.md) (roles, what is signed, privacy).
## Disclaimer

G-Windows is an independent open-source project and is not affiliated with, endorsed by or sponsored by Microsoft. "Windows" is a trademark of Microsoft Corporation.

## License

[MIT](LICENSE)

---

## Português

**G-Windows** organiza a área de trabalho do Windows em "cercas" translúcidas alinhadas numa grade que se adapta ao tamanho de cada monitor. As cercas só guardam referências: nada é movido, copiado ou apagado.

- **Interruptor no topo da tela:** o app roda em segundo plano; leve o mouse ao topo de qualquer monitor, sobre o desktop vazio, e clique no interruptor para mostrar/ocultar as cercas.
- **Tipos de cerca:** manual, portal de pasta, inteligentes (recentes, maiores arquivos, não tocados há 90 dias, pastas mais acessadas — de Desktop, Downloads e Documentos) e widgets (relógio, notas).
- **Arrastar e soltar** ou **colar caminhos** (Ctrl+V); caminhos de rede são aceitos.
- **Regras** de captura automática, perfis, snapshots, importar/exportar layout, paleta de comandos (`Ctrl+Space`) e atalho global (`Ctrl+Alt+C`).
- Idioma da interface: português ou inglês (ícone da bandeja → *Idioma / Language*).

**Privacidade:** tudo local, sem rede nem telemetria. Configurações em `%AppData%\G-Windows\layout.json`.

**Instalação:** baixe o zip em *Releases*, extraia e execute `G-Windows.exe` (a versão pequena exige o .NET 8 Desktop Runtime; a *self-contained* não exige nada).
