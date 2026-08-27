const { execSync } = require('child_process');

function runReg(cmd) {
  try {
    execSync(cmd, { stdio: 'ignore' });
    return true;
  } catch (e) {
    return false;
  }
}

function applyStorageMemoryTweaks(hardware) {
  const log = [];

  // 1. NTFS Memory Usage (Enhances file indexing & disk cache for Godot/Git/Builds)
  // 2 = Increased memory limit for NTFS table lookups
  runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem" /v "NtfsMemoryUsage" /t REG_DWORD /d 2 /f`);
  log.push('• NTFS Memory Usage: Configurado para nível 2 (Acelera leitura de milhares de arquivos de código e assets).');

  // 2. Disable Last Access Update (Prevents unnecessary disk writes on file reads)
  runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem" /v "NtfsDisableLastAccessUpdate" /t REG_DWORD /d 1 /f`);
  log.push('• NTFS Last Access: Desativada atualização de carimbo de data ao ler arquivos (Reduz I/O desnecessário).');

  // 3. Disable Paging Executive (Keeps kernel drivers in physical RAM if >=16GB)
  if (hardware.ram.totalGB >= 16) {
    runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management" /v "DisablePagingExecutive" /t REG_DWORD /d 1 /f`);
    log.push('• Paging Executive: Drivers do sistema retidos na RAM física (Sem paginação lenta em disco para chamadas de sistema).');
  }

  // 4. Large System Cache (0 = Prioritize Application/Game RAM over generic OS cache)
  runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management" /v "LargeSystemCache" /t REG_DWORD /d 0 /f`);
  log.push('• Gerenciamento de Memória: Cache do sistema ajustado para priorizar memória de aplicações ativas e jogos.');

  return log;
}

module.exports = { applyStorageMemoryTweaks };
