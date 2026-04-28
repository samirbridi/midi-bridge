# USB-MIDI 1.0 ⇄ MIDI 2.0 Bridge (Windows 10/11) — Design

## 1. Objetivo

Criar um software do tipo “serviço/daemon + tray” para Windows 10 e 11 que:

- Mantenha controladoras USB-MIDI 1.0 (firmware antigo, sem atualização) utilizáveis em sistemas atualizados.
- Reduza falhas práticas observadas em uso real: perda/instabilidade de atalhos (entrada) e LEDs/activators confusos ou sem retorno (saída).
- Exponha portas virtuais para que vMix/Resolume/DAWs e demais apps se conectem ao Bridge em vez de falar direto com o hardware.
- Opcionalmente exponha uma porta MIDI 2.0 (UMP) no Windows 11 quando a pilha/sistema suportar, traduzindo para o hardware MIDI 1.0.

## 2. Contexto e Premissas

- A controladora física é USB-MIDI 1.0.
- No Windows 10, o ecossistema real é predominantemente MIDI 1.0; MIDI 2.0/UMP pode não estar disponível de forma consistente para apps.
- O Bridge deve funcionar como “ponte” em modo usuário, evitando dependência de firmware do fabricante.
- O Bridge pode depender de um driver/solução de porta virtual instalada no Windows (MVP). Evolução futura pode empacotar solução própria.

## 3. Metas e Não-Metas

### Metas

- Suportar múltiplos dispositivos físicos simultaneamente.
- Criar e manter portas virtuais por dispositivo.
- Roteamento bidirecional e resiliente (hotplug).
- “Sanitização” do stream MIDI para reduzir glitches práticos.
- Perfis: modo genérico + perfis por família/modelo (ex.: Akai).
- Observabilidade: logs e export de diagnóstico para troubleshooting.

### Não-Metas (v1)

- Implementar substituição completa de driver USB no kernel.
- Garantir suporte completo a MIDI 2.0 (Profiles/Property Exchange) quando o hardware é MIDI 1.0.
- Converter tudo com fidelidade perfeita; em alguns casos a tradução é por degradação.

## 4. Modelo de Portas Virtuais (MIDI 1.0)

### 4.1 Motivação: “atalhos” vs “LEDs/activators”

Em muitos ambientes de uso (vMix, Resolume e similares), o operador pensa em dois fluxos independentes:

- **Atalhos**: botões/pads/knobs que entram no PC (Hardware → App).
- **Activators/LEDs**: feedback que volta para o hardware (App → Hardware).

Na prática, MIDI 1.0 é bidirecional (IN/OUT), mas certas rotinas de configuração de software e a experiência do usuário ficam melhores quando esses fluxos aparecem como “portas” separadas.

### 4.2 Proposta: dois modos de exposição MIDI 1.0

- **Modo Split (padrão no MVP)**: criar duas portas/dispositivos virtuais por controladora.
  - **Porta “Keys/Shortcuts”** (foco em entrada):
    - App consome essa porta como fonte de atalhos.
    - Roteamento principal: Hardware → Porta.
    - Qualquer saída enviada pelo app para essa porta é descartada (ou opcionalmente logada).
  - **Porta “LEDs/Activators”** (foco em saída):
    - App envia feedback de LEDs/activators para essa porta.
    - Roteamento principal: Porta → Hardware.
    - A entrada dessa porta pode ficar vazia ou opcionalmente espelhar o Hardware → App para debug.
- **Modo Combined (opcional)**: criar uma porta virtual única bidirecional por controladora (IN/OUT).
  - Útil para apps que já lidam bem com uma porta bidirecional.

O Bridge deve permitir trocar Split/Combined por dispositivo, sem exigir alterações no hardware.

### 4.3 Requisito do provedor de porta virtual

Nem todo driver de porta virtual permite criar portas estritamente unidirecionais. Para compatibilidade:

- O Bridge deve suportar portas virtuais “bidirecionais” e impor direção via regras de roteamento (descartar o sentido não desejado).

### 4.4 Nomenclatura e unicidade (multi-dispositivo)

Para suportar múltiplas controladoras ao mesmo tempo, o Bridge deve:

- Gerar nomes de portas virtuais únicos e estáveis, por exemplo:
  - `Bridge - <DeviceName> - Keys`
  - `Bridge - <DeviceName> - LEDs`
- Incluir um sufixo quando houver colisão (`#2`, `#3`) e expor no tray o vínculo “porta virtual ↔ dispositivo físico”.

## 5. Exposição MIDI 2.0 (UMP)

### 5.1 Disponibilidade por sistema

