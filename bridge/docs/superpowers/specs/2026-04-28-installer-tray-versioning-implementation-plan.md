# USB MIDI Bridge — Installer/Tray/Versioning Implementation Plan

## Objetivo

Implementar:

- UI Tray com status e controle do Windows Service
- Pipeline de build/release com versionamento automático e changelog
- Instalador WiX (Burn + MSI) com download de dependências

## Fase 1 — Versionamento e Changelog (base do release)

1) Adicionar `bridge/version.json` como fonte da verdade
2) Adicionar `bridge/Directory.Build.props` para aplicar versão a todos os projetos
3) Adicionar `bridge/CHANGELOG.md` gerado
4) Criar script `bridge/tools/versioning/` para:
   - ler commits desde a última tag `vX.Y.Z`
   - validar prefixos permitidos
   - calcular `X.Y.Z` e `build`
   - atualizar `version.json`, `Directory.Build.props`, `CHANGELOG.md`
5) Criar workflow GitHub Actions (Windows runner) para “Release”:
   - executar script de versionamento
   - build/test
   - publish `Bridge.Service` e `Bridge.Tray`
   - criar zip `UsbMidiBridge-win-x64.zip`
   - (fase 3) gerar MSI/Setup.exe e anexar no GitHub Release

## Fase 2 — Tray UI + IPC

1) Definir contrato de status (`BridgeStatus` JSON):
   - service state (running/stopped)
   - selected IN/OUT
   - profile id
   - vid/pid (quando detectado)
   - virtual port names
   - logs path
2) Implementar Named Pipe no `Bridge.Service`:
   - listener local
   - comando `GetStatus`
3) Implementar `Bridge.Tray`:
   - NotifyIcon + menu
   - polling `GetStatus`
   - Start/Stop do service
   - abrir pasta de logs

## Fase 3 — Instalador (WiX)

1) Criar projeto WiX MSI:
   - instala publish output de Service/Tray
   - registra serviço
   - atalhos (Start Menu + Startup)
2) Criar bootstrapper (Burn):
   - detectar .NET 8 Desktop Runtime
   - baixar e instalar .NET runtime (silent)
   - baixar e executar loopMIDI installer (wizard)
   - executar MSI
3) Integrar geração no CI:
   - build WiX
   - gerar `UsbMidiBridge.msi` e `UsbMidiBridgeSetup.exe`
   - anexar no GitHub Release

## Critérios de aceite

- O usuário instala via `UsbMidiBridgeSetup.exe` com wizard, sem passos manuais de download.
- Ao final, o service roda e o tray aparece na bandeja com status atualizado.
- Versionamento e changelog são gerados automaticamente e seguem o padrão:
  - `vX.Y.Z (build XXXX)`
- Build e testes passam na pipeline de release.

