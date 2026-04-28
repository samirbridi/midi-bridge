# USB MIDI Bridge — VID/PID (SetupAPI) Implementation Plan

## Objetivo

Implementar detecção `VID/PID` best-effort no Windows para melhorar seleção automática de perfil, sem quebrar o comportamento atual baseado em nome.

## Entregáveis

- Parser reutilizável para extrair `VID/PID` de strings de Hardware ID (`USB\\VID_XXXX&PID_YYYY`).
- Resolver best-effort WinMM ↔ SetupAPI para obter `VID/PID` a partir do `Name` do WinMM.
- Integração no `Bridge.Service` para:
  - passar `vid/pid` para `ResolveProfileForDevice(vid, pid, name)` quando possível
  - manter fallback para `NameContains`/`*` quando não for possível
- Testes unitários (parser e normalização).

## Passos

### 1) Parser `VID/PID` (Core)

- Criar utilitário em `Bridge.Core` para:
  - `TryParseVidPid(string hardwareId, out int vid, out int pid)`
  - aceitar maiúsculas/minúsculas
  - tolerar múltiplos formatos comuns (ex.: `USB\\VID_XXXX&PID_YYYY`, `HID\\VID_XXXX&PID_YYYY`)

### 2) SetupAPI enumerator/resolver (WinMM)

- Adicionar P/Invoke do SetupAPI no projeto `Bridge.IO.Midi1.WinMM`:
  - `SetupDiGetClassDevsW`
  - `SetupDiEnumDeviceInfo`
  - `SetupDiGetDeviceRegistryPropertyW` (FriendlyName/DeviceDesc/HardwareId)
  - `SetupDiDestroyDeviceInfoList`
- Enumerar dispositivos de classe Media (`GUID_DEVCLASS_MEDIA`) ou fallback por classe “present devices” e filtrar por presença de `VID_`/`PID_`.
- Para cada device, coletar:
  - `FriendlyName` (ou `DeviceDesc` se não existir)
  - `HardwareId` (MULTI_SZ)
  - extrair `VID/PID` do primeiro HardwareId que casar.
- Implementar `WinMmVidPidResolver.TryResolve(string winMmName)`:
  - normalizar strings
  - tentar match por contains em ambos sentidos
  - retornar `null` quando não casar

### 3) Integração no Bridge.Service

- Ao resolver profile do input:
  - tentar `TryResolve` para obter `vid/pid`
  - chamar `_profileStore.ResolveProfileForDevice(vid, pid, input.Name)`
- No ranking de `PickInput`:
  - aumentar score quando `vid/pid` resultar em profile não-genérico
- Logs em nível `Information` apenas quando detectar `vid/pid` e/ou quando a seleção mudar.

### 4) Testes

- Adicionar testes em `Bridge.Core.Tests` para:
  - parse de `VID/PID` com diferentes strings
  - falhas seguras (strings sem VID/PID)
  - normalização básica de nome (remover whitespace e colapsar espaços)

## Critérios de aceite

- Em Windows, quando `VID/PID` forem detectáveis, o bridge seleciona o profile por `VID/PID` (prioridade maior que nome).
- Quando `VID/PID` não forem detectáveis, o bridge continua funcionando via name-match e fallback `*`.
- Build e testes passam.

