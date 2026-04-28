# Bridge (Windows)

Aplicação .NET (Windows) que cria portas MIDI virtuais e faz o roteamento entre uma controladora MIDI física e softwares (Resolume, vMix, OBS, etc.).

## Requisitos

- Windows 10/11
- .NET 8 SDK
- Driver teVirtualMIDI instalado (necessário para criar portas virtuais)

## Como funciona (MVP)

O serviço escolhe automaticamente 1 controladora por vez (MIDI IN + MIDI OUT) e cria duas portas virtuais:

- `{NomeDetectado} - KEYS` (hardware → PC)
- `{NomeDetectado} - LEDs` (PC → hardware)

Roteamento:

- hardware → `{NomeDetectado} - KEYS`
- `{NomeDetectado} - LEDs` → hardware

## Rodar em modo console (recomendado para testar)

Na pasta `bridge/`:

```powershell
dotnet build .\UsbMidiBridge.sln -c Release
dotnet run --project .\src\Bridge.Service\Bridge.Service.csproj -c Release
```

## Rodar como Windows Service

### Publicar

```powershell
dotnet publish .\src\Bridge.Service\Bridge.Service.csproj -c Release -r win-x64 --self-contained false
```

O executável ficará em algo como:

`.\src\Bridge.Service\bin\Release\net8.0\win-x64\publish\Bridge.Service.exe`

### Instalar (PowerShell como Administrador)

```powershell
$exe = (Resolve-Path ".\src\Bridge.Service\bin\Release\net8.0\win-x64\publish\Bridge.Service.exe").Path
sc.exe create "UsbMidiBridge" binPath= "`"$exe`"" start= auto
sc.exe start "UsbMidiBridge"
```

Para parar/remover:

```powershell
sc.exe stop "UsbMidiBridge"
sc.exe delete "UsbMidiBridge"
```

## Variáveis de ambiente

### Profiles

- `USB_MIDI_BRIDGE_PROFILE_MANIFEST_URL`
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

## Dicas de uso (Resolume/vMix/OBS)

- Use a porta `{NomeDetectado} - KEYS` como entrada para mapear botões/faders.
- Use a porta `{NomeDetectado} - LEDs` como saída para feedback/LEDs.

