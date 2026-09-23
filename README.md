# ⚡ Adaptive PC Optimizer

> **Otimizador de Desempenho, Baixa Latência e Central Pós-Formatação de Drivers para Windows 10 & 11.**
> Um único `.exe`, sem instalação, sem dependências externas — dê 2 cliques e pronto.
> Criado para **gamers competitivos**, **desenvolvedores de jogos** (Godot/Unity/Unreal) e
> **workloads multi-agentes de IA** (Orca/Antigravity) que precisam do PC respondendo rápido
> em tudo ao mesmo tempo.

[![Licença: MIT](https://img.shields.io/badge/Licença-MIT-green.svg)](LICENSE)
[![Windows 10 & 11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6.svg)](#)

---

## O que é

Um programa de linha de comando com menu interativo que ajusta o Windows pra baixa latência
(jogos, edição de código, múltiplos agentes de IA rodando ao mesmo tempo) e resolve o
pós-formatação (drivers e runtimes essenciais) — tudo com um **Ponto de Restauração do
Windows** e um diário de cada valor alterado criados automaticamente antes de qualquer mudança.

```
┌──────────────────────────────────────────────────────────────┐
│                      MENU DE OPÇÕES                          │
├──────────────────────────────────────────────────────────────┤
│  [1] ⚡ OTIMIZAR MEU PC (Adaptive PC Optimizer)               │
│  [2] 🔄 FORMATEI MEU PC AGORA (Instalar Drivers e Runtimes)  │
│  [3] 📊 VERIFICAR HARDWARE & STATUS DE LATÊNCIA              │
│  [4] 🛡️ DESFAZER OTIMIZAÇÃO (RESTAURAR VALORES ORIGINAIS)    │
│  [0] ❌ SAIR                                                  │
└──────────────────────────────────────────────────────────────┘
```

### Filosofia: nunca mentir sobre o que foi feito

Toda ação do programa reporta o resultado **real**, verificado no próprio sistema — nunca uma
mensagem de sucesso genérica independente do que aconteceu de fato:
- **[3] Status** lê o valor atual no Registro pra cada ajuste, não uma lista fixa que sempre diz "ativado".
- **[2] Drivers/Runtimes** confere se cada componente já está instalado (`winget list --exact`)
  antes de instalar — nunca reinstala por cima do que já existe, zero risco de conflito.
- Se uma ferramenta necessária (ex.: `winget`) não está disponível na máquina, o programa avisa
  isso explicitamente com instrução de como resolver, em vez de fingir que funcionou.
- **[1] Otimizar** conta cada escrita de Registro que de fato funcionou e lista as que falharam —
  não imprime "sucesso" fixo.

---

## [1] ⚡ Otimizar Meu PC

Aplica um conjunto de ajustes não-destrutivos, calibrados pra combinação exata de processador,
placa de vídeo, RAM e build do Windows detectados na hora:

| Área | O que muda |
|---|---|
| **CPU & Threads** | `Win32PrioritySeparation = 0x26` (quantum curto e variável, 3:1 foreground boost — em qualquer CPU) e `MMCSS` com 90% realtime / 10% background — jogo/IDE respondem rápido sem travar processos de fundo (ex.: agentes de IA rodando). |
| **GPU & DirectX** | Ativa **HAGS** *(Hardware Accelerated GPU Scheduling, quando a GPU suporta)*, fila de baixa latência `MaxFrameLatency = 1` e amplia o cache de shaders — reduz microtravamentos (*stutters*). |
| **Periféricos** | Mouse em **Raw Input 1:1** *(sem aceleração do Windows)*, teclado com repetição instantânea. |
| **Armazenamento & RAM** | Cache NTFS nível 2 *(leitura mais rápida de milhares de arquivos — código, assets)* e retenção de drivers do kernel na RAM física (só em máquinas com 16GB+). |
| **Rede** | `TCPNoDelay` e `TcpAckFrequency = 1` *(desativa o Algoritmo de Nagle — menor ping, resposta imediata de chamadas de API)*. |
| **Híbrido Dev + Jogo** | Prioridade alta pro Godot Engine e desativa a gravação passiva do GameDVR *(libera 5-10% de GPU)*. |

## [2] 🔄 Formatei Meu PC Agora (Central de Drivers & Runtimes)

Pensada pra logo depois de uma instalação limpa do Windows:

1. **Varredura PnP**: identifica barramentos/componentes sem driver instalado.
2. **Driver de GPU**: detecta o fabricante (**NVIDIA**, **AMD** ou **Intel**) e abre a página
   oficial de download/detecção correta no navegador. A instalação em si é sempre manual, feita
   pelo usuário na página do próprio fabricante — nenhum driver de GPU é baixado ou instalado
   às cegas por script (cada fabricante exige aceitar termos próprios, e não existe link de
   download silencioso oficial e estável pra isso).
3. **Drivers de chipset, áudio e rede**: abre a tela de Atualizações Opcionais do Windows
   (Windows Update é o canal oficial e mais seguro pra esses).
4. **Runtimes essenciais** (Microsoft Visual C++ 2015-2022 x64/x86, DirectX End-User Runtime,
   .NET Desktop Runtime 8): instalados silenciosamente via `winget`, **só depois de
   confirmar que cada um ainda não está presente** — zero reinstalação desnecessária, zero
   conflito. Se o `winget` não estiver disponível na máquina (acontece às vezes logo após
   formatar), o programa avisa isso claramente em vez de fingir sucesso, e indica como instalar
   o "App Installer" pela Microsoft Store pra resolver.

## [3] 📊 Verificar Hardware & Status de Latência

Mostra o hardware detectado agora (CPU, GPU, RAM, build do Windows) e lê, ao vivo, o valor atual
de cada ajuste que a opção [1] configura — útil pra confirmar se os ajustes já foram aplicados
antes, sem precisar rodar [1] de novo só pra saber.

## [4] 🛡️ Desfazer Otimização

Antes de alterar qualquer coisa, a opção [1] cria duas redes de segurança:

1. **Ponto de Restauração do Windows** ("Adaptive PC Optimizer") — reversão completa e oficial,
   incluindo os ajustes de rede via `netsh`. Se o Windows não permitir criar o ponto (Proteção do
   Sistema bloqueada por política, falta de espaço), o programa avisa e **pergunta** se deve
   continuar — nunca segue calado.
2. **Diário por valor** (`backups/journal_*.txt`) — o estado original de **cada** valor alterado,
   inclusive "não existia". A opção [4] usa esse diário pra desfazer exatamente o que foi mudado:
   devolve os valores antigos e **apaga** os que foram criados pela otimização.

O diário é amarrado ao nome do computador em que foi criado: um diário de outra máquina (por
exemplo, a pasta copiada de outro PC) é recusado. Diários em formato antigo, sem identificação de
origem, também são recusados — nesse caso use o Ponto de Restauração.

Ao final, a opção [4] oferece abrir a **Restauração do Sistema** pra reversão 100% completa.

---

## Motor

O `.exe` é **standalone de verdade** e usa **sempre** o motor nativo em C# (`src/Program.cs`),
sem depender de nada além do que o Windows já traz (`reg`, `netsh`, `pnputil`, PowerShell).
O core de otimização não usa `winget` — ele só entra na opção [2], que detecta a ausência dele e
avisa em vez de travar.

A implementação em JavaScript (`index.js` + `src/*.js`) continua no repositório só pra
desenvolvimento (`npm run optimize` etc.). Ela **não** tem o Ponto de Restauração nem o diário
por valor — a restauração dela é parcial. Pra uso real, use o `.exe`.

### Compilar o .exe

```
C:WindowsMicrosoft.NETFramework644.0.30319csc.exe /nologo /codepage:65001 /optimize+ /target:exe /out:AdaptivePCOptimizer.exe srcProgram.cs
```
(`/codepage:65001` é obrigatório: o fonte é UTF-8 sem BOM.)

---

## 🚀 Como Usar

1. Baixe o [`AdaptivePCOptimizer.exe`](AdaptivePCOptimizer.exe). Pra instalar no PC de outra pessoa,
   **copie só o `.exe`** — ele cria a própria pasta `backups/` na primeira execução.
2. Dê 2 cliques nele. O Windows vai pedir permissão de Administrador (UAC) — aceite, é
   necessário pra alterar o Registro e o Windows Update.
3. Escolha uma opção no menu (`1`–`4`) ou `0` pra sair.

Também dá pra rodar direto por linha de comando ou script:
```
AdaptivePCOptimizer.exe --apply      (equivalente à opção 1)
AdaptivePCOptimizer.exe --drivers    (equivalente à opção 2)
AdaptivePCOptimizer.exe --status     (equivalente à opção 3)
AdaptivePCOptimizer.exe --restore    (equivalente à opção 4)
```
Ou pelos atalhos prontos: [`optimize.bat`](optimize.bat) e [`restore.bat`](restore.bat).

---

## 📄 Licença

Distribuído sob licença **MIT** — gratuito e livre para uso pessoal e comercial em qualquer computador.