- **Windows 10**: operar com MIDI 1.0 (portas virtuais MIDI 1.0).
- **Windows 11**: se o sistema e a pilha MIDI oferecerem suporte, expor adicionalmente uma porta MIDI 2.0/UMP virtual por controladora (ou por par Split/Combined), traduzindo para o hardware MIDI 1.0.

### 5.2 Estratégia “dual stack”

Por dispositivo físico, o Bridge pode expor:

- MIDI 1.0 Split (Keys + LEDs) sempre.
- MIDI 2.0 UMP opcional (quando disponível).

Apps antigos usam MIDI 1.0; apps novos podem preferir UMP.

## 6. Tradução (MIDI 1.0 ⇄ MIDI 2.0)

### 6.1 MIDI 1.0 → UMP

Converter quando possível:

- Note On/Off
- Control Change
- Program Change
- Pitch Bend
- Channel Pressure / Poly Aftertouch
- SysEx (com limites; ver 6.3)

### 6.2 UMP → MIDI 1.0

- Degradar mensagens de alta resolução para 7-bit/14-bit quando aplicável.
- Se o app enviar recursos sem equivalente no MIDI 1.0 (Profiles/Property Exchange), o Bridge deve:
  - ignorar de forma segura, e/ou
  - registrar no diagnóstico como “não suportado” sem interromper o fluxo.

### 6.3 SysEx e fragmentação

- O Bridge deve lidar com SysEx fragmentado e tempo entre fragmentos.
- Deve existir limite configurável por dispositivo (tamanho e timeout) para evitar travamentos.

## 7. Estabilização do Fluxo (anti-glitch)

O Bridge deve incluir um pipeline de “sanitização” (habilitável por perfil):

- **Ordenação e coalescing**: reduzir tempestade de CC repetidos em janelas curtas.
- **Rate limit**: evitar flood acidental que derruba feedback/LED.
- **Debounce opcional**: para botões instáveis (em perfis conhecidos).
- **Panic/Reset**: comandos de segurança (All Notes Off, Reset Controllers) acionados por:
  - reconexão do dispositivo,
  - troca de perfil,
  - detecção de stream inválido (configurável).

## 8. Gerenciador de LEDs/Activators

Para reduzir “LED confuso”:

- Manter cache do último estado por (canal, note/cc) conforme perfil.
- Em eventos de resync (reconnect, reenumeração, troca de perfil):
  - aplicar sequência de init/clear do perfil,
  - reenviar estado conhecido para restaurar LEDs.

## 9. Hotplug e Resiliência

Requisitos:

- Detectar conexão/desconexão sem exigir reinício do PC.
- Se o dispositivo cair, manter as portas virtuais (quando possível) e sinalizar “dispositivo offline” no tray.
- Ao retornar, reabrir IN/OUT, reanexar roteamento e executar resync de LEDs.
- Permitir “fixar” o mapeamento de perfil e modo Split/Combined por identificador do dispositivo (quando possível), para que reconexões não exijam reconfiguração.

## 10. Perfis

### 10.1 Perfil genérico

- Sem suposições: apenas roteamento, sanitização básica e opção de panic/resync mínimo.

### 10.2 Perfis específicos (ex.: Akai)

Campos típicos:

- Critérios de match: VID/PID, nome do dispositivo, etc.
- Canal padrão e mapeamento de LEDs (Note vs CC).
- Valores de LED on/off e mensagens de init/clear.
- Regras de sanitização específicas (rate-limit/filters).

Formato sugerido: JSON versionado.

## 11. Componentes do Sistema

- **Windows Service (Core)**
  - Descoberta de dispositivos físicos
  - Lifecycle por dispositivo
  - Roteamento e tradução
  - Logs/telemetria local
- **Tray UI**
  - Lista de dispositivos e status
  - Seleção de perfil e modo Split/Combined
  - Export de diagnóstico
  - Atalho para “Reset/Resync”
- **VirtualPortProvider (abstração)**
  - Implementação do MVP usando driver/solução existente no Windows
  - Interface estável para permitir troca futura para solução embutida

## 12. Critérios de Aceite (MVP)

- Conectar 2+ controladoras simultâneas e criar portas virtuais distintas.
- Em vMix/Resolume:
  - Receber atalhos via porta “Keys/Shortcuts”.
  - Enviar feedback/LED via porta “LEDs/Activators”.
- Hotplug: desconectar e reconectar mantendo o Bridge funcional e executando resync.
- Tradução básica MIDI 1.0 ↔ UMP ativada quando ambiente suportar (Win11), sem travar o roteamento MIDI 1.0.
