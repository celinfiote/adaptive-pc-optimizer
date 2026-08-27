const { execSync } = require('child_process');

function runReg(cmd) {
  try {
    execSync(cmd, { stdio: 'ignore' });
    return true;
  } catch (e) {
    return false;
  }
}

function applyCpuSchedulerTweaks(hardware) {
  const log = [];

  // 1. Win32PrioritySeparation
  // Value 0x26 (38 dec): Short variable quantum with 3:1 foreground boost.
  // Perfect for responsive 144Hz/240Hz gaming + snappy IDE response without freezing background agent threads.
  const priorityValue = hardware.cpu.threads >= 8 ? '0x26' : '0x28';
  runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl" /v "Win32PrioritySeparation" /t REG_DWORD /d ${priorityValue} /f`);
  log.push(`• Escalonamento de Threads CPU: Win32PrioritySeparation configurado para ${priorityValue} (Resposta ágil em primeiro plano + threads de background balanceadas).`);

  // 2. MMCSS (Multimedia Class Scheduler Service)
  // NetworkThrottlingIndex = 0xFFFFFFFF (Desativa limitação de rede em jogos/mídia)
  // SystemResponsiveness = 10 (Garante 90% de CPU para processos em tempo real enquanto reserva 10% para o sistema/agentes)
  runReg(`reg add "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile" /v "NetworkThrottlingIndex" /t REG_DWORD /d 0xFFFFFFFF /f`);
  runReg(`reg add "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile" /v "SystemResponsiveness" /t REG_DWORD /d 10 /f`);
  log.push('• MMCSS: Throttling de rede desativado e Responsividade do Sistema ajustada para 90% realtime / 10% background.');

  // 3. MMCSS Games Profile
  const gamesPath = 'HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games';
  runReg(`reg add "${gamesPath}" /v "GPU Priority" /t REG_DWORD /d 8 /f`);
  runReg(`reg add "${gamesPath}" /v "Priority" /t REG_DWORD /d 6 /f`);
  runReg(`reg add "${gamesPath}" /v "Scheduling Category" /t REG_SZ /d "High" /f`);
  runReg(`reg add "${gamesPath}" /v "SFIO Priority" /t REG_SZ /d "High" /f`);
  log.push('• Perfil MMCSS Games: Prioridade de GPU e Escalonamento elevadas para High.');

  // 4. CSRSS (Client Server Runtime Subsystem) Priority Optimization
  // Seta prioridade de renderização de janelas sem desestabilizar o kernel
  runReg(`reg add "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\csrss.exe\\PerfOptions" /v "CpuPriorityClass" /t REG_DWORD /d 3 /f`);
  log.push('• Subsistema CSRSS: Otimização de despacho de mensagens de janela para baixa latência.');

  return log;
}

module.exports = { applyCpuSchedulerTweaks };
