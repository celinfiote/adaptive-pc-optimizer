# ⚡ Adaptive PC Optimizer (Universal Tweak Engine)

> **Otimizador de Desempenho e Baixa Latência Universal e Adaptativo para Windows 10 & 11**  
> Desenvolvido especificamente para **Gamers Competitivos**, **Desenvolvedores de Jogos (Godot/Unity/Unreal)** e **Workloads Multi-Agentes de IA (Orca/Antigravity)**.

---

## 🎯 Filosofia de Design: *Zero Destruição, Máxima Eficiência*

Ao contrário de scripts de "debloat" genéricos que quebram recursos essenciais do Windows, o **Adaptive PC Optimizer** opera sob diretrizes estritas:
1. **NENHUMA função essencial é desativada**: Bluetooth, Wi-Fi, Xbox Live, impressoras, áudio e atualizações permanecem intactos.
2. **Adaptação Dinâmica ao Hardware**: Detecta automaticamente processador (AMD/Intel), placa de vídeo (NVIDIA/AMD/Intel), quantidade de RAM e tipo de armazenamento (NVMe/SSD/HDD) para aplicar os parâmetros ideais.
3. **Equilíbrio Jogo + Desenvolvimento + IA**: Garante framerates altos e estáveis em jogos enquanto compila código no Godot e executa múltiplos agentes de IA em segundo plano sem engasgos ou travamentos.
4. **Snapshot e Restauração em 1 Clique**: Cria automaticamente um backup completo do Registro antes de aplicar qualquer alteração, permitindo reversão instantânea.

---

## 🛠️ O que o Programa Otimiza?

### 1. ⚙️ Escalonamento de CPU & Threads (CPU Scheduler)
- **`Win32PrioritySeparation = 0x26`**: Ajusta o quantum de CPU para 3:1 em favor do processo em primeiro plano (jogo/editor), entregando frametimes lisos sem congelar threads de background.
- **MMCSS (Multimedia Class Scheduler)**:
  - Remove o estrangulamento de rede durante jogos (`NetworkThrottlingIndex = 0xFFFFFFFF`).
  - Reserva 90% de prioridade para processos em tempo real e 10% para o sistema operacional (`SystemResponsiveness = 10`).
- **CSRSS (Client Server Runtime)**: Priorização do despacho de mensagens de janela para baixa latência de clique e renderização.

### 2. 🎮 Drivers Gráficos, DirectX & Comunicação com GPU
- **HAGS (Hardware Accelerated GPU Scheduling)**: Habilita agendamento acelerado por hardware em GPUs compatíveis para reduzir a latência entre CPU e GPU.
- **DirectX Low Latency Queue**: Configura `MaxFrameLatency = 1` para entrega imediata de quadros sem acúmulo de buffer.
- **Shader Cache de 10GB**: Expande o buffer de shaders pré-compilados no disco, eliminando os micro-travamentos (*stutters*) comuns ao carregar novos efeitos visuais em jogos e na engine Godot.
- **DWM Multi-Monitor Sync**: Sincronização limpa entre múltiplos monitores com taxas de atualização diferentes (ex: 144Hz + 60Hz).

### 3. 🖱️ Periféricos & Comunicação com Hardware (Mouse, Teclado, Monitores)
- **True 1:1 Raw Mouse Input**: Desativa as curvas de aceleração do ponteiro do Windows, garantindo precisão matemática e resposta instantânea do sensor do mouse.
- **Taxa de Repetição do Teclado**: Reduz o atraso de repetição para 0ms e taxa máxima (31) para movimentação ágil e digitação rápida.
- **Buffer de Fila HID (USB/PS2)**: Ajusta o tamanho da fila de pacotes para 100, ideal para mouses de alta taxa de sondagem (1000Hz a 8000Hz).

### 4. 💾 Armazenamento & Memória RAM
- **NTFS Memory Usage (Nível 2)**: Amplia a memória de cache para consultas de tabelas de arquivos NTFS, acelerando drasticamente o carregamento de milhares de assets, scripts e repositórios Git.
- **NTFS Disable Last Access Update**: Desativa a gravação desnecessária de data em cada leitura de arquivo.
- **Disable Paging Executive**: Mantém drivers do sistema e código do kernel na RAM física (para sistemas com 16GB+), eliminando paginação lenta em disco.

### 5. 🌐 Rede & Pilha TCP/IP (Baixo Ping & Streaming de APIs)
- **TCP No Delay & TCP Ack Frequency = 1**: Desativa o Algoritmo de Nagle, enviando pacotes instantaneamente sem aguardar confirmações lentas.
- **Receive Side Scaling (RSS)**: Distribui o processamento de pacotes de rede entre todos os núcleos da CPU.

### 6. 🛠️ Modo Híbrido: Game Dev + Jogos + Multi-Agentes
- **Prioridade de Processo para Godot & IDEs**: Define prioridade de CPU e I/O de alta performance para a engine.
- **Desativação de Captura Passiva do GameDVR**: Libera 5% a 10% de recursos de GPU e encoders de vídeo sem afetar screenshots ou utilitários manuais.

---

## 🚀 Como Usar no Seu Computador

### ⚡ Método Rápido (1 Clique no Windows Explorer):
1. Dê 2 cliques no arquivo [`optimize.bat`](optimize.bat).
2. O programa detectará seu hardware automaticamente, criará um backup e aplicará todas as otimizações.
3. Reinicie o computador para usufruir de 100% dos ganhos de latência de drivers e kernel.

### 💻 Método via Linha de Comando (CLI):
```bash
# Aplicar todas as otimizações adaptativas
node index.js --apply

# Verificar status e métricas de baixa latência
node index.js --status

# Restaurar configurações originais a partir do backup
node index.js --restore
```

---

## 🔄 Como Desfazer / Restaurar o Backup

Caso queira reverter para as configurações padrão a qualquer momento:
1. Dê 2 cliques no arquivo [`restore.bat`](restore.bat) ou execute `node index.js --restore`.
2. O programa restaurará automaticamente o snapshot original salvo na pasta `backups/`.

---

## 📄 Licença
Distribuído sob a licença **MIT**. Livre para uso, modificação e distribuição em qualquer máquina.
