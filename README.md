# MIDI Bridge

Este repositório contém dois componentes:

- `bridge/`: aplicação .NET (Windows) que cria portas MIDI virtuais e faz o roteamento entre hardware MIDI e softwares (Resolume, vMix, OBS, etc.)
- `profile-store/`: repositório público de perfis (JSON) usado pelo bridge para auto-detectar controladoras e aplicar ajustes/segurança

## Estrutura

- `bridge/UsbMidiBridge.sln`: solução .NET 8
- `bridge/src/Bridge.Service`: serviço/worker que mantém a sessão do bridge
- `bridge/src/Bridge.IO.Midi1.WinMM`: backend MIDI 1.0 via WinMM
- `bridge/src/Bridge.IO.VirtualMidi.TeVirtualMidi`: backend de porta virtual via teVirtualMIDI
- `profile-store/index/manifest.json`: manifest público (lista perfis e hashes)
- `profile-store/profiles/**`: perfis em JSON

## Requisitos (Bridge)

- Windows 10/11
- .NET 8 SDK
- Driver teVirtualMIDI instalado (requisito para criar portas virtuais)

## Como rodar (Bridge)

Na pasta `bridge/`:

```powershell
dotnet build .\UsbMidiBridge.sln -c Release
dotnet run --project .\src\Bridge.Service\Bridge.Service.csproj -c Release
```

Ao iniciar, o serviço:

- escolhe automaticamente 1 controladora por vez (MIDI IN + MIDI OUT)
- cria 2 portas virtuais:
  - `{NomeDetectado} - KEYS`
  - `{NomeDetectado} - LEDs`
- roteia:
  - hardware → `{NomeDetectado} - KEYS`
  - `{NomeDetectado} - LEDs` → hardware

## Variáveis de ambiente

### Profiles

- `USB_MIDI_BRIDGE_PROFILE_MANIFEST_URL`
  - URL do manifest do profile-store
  - default: `https://raw.githubusercontent.com/samirbridi/midi-bridge/refs/heads/main/profile-store/index/manifest.json`

### Seleção de device (opcional)

Use somente se o auto-detect não escolher o device certo.

- `USB_MIDI_BRIDGE_DEVICE_IN_CONTAINS`
- `USB_MIDI_BRIDGE_DEVICE_OUT_CONTAINS`

Exemplo:

```powershell
$env:USB_MIDI_BRIDGE_DEVICE_IN_CONTAINS="AKAI"
$env:USB_MIDI_BRIDGE_DEVICE_OUT_CONTAINS="AKAI"
dotnet run --project .\src\Bridge.Service\Bridge.Service.csproj -c Release
```

## Profile Store

O bridge faz download do `manifest.json` e dos perfis, valida SHA-256 e mantém cache local com rollback/quarentena em caso de arquivo inválido.

Para contribuir com perfis:

- adicionar/editar arquivos em `profile-store/profiles/**`
- atualizar `profile-store/index/manifest.json` com `url`, `sha256` e `sizeBytes`
