#!/usr/bin/env node

/**
 * ⚡ Adaptive PC Optimizer (Universal Tweak Engine)
 * Otimizador de Desempenho e Baixa Latência Adaptativo para Gamers e Desenvolvedores de Jogos
 * Desenvolvido para máxima performance de hardware sem desativar funções essenciais do sistema.
 */

const readline = require('readline');
const { detectHardware } = require('./src/hardware_detector');
const { createRegistryBackup, restoreLatestBackup } = require('./src/backup_manager');
const { applyCpuSchedulerTweaks } = require('./src/tweaks/cpu_scheduler');
const { applyGpuDisplayTweaks } = require('./src/tweaks/gpu_display');
const { applyInputDevicesTweaks } = require('./src/tweaks/input_devices');
const { applyStorageMemoryTweaks } = require('./src/tweaks/storage_memory');
const { applyNetworkLatencyTweaks } = require('./src/tweaks/network_latency');
const { applyDevGamingHybridTweaks } = require('./src/tweaks/dev_gaming_hybrid');
const { runBenchmark } = require('./src/benchmark');

function printBanner(hardware) {
  console.log('\n================================================================');
  console.log('       ⚡ ADAPTIVE PC OPTIMIZER — UNIVERSAL TWEAK ENGINE        ');
  console.log('       Performance, Low Latency & Multi-Agent Workload Tuning   ');
  console.log('================================================================');
  console.log(`🖥️  Processador: ${hardware.cpu.name} (${hardware.cpu.cores}C/${hardware.cpu.threads}T)`);
  console.log(`🎮  Placa de Vídeo: ${hardware.gpu.primary} [${hardware.gpu.vendor}]`);
  console.log(`🧠  Memória RAM: ${hardware.ram.totalGB} GB (Livre: ${hardware.ram.freeGB} GB) [${hardware.ram.category}]`);
  console.log(`💾  Armazenamento: ${hardware.storage.isNVMe ? 'NVMe SSD' : (hardware.storage.isSSD ? 'SATA SSD' : 'HDD')}`);
  console.log(`🪟  Sistema: ${hardware.os.caption} (Build ${hardware.os.build})`);
  console.log('================================================================\n');
}

function applyAllTweaks(hardware) {
  console.log('🚀 Iniciando Otimização Adaptativa Segura...\n');

  // 1. Criar backup do registro antes de qualquer alteração
  createRegistryBackup();
  console.log('');

  const allLogs = [];

  // 2. CPU & Thread Scheduling
  console.log('⚙️  [1/6] Otimizando Escalonamento de CPU e Prioridade de Threads...');
  allLogs.push(...applyCpuSchedulerTweaks(hardware));

  // 3. GPU & Display Driver Communication
  console.log('🎮 [2/6] Otimizando Comunicação GPU, DirectX e HAGS...');
  allLogs.push(...applyGpuDisplayTweaks(hardware));

  // 4. Input Devices (Mouse 1:1, Keyboard 0ms, HID Queues)
  console.log('🖱️  [3/6] Otimizando Periféricos (Mouse Raw 1:1, Teclado 0ms, Fila HID)...');
  allLogs.push(...applyInputDevicesTweaks());

  // 5. Storage & RAM Throughput
  console.log('💾 [4/6] Otimizando Cache NTFS e Alocação de Memória Física...');
  allLogs.push(...applyStorageMemoryTweaks(hardware));

  // 6. Network Latency & TCP No Delay
  console.log('🌐 [5/6] Otimizando Pilha de Rede TCP/IP (Baixo Ping & Zero Nagle)...');
  allLogs.push(...applyNetworkLatencyTweaks());

  // 7. Game Dev & Multi-Agent Balance (Godot, IDE, GameDVR)
  console.log('🛠️  [6/6] Otimizando Ambiente Híbrido de Jogo + Desenvolvimento + Multi-Agentes...');
  allLogs.push(...applyDevGamingHybridTweaks());

  console.log('\n================================================================');
  console.log('             ✅ OTIMIZAÇÃO CONCLUÍDA COM SUCESSO!              ');
  console.log('================================================================');
  for (const item of allLogs) {
    console.log(item);
  }
  console.log('================================================================');
  console.log('💡 Dica: Para obter 100% do ganho de latência de drivers e kernel,');
  console.log('   reinicie seu computador assim que for conveniente.\n');
}

function showStatus() {
  const hardware = detectHardware();
  printBanner(hardware);
  console.log('📊 Métricas e Status de Baixa Latência:');
  const bench = runBenchmark();
  for (const [k, v] of Object.entries(bench)) {
    console.log(`• ${k}: ${v}`);
  }
  console.log('');
}

function main() {
  const args = process.argv.slice(2);
  const hardware = detectHardware();

  if (args.includes('--apply') || args.includes('-a') || args.includes('optimize')) {
    printBanner(hardware);
    applyAllTweaks(hardware);
    process.exit(0);
  }

  if (args.includes('--restore') || args.includes('-r') || args.includes('restore')) {
    printBanner(hardware);
    restoreLatestBackup();
    process.exit(0);
  }

  if (args.includes('--status') || args.includes('-s') || args.includes('status')) {
    showStatus();
    process.exit(0);
  }

  // Interactive CLI Menu
  printBanner(hardware);
  console.log('Selecione uma opção:');
  console.log('  1. ⚡ Aplicar Otimização Adaptativa Completa (Recomendado)');
  console.log('  2. 📊 Verificar Status e Métricas de Latência');
  console.log('  3. 🔄 Restaurar Backup Original do Registro');
  console.log('  4. ❌ Sair\n');

  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
  });

  rl.question('Digite a opção desejada [1-4]: ', (answer) => {
    rl.close();
    const opt = answer.trim();
    if (opt === '1') {
      applyAllTweaks(hardware);
    } else if (opt === '2') {
      showStatus();
    } else if (opt === '3') {
      restoreLatestBackup();
    } else {
      console.log('Operação cancelada.');
    }
  });
}

if (require.main === module) {
  main();
}

module.exports = { detectHardware, applyAllTweaks, restoreLatestBackup };
