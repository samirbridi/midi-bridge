# USB-MIDI 1.0 ⇄ MIDI 2.0 Bridge (Windows 10/11) — Plano de Implementação

## 1. Stack e diretrizes

- Linguagem/runtime: C# + .NET (Windows-only).
- Arquitetura: Windows Service (core) + Tray App (controle e status).
- Portas virtuais: teVirtualMIDI (P/Invoke) para criar portas por código.
- Compatibilidade:
  - Windows 10/11: sempre expor portas virtuais MIDI 1.0 (Split e opcional Combined).
  - Windows 11: deixar ganchos para MIDI 2.0/UMP (implementação pode ser fase 2, dependendo da API e disponibilidade real).

## 2. Estrutura de solução (projetos)

- `Bridge.Core`
  - Modelos de mensagem (MIDI 1.0), roteamento, sanitização, LED cache, hotplug state machine, perfis.
- `Bridge.IO.Midi1.WinMM` (ou similar)
  - Acesso a MIDI 1.0 físico via WinMM (P/Invoke) ou wrapper dedicado.
- `Bridge.IO.VirtualMidi.TeVirtualMidi`
  - Criação e I/O de portas virtuais via teVirtualMIDI.
- `Bridge.Service`
  - Host do Windows Service, gerenciamento multi-dispositivo, lifecycle, IPC para o tray.
- `Bridge.Tray`
  - UI mínima (tray), lista de dispositivos, perfil/mode, botões Reset/Resync, export diagnóstico.
- `Bridge.Profiles`
  - Carregamento/validação de perfis JSON e versão de schema.
- `Bridge.Diagnostics`
  - Logging, recorder de eventos, export de bundles de diagnóstico.

## 3. Entregas por fases

### Fase 0 — Prova técnica (MIDI 1.0 “pass-through”)

Objetivo: provar que conseguimos roteamento estável com portas virtuais Split.

- Implementar `TeVirtualMidiProvider`:
  - Criar 2 portas por dispositivo: `Bridge - <Name> - Keys` e `Bridge - <Name> - LEDs`.
  - API de leitura/escrita de bytes/mensagens MIDI 1.0.
- Implementar `WinMM` device I/O:
  - Enumerar dispositivos MIDI IN/OUT.
  - Abrir streams e receber callbacks.
- Implementar roteamento mínimo:
  - Hardware IN → porta Keys (virtual OUT do bridge para apps).
  - Porta LEDs (virtual IN do bridge vindo do app) → Hardware OUT.
- Criar tray simples:
  - Mostrar dispositivos e portas criadas.
  - Start/Stop por dispositivo.

Critério de aceite:
- vMix/Resolume recebe atalhos pela porta Keys e controla LEDs pela porta LEDs.

### Fase 1 — Resiliência + anti-glitch + hotplug

Objetivo: atacar o problema real de “atalhos quebrando” e “LED confuso”.

- Hotplug / reenumeração:
  - Watcher de dispositivos (poll + diffs, ou notificações do Windows quando viável).
  - State machine por dispositivo: Online/Offline/Reconnecting.
  - Persistir associação (device key → perfil/mode) para não exigir reconfiguração após reconectar.
- Sanitização:
  - Rate-limit configurável (por tipo de mensagem e por perfil).
  - Coalescing de CC repetidos (janela curta).
  - Filtros por perfil.
- Panic/Reset:
  - Comandos All Notes Off / Reset Controllers no reconectar e por ação do usuário no tray.
- LED Feedback Manager:
  - Cache de último estado (por canal, note/cc, conforme perfil).
  - Resync: init/clear do perfil + reenviar estado.

Critério de aceite:
- Desconectar/reconectar sem “bagunçar” LEDs (ou recuperando com resync automático).

### Fase 2 — Perfis (Genérico + Akai inicial)

Objetivo: reduzir configuração manual e melhorar “comportamento padrão”.

- Formato de perfil JSON versionado:
  - Match: VID/PID/nome.
  - LED mode: note/cc, canal, valores on/off, init/clear.
  - Sanitização: rate-limit, debounce, filtros.
- Perfil genérico sempre disponível.
- Perfis Akai (MVP):
  - Começar com 1–2 modelos populares e evoluir.
- UI de perfil:
  - Escolha automática (match) com override manual no tray.

Critério de aceite:
- Dispositivo Akai reconhecido e com LED resync mais consistente sem ajustes manuais.

### Fase 3 — Diagnóstico e suporte (para casos reais)

Objetivo: tornar o produto “suportável” sem acesso ao hardware do usuário.

- Monitor/Recorder:
  - Capturar streams por porta com timestamps.
  - Exportar arquivo (JSON/CSV/bin) para reproduzir bugs.
- Bundle de diagnóstico:
  - Versão, lista de devices, perfis aplicados, logs, estatísticas de drops/coalescing.
- Detecção de loop:
  - Identificar padrões de eco/loopback e cortar automaticamente.

### Fase 4 — MIDI 2.0/UMP (Windows 11, opcional)

Objetivo: expor uma porta MIDI 2.0 e traduzir para hardware 1.0 quando tecnicamente viável.

- Criar abstrações:
  - `IMidiEndpoint` (MIDI 1.0 bytes vs UMP words).
  - `ITranslator` (MIDI1↔UMP).
- Implementar tradução do “básico” (mensagens comuns).
- Implementar endpoints UMP apenas quando houver API confiável no Windows 11.

Critério de aceite:
- Apps UMP conseguem controlar o hardware 1.0 para o subconjunto traduzível, sem quebrar o MIDI 1.0.

## 4. Padrões de engenharia

- Separar “engine” (Bridge.Core) da UI/Service.
- IPC Service↔Tray:
  - Canal local (named pipes) para status/comandos (Start/Stop/Reset/Export).
- Logging:
  - Logs rotativos e níveis (Info/Debug).
  - Nunca registrar conteúdo sensível; eventos MIDI podem ser logados com opção do usuário.
- Config/persistência:
  - Armazenar preferências e binding dispositivo→perfil localmente.

## 5. Testes

- Testes unitários (Core):
  - Sanitização (coalescing/rate-limit).
  - LED cache/resync.
  - Parser/serializer de perfis JSON.
- Teste de integração local (Windows):
  - Loopback via portas virtuais (simular app enviando LED e validar chegada no hardware endpoint simulado).
- Teste manual guiado:
  - Checklist para vMix e Resolume (como selecionar Keys/LEDs).

## 6. Distribuição

- Installer:
  - Instalar serviço + tray + dependências do teVirtualMIDI (se redistribuível conforme licença).
- Primeiro MVP pode pedir que o usuário instale o driver/SDK necessário; versão posterior empacota tudo.

## 7. Riscos e mitigação

- API MIDI 2.0 no Win11: manter como fase opcional; não bloquear o valor do produto (MIDI 1.0 + estabilidade).
- Portas virtuais e licenciamento: confirmar termos do teVirtualMIDI para distribuição.
- Variedade de devices: começar com genérico robusto + perfis incrementais.

