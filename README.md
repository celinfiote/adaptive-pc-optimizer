# ⚡ Adaptive PC Optimizer (Universal Tweak & Driver Engine)

> **Software Universal e Adaptativo para Windows 10 & 11 (.EXE Standalone)**  
> Otimizador de Desempenho, Baixa Latência e **Central Pós-Formatação de Drivers Oficiais**.  
> Criado para **Gamers Competitivos**, **Desenvolvedores de Jogos (Godot/Unity/Unreal)** e **Workloads Multi-Agentes de IA (Orca/Antigravity)**.

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
1. **Varredura PnP de Dispositivos**: Identifica barramentos e componentes sem driver.
2. **Microsoft Visual C++ Redistributable All-in-One**: Instala silenciosamente todas as bibliotecas C++ de 2015 a 2022 (x86 e x64).
3. **DirectX End-User Runtime**: Atualiza todas as bibliotecas legadas e modernas do DirectX 9, 10, 11 e 12.
4. **Microsoft .NET Desktop Runtime 8**: Runtimes necessários para ferramentas de desenvolvimento e jogos modernos.
5. **Driver Oficial de GPU**: Detecta se a placa é **NVIDIA**, **AMD** ou **Intel Arc** e baixa o software oficial do fabricante (*GeForce Experience / PhysX / AMD Adrenalin / Intel Graphics*).
6. **Utilitários Essenciais de Dev**: Instala 7-Zip, Git e Node.js LTS via instaladores oficiais homologados.

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
