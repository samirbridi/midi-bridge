# USB MIDI Bridge — MVP (Bridge Real) Design

## Objetivo

Implementar o MVP funcional do Bridge no Windows (.NET 8) para 1 controladora física por vez, usando:

- MIDI físico via WinMM
- Portas virtuais via teVirtualMIDI
- Roteamento bidirecional com sanitizer e cache de LEDs
- Seleção automática de perfil via Profile Store (manifest público)

## Escopo do MVP

- 1 sessão ativa por vez (1 par IN/OUT físico + 2 portas virtuais)
- 2 portas virtuais separadas:
  - `{NomeDetectado} - KEYS`
  - `{NomeDetectado} - LEDs`
- Roteamento:
  - Físico IN → Virtual KEYS
  - Virtual LEDs → Físico OUT
- Observabilidade via logs do serviço (sem UI de tray no MVP)

## Fora de escopo (por enquanto)

- Multi-dispositivo simultâneo
- UI (tray/wizard) para seleção manual
- Detecção VID/PID (SetupAPI)
- SysEx completo no WinMM (neste código base o output atual só envia ShortMsg)

## Componentes

- Bridge.Service: coordena a sessão, detecta dispositivos, aplica profile store e mantém loops de execução.
- Bridge.IO.Midi1.WinMM:
  - WinMmMidiEnumerator: lista entradas e saídas.
  - WinMmMidiInput: fonte assíncrona (callback → channel).
  - WinMmMidiOutput: sink (ShortMsg).
- Bridge.IO.VirtualMidi.TeVirtualMidi:
  - TeVirtualMidiPort: source+sink para a porta virtual.
- Bridge.Core:
  - MidiRouter: loop source→sink.
  - MidiSanitizer: rate limit + coalescing de CC.
  - LedStateCache: snapshot de mensagens LED-like para “restore” ao reconectar.
  - ProfileStore: update+cache+resolver de perfis.

## Naming das portas virtuais

### Nome base

- `NomeDetectado` = nome do MIDI IN físico selecionado (WinMM).

### Formatos

- `{NomeDetectado} - KEYS`
- `{NomeDetectado} - LEDs`

### Higienização

- Remover `\r`, `\n`, `\t`
- Trim e colapsar espaços múltiplos
- Se vazio, usar `USB MIDI Bridge`
- Truncar para um máximo seguro preservando o sufixo:
  - Ex.: se exceder o limite, truncar o prefixo e manter ` - KEYS` / ` - LEDs`

## Seleção do dispositivo (MVP)

- Selecionar o MIDI IN físico:
  - Default: primeiro `WinMmMidiEnumerator.ListInputs()`
  - Futuro: override via configuração/CLI/tray.
- Selecionar o MIDI OUT físico:
  - Preferir o primeiro output com nome semelhante ao IN (contains case-insensitive).
  - Fallback: primeiro `ListOutputs()`.

## Profiles e auto-seleção

- Resolver profile por nome do IN físico:
  - `ProfileStore.ResolveProfileForDevice(null, null, input.Name)`
  - Fallback para built-ins (Akai→Generic) se necessário.
- Aplicar profile no MVP:
  - Sanitizer: mapear `sanitization.maxMessagesPerSecond` e `coalesceWindowMs` para `MidiSanitizerOptions`.
  - LEDs: usar `LedStateCache` para snapshot/replay (sem regras avançadas por vendor neste MVP).

## Roteamento e loops

### Rota 1 (entrada)

- Source: `WinMmMidiInput`
- Sink: `TeVirtualMidiPort` (porta KEYS)
- Sanitizer habilitado

### Rota 2 (LEDs)

- Source: `TeVirtualMidiPort` (porta LEDs)
- Sink: `WinMmMidiOutput`
- Sanitizer habilitado
- `LedStateCache.TryApply` em cada mensagem LED-like para manter snapshot

## Resiliência / reconexão

- Se falhar abrir qualquer backend (WinMM/teVirtualMIDI), a sessão não sobe; o serviço loga e tenta novamente no próximo tick.
- Ao detectar mudança no snapshot de devices (contagem/nome), reavaliar e recriar sessão.
- Ao recriar o output físico:
  - Reenviar `LedStateCache.Snapshot()` para restaurar estado visual (best-effort).

## Segurança

- Profile Store: somente JSON + validação (sem execução de código).
- Sem logar tokens/chaves/segredos.
- Tratar URLs do manifest como configuração (env var), com default apontando para o repositório público.

## Validação

- Testes unitários continuam cobrindo resolver do manifest e fallback wildcard.
- Teste manual (Windows):
  - Iniciar o serviço
  - Confirmar criação das portas virtuais com o nome `{NomeDetectado} - KEYS/LEDs`
  - Confirmar que mensagens do hardware chegam no app via porta KEYS
  - Confirmar que mensagens enviadas para a porta LEDs chegam no hardware (LEDs/feedback)

