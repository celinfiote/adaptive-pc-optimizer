# ⚡ Adaptive PC Optimizer (Universal Tweak & Driver Engine)

> **Software Universal e Adaptativo para Windows 10 & 11 (.EXE Standalone)**  
> Otimizador de Desempenho, Baixa Latência e **Central Pós-Formatação de Drivers Oficiais**.  
> Criado para **Gamers Competitivos**, **Desenvolvedores de Jogos (Godot/Unity/Unreal)** e **Workloads Multi-Agentes de IA (Orca/Antigravity)**.
>
> ⚠️ **Correção importante (28/08/2026)**: versões anteriores do `.exe` dependiam
> silenciosamente do Node.js estar instalado na máquina — numa máquina sem Node (o caso comum,
> já que é uma ferramenta de desenvolvedor, não algo que jogadores têm por padrão), a opção
> [1] caía num fallback quebrado que só tentava 1 comando de `winget` e travava aí, sem
> aplicar nenhum ajuste real e sem avisar o usuário disso. **Agora o `.exe` tem um motor
> nativo completo em C#** que aplica exatamente os mesmos ajustes sem depender de Node.js
> nem de `winget` — o programa é standalone de verdade agora, como o nome sempre prometeu.

---

## 🎯 As Duas Funções Principais do Programa

```
┌──────────────────────────────────────────────────────────────┐
│                      MENU DE OPÇÕES                          │
├──────────────────────────────────────────────────────────────┤
│  [1] ⚡ OTIMIZAR MEU PC (Adaptive PC Optimizer)               │
│  [2] 🔄 FORMATEI MEU PC AGORA (Instalar Drivers e Runtimes)  │
│  [3] 📊 VERIFICAR HARDWARE & STATUS DE LATÊNCIA              │
│  [4] 🛡️ RESTAURAR BACKUP ORIGINAL DO REGISTRO                │
│  [0] ❌ SAIR                                                  │
└──────────────────────────────────────────────────────────────┘
```

---

### 1. ⚡ [1] OTIMIZAR MEU PC (Adaptive Tweak Engine)
Aplica o conjunto de otimizações não-destrutivas calibradas para a sua combinação exata de processador, placa de vídeo, RAM e armazenamento:
- **CPU & Escalonamento de Threads**: `Win32PrioritySeparation = 0x26` *(3:1 Foreground Boost)* e `MMCSS` com 90% realtime / 10% background para jogos fluidos e IDEs rápidas sem travar os agentes de IA.
- **Drivers de Vídeo & DirectX**: Habilita **HAGS** *(Hardware Accelerated GPU Scheduling)*, fila de baixa latência `MaxFrameLatency = 1` e aloca **10GB de Shader Cache** para eliminar micro-travamentos (*stutters*).
- **Comunicação com Periféricos**: Mouse com resposta **Raw Input 1:1** *(sem aceleração do Windows)*, teclado com **0ms de atraso** e fila HID ajustada para mouses de 1000Hz a 8000Hz.
- **Armazenamento & RAM**: Cache de tabelas NTFS nível 2 *(leitura ultra-rápida de código/assets)* e retenção de drivers do kernel na RAM física.
- **Pilha de Rede TCP/IP**: `TCPNoDelay = 1` e `TcpAckFrequency = 1` *(desativa o Algoritmo de Nagle para menor ping e resposta imediata de APIs)*.
- **Híbrido Game Dev**: Prioridade de alta performance para a engine Godot e desativação de gravação passiva do GameDVR *(libera 5-10% de GPU)*.

---

### 2. 🔄 [2] FORMATEI MEU PC AGORA (Central de Drivers & Runtimes)
Acabou de formatar o computador? Essa opção detecta o hardware do computador e instala/atualiza automaticamente todos os componentes e drivers oficiais necessários para rodar qualquer jogo ou motor gráfico do zero:

> ✅ **Verificação anti-conflito (28/08/2026)**: antes de instalar QUALQUER runtime, o
> programa consulta `winget list --id <pacote> --exact` pra checar se já está instalado —
> se estiver, pula e mostra "já instalado", nunca reinstala por cima. Zero risco de
> conflito com o que já existe na máquina. Driver de GPU e drivers de chipset/periféricos
> nunca são instalados às cegas por script (ver itens 5 e 6 abaixo) — só os runtimes da
> Microsoft/Node.js passam por instalação silenciosa, e mesmo esses só depois de confirmar
> que não estão presentes ainda.

1. **Varredura PnP de Dispositivos**: Identifica barramentos e componentes sem driver.
2. **Microsoft Visual C++ Redistributable All-in-One**: Instala silenciosamente todas as bibliotecas C++ de 2015 a 2022 (x86 e x64) — só se ainda não estiverem instaladas.
3. **DirectX End-User Runtime**: Atualiza todas as bibliotecas legadas e modernas do DirectX 9, 10, 11 e 12.
4. **Microsoft .NET Desktop Runtime 8**: Runtimes necessários para ferramentas de desenvolvimento e jogos modernos.
5. **Driver Oficial de GPU**: Detecta se a placa é **NVIDIA**, **AMD** ou **Intel** e abre a página oficial de download/detecção do fabricante certo no navegador — a instalação de driver de GPU é sempre feita manualmente pelo usuário na página oficial, nunca às cegas por script (cada fabricante exige aceitar termos próprios e não existe link de download silencioso oficial e estável).
6. **Drivers de Chipset/Áudio/Rede**: Abre a tela de Atualizações Opcionais do próprio Windows (via Windows Update, o canal oficial do sistema).
7. **Node.js LTS**: Instalado via `winget` quando disponível — desbloqueia a versão completa (Node.js) do optimizer nas próximas execuções, com detecção de hardware mais completa que o motor nativo.

> Se o `winget` não estiver disponível na máquina (comum logo após formatar), os runtimes acima não são instalados e o programa avisa isso claramente — nunca finge sucesso. Instale o "App Installer" pela Microsoft Store e rode a opção de novo.

---

## 🚀 Como Executar no Windows (Qualquer Computador)

1. Dê 2 cliques no executável [`AdaptivePCOptimizer.exe`](AdaptivePCOptimizer.exe).
2. O programa solicitará privilégios de Administrador automaticamente *(UAC)*.
3. Escolha a opção desejada no menu interativo (`1` para otimizar ou `2` pós-formatação).

*(Você também pode executar via linha de comando ou script batch: [`optimize.bat`](optimize.bat)).*

---

## 🔄 Como Desfazer / Restaurar Configurações

O programa cria um snapshot automático do Registro antes de alterar qualquer valor. Para voltar ao estado original:
- Abra o programa e escolha a opção **`[4] RESTAURAR BACKUP`** ou execute [`restore.bat`](restore.bat).

---

## 📄 Licença
Distribuído sob licença **MIT**. Gratuito e livre para uso pessoal e comercial em qualquer computador.
