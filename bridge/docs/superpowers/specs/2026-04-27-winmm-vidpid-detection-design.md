USB MIDI Bridge — Detecção VID/PID (Best-effort) via SetupAPI
Objetivo
Melhorar a auto-seleção de perfis (Profile Store) detectando VID/PID do hardware MIDI no Windows, mantendo fallback total para o comportamento atual baseado em nome.

Regras:

Se VID/PID puderem ser detectados, priorizar match por VID/PID.
Se não puder, continuar com NameContains e fallback * (genérico), sem quebrar o bridge.
Sem UI no MVP; apenas logs e variáveis de ambiente opcionais já existentes para override.
Contexto atual
O bridge enumera dispositivos MIDI via WinMM (midiInGetDevCapsW, midiOutGetDevCapsW), obtendo DeviceId e Name.
O Profile Store já suporta resolução por VID/PID e por NameContains.
No Windows, WinMM não expõe diretamente VID/PID do dispositivo; é necessário consultar APIs de device enumeration (SetupAPI/Registry).
Abordagem escolhida
Implementar uma camada “best-effort” no Windows usando SetupAPI para obter Hardware IDs e extrair USB\\VID_xxxx&PID_yyyy, correlacionando com os nomes retornados pelo WinMM.

Saídas do recurso
Novo modelo de identidade (conceito)
MidiDeviceIdentity
int DeviceId (WinMM)
string Name (WinMM)
int? Vid
int? Pid
Novo resolvedor de VID/PID (conceito)
WinMmVidPidResolver
Entrada: string winMmDeviceName
Saída: (int? vid, int? pid) (best-effort)
Como a correlação funciona (best-effort)
Enumerar dispositivos de áudio/MIDI via SetupAPI (apenas Windows).
Ler propriedades que contenham:
Friendly name / device description (texto)
Hardware IDs (multisz) contendo VID_ e PID_ quando for USB
Normalizar strings para comparação:
Trim, remover \r\n\t
Colapsar espaços
Comparação case-insensitive
Tentar casar o winMmDeviceName com o “friendly name” do SetupAPI:
contains em ambos os sentidos
fallback por “token overlap” simples (opcional) caso necessário
Se casar, extrair VID/PID do Hardware ID:
Regex/parse de VID_([0-9A-Fa-f]{4}) e PID_([0-9A-Fa-f]{4})
Se falhar em qualquer etapa, retornar null para ambos.
Integração no bridge
Resolução de perfil
Onde hoje resolvemos:

ResolveProfileForDevice(null, null, input.Name)
Passará a resolver (quando disponível):

ResolveProfileForDevice(vid, pid, input.Name)
Ordem de prioridade pretendida:

VID/PID (mais preciso)
NameContains
NameContains="*" (fallback genérico)
Seleção automática de device (ranking)
O ranking de “melhor IN” deve considerar:

Se VID/PID detectados resultam em profile específico (não genérico) → maior score
Se somente name-match resulta em profile específico → score menor
Fallback total para o primeiro IN quando tudo for genérico
Observação: filtros já existentes devem continuar ativos:

Ignorar portas virtuais do próprio bridge ( - KEYS / - LEDs)
Compatibilidade e falhas
Drivers/transportes que não sejam USB (ex.: virtual ports de terceiros) podem não ter VID/PID.
A correlação WinMM ↔ SetupAPI não é garantida em 100% dos casos; por isso a estratégia é best-effort.
Em caso de erro, o bridge deve:
logar em nível Debug/Information (sem spam)
seguir funcionando por NameContains normalmente
Segurança e privacidade
Nenhuma informação sensível é coletada.
Apenas nomes de device (já visíveis no sistema) e VID/PID são usados localmente para seleção de perfil.
Nenhum upload de inventário; sem telemetria.
Testes/validação
Unit tests:
Parser de Hardware ID → extrai VID/PID corretamente
Normalização de nome
Estratégia de match (casos simples)
Teste manual (Windows):
Conectar um device USB MIDI conhecido (Akai/Novation/etc.)
Confirmar em log que VID/PID foram detectados e profile selecionado por VID/PID
Confirmar fallback funcionando quando nenhum VID/PID for encontrado
