# USB MIDI Bridge — Installer (WiX), Tray UI e Versionamento Automático

## Objetivo

Entregar uma experiência “user-friendly” no Windows com:

- UI em tray para controle e status do bridge (sem depender de console)
- Instalação via MSI com wizard, com bootstrapper para dependências
- Versionamento e changelog automáticos baseados em commits estruturados

## Componentes do produto

### Bridge.Service (Windows Service)

- Motor do bridge: WinMM IN/OUT, teVirtualMIDI KEYS/LEDs, profile store, sanitização, cache/replay de LEDs
- Executa como Windows Service (já suportado via `AddWindowsService`)

### Bridge.Tray (Tray UI)

- UI principal para o usuário final (ícone na bandeja)
- Exibe status e permite start/stop do serviço
- Mostra:
  - device selecionado (IN/OUT)
  - profile escolhido
  - VID/PID (quando detectados)
  - nomes das portas virtuais criadas (KEYS/LEDs)
- Abre logs e fornece botão “copiar diagnóstico”

## Arquitetura UI ↔ Service

### Requisitos

- Sem dependência de UI para o funcionamento do bridge (service continua autônomo)
- UI pode iniciar/parar o service e exibir status sem reiniciar o PC
- Comunicação local, sem rede, sem privilégios desnecessários

### Solução

- **Named Pipe local** (recomendado)
  - Service expõe endpoints simples:
    - `GetStatus` → JSON
    - `GetLogsPath` → string
    - opcional futuro: `SetDeviceSelectionOverrides`
  - Tray faz polling leve (ex.: a cada 1s–2s) e atualiza UI

## Instalador

### Restrições de redistribuição (driver virtualMIDI)

- O instalador não deve embutir instaladores de terceiros sem permissão explícita.
- Estratégia: baixar do site oficial durante o setup e executar o instalador oficial (wizard do vendor).

### Entregáveis do instalador

- `UsbMidiBridgeSetup.exe` (bootstrapper com wizard)
- `UsbMidiBridge.msi` (MSI principal)
- `UsbMidiBridge-win-x64.zip` (artefato “portable” do publish, opcional)

### Bootstrapper (WiX Burn)

- Wizard que:
  1) Verifica e instala **.NET 8 Desktop Runtime x64** (silent)
  2) Verifica e instala **loopMIDI** (download + executar instalador do loopMIDI)
  3) Instala `UsbMidiBridge.msi`
- No final, inicia:
  - Windows Service
  - Tray (ou configura para iniciar no login, conforme opção do usuário)

### MSI (WiX)

- Instala arquivos do `Bridge.Service` e do `Bridge.Tray`
- Registra o Windows Service:
  - StartType: `Automatic`
  - Opção de iniciar ao final do setup
- Atalhos:
  - Start Menu (Tray)
  - Startup (Tray no login)
- Uninstall:
  - Para o serviço
  - Remove arquivos e atalhos

## Versionamento e Changelog (Regra Global)

### Fonte de verdade

- `bridge/version.json`:
  - `{ major, minor, patch, build }`
- `bridge/CHANGELOG.md` gerado automaticamente
- `bridge/Directory.Build.props` atualizado automaticamente

### Formato final da versão

- Exibição para usuário: `vX.Y.Z (build XXXX)`
- Tag de release: `vX.Y.Z`

### Regras de incremento

- Ler commits desde a última tag `vX.Y.Z`
- Se existir `break:` ou `BREAKING CHANGE` → `X++`, `Y=0`, `Z=0`
- Senão, se existir `feat:` → `Y++`, `Z=0`
- Senão → `Z++`
- `build` incrementa **a cada release** (+1)

### Commits suportados

- `feat: ...`
- `fix: ...`
- `improve: ...`
- `perf: ...`
- `refactor: ...`
- `break: ...`

### Changelog

Gerar seção:

## vX.Y.Z (build XXXX) - YYYY-MM-DD

- 💥 Breaking Changes: commits `break`
- ✨ Features: commits `feat`
- 🐛 Fixes: commits `fix`
- ⚡ Improvements: commits `improve`, `perf`, `refactor`

Cada item deve referenciar o SHA (curto) e, quando aplicável, link para commit.

## CI / Release automation

- Workflow de release (manual ou por tag) que:
  - valida padrão de commit
  - calcula versão
  - atualiza `version.json`, `Directory.Build.props`, `CHANGELOG.md`
  - gera `Setup.exe`, `MSI`, `ZIP`
  - publica GitHub Release com os assets

