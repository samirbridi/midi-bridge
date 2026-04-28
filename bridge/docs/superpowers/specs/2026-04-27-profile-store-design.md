# Profile Store (perfis de controladoras) — Design

## 1. Objetivo

Criar um sistema de perfis para o USB-MIDI Bridge que:

- Permita suportar muitas controladoras (Akai, Novation, Behringer, Korg, etc.) sem exigir release do aplicativo a cada novo modelo.
- Faça auto-detecção de perfil por dispositivo e aplique automaticamente.
- Suporte atualização automática (on por padrão), com segurança e rollback.
- Seja open source e orientado à comunidade (repositório público).

## 2. Conceitos

- **Perfil**: arquivo JSON que descreve regras de LED/feedback, sanitização e init/clear específicas do dispositivo ou família.
- **Bundle offline**: conjunto de perfis embarcados no instalador (garante funcionamento “out of the box”).
- **Store online**: repositório público com perfis adicionais e atualizações.
- **Cache local**: cópia dos perfis baixados e a versão anterior para rollback.

## 3. Estrutura do repositório público

Estrutura sugerida:

- `index/manifest.json`
- `schema/profile.schema.json`
- `profiles/<vendor>/<model>.json`

O repositório pode ser hospedado em GitHub, com releases opcionais para bundles offline.

## 4. Manifesto (manifest.json)

O `manifest.json` lista todos os perfis disponíveis e metadados para seleção e segurança.

Campos por perfil (mínimo):

- `id`: string única e estável
- `displayName`: nome legível
- `schemaVersion`: versão do schema do perfil
- `match`: regras de seleção
  - `vid`: inteiro (opcional)
  - `pid`: inteiro (opcional)
  - `nameContains`: string (opcional)
- `url`: URL HTTPS do JSON do perfil
- `sha256`: hash do conteúdo do perfil
- `sizeBytes`: tamanho do arquivo (limite para proteção)
- `tags`: lista opcional (ex.: `resolume`, `vmix`, `obs`, `grid`, `fader`)

O Bridge deve tratar `manifest.json` como fonte de verdade do store.

## 5. Schema de perfil (profile.schema.json)

O schema deve:

- Validar campos obrigatórios.
- Impor limites (ex.: valores numéricos, tamanho de arrays, comprimento de strings).
- Restringir configurações perigosas (ex.: limites máximos de SysEx e regras).

## 6. Seleção automática de perfil

Ordem de prioridade (por dispositivo):

1. **Match exato VID/PID**
2. Match por `nameContains`
3. Match por “família” (quando existir perfil de família com regras menos específicas)
4. Perfil genérico

Regras adicionais:

- Se o usuário “fixar” um perfil no tray, o override vence a seleção automática.
- O tray deve exibir o “nível de confiança” do match (VID/PID vs nome vs fallback).

## 7. Identidade do dispositivo (Windows)

Para um match robusto, o Bridge deve tentar obter:

- Nome do dispositivo MIDI (WinMM)
- Se possível, VID/PID do dispositivo USB correspondente

Observação: mapear WinMM → instância USB pode exigir uso de APIs do Windows (ex.: SetupAPI). Quando VID/PID não for obtido, o Bridge deve cair para match por nome.

## 8. Atualização automática (padrão)

Comportamento:

- Checar updates no startup e periodicamente (configurável).
- Baixar apenas via HTTPS.
- Armazenar `manifest.json` e perfis em cache local.

Validações obrigatórias:

- Validar `manifest.json` (JSON bem formado e campos esperados).
- Validar SHA-256 de cada perfil baixado contra o `sha256` do manifest.
- Validar o JSON do perfil contra `profile.schema.json` (embarcado no app).
- Rejeitar perfis acima de `sizeBytes` ou limites locais (proteção).
- Nunca executar código vindo do store; perfis são dados e devem ser interpretados de forma restrita pelo Bridge.

## 9. Rollback e quarentena (automático)

O Bridge deve ser resiliente a perfis “ruins”:

- Para cada perfil, manter:
  - versão ativa
  - versão anterior (última boa)
- Se um perfil atualizado falhar validação ou causar erros em runtime, o Bridge deve:
  - reverter para a versão anterior automaticamente
  - marcar a versão nova como “quarentenada” para aquele dispositivo
  - registrar no diagnóstico (com motivo e timestamp)

Critérios típicos para “quebrou em runtime”:

- Exceptions recorrentes ao aplicar/init/resync
- Detecção de loop no roteamento associado ao perfil
- Taxa de drops/flood acima de limite configurado, imediatamente após aplicar o perfil

## 12. Privacidade

- O Bridge não deve enviar dados do usuário para o store.
- Atualização é pull (download de manifest/perfis) e pode ser desativada.

## 10. Contribuição da comunidade

- Perfis entram via Pull Request.
- CI valida:
  - conformidade com schema
  - colisão de `id`
  - duplicidade de VID/PID (com regras)
  - hash e tamanho

## 11. Critérios de aceite

- Bridge opera com bundle offline sem internet.
- Bridge consegue baixar manifest e atualizar perfis automaticamente.
- Bridge seleciona perfil por VID/PID quando disponível e por nome quando não.
- Bridge faz rollback automático quando um perfil novo é inválido ou instável.
