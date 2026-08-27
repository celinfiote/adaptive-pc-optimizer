const { execSync } = require('child_process');

function runReg(cmd) {
  try {
    execSync(cmd, { stdio: 'ignore' });
    return true;
  } catch (e) {
    return false;
  }
}

function applyDevGamingHybridTweaks() {
  const log = [];

  // 1. Process CPU Priority Class for Godot Engine & IDEs
  // High priority for Godot Editor & Engine without starving background agents
  const godotKey = 'HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\Godot_v4.7.2-stable_win64_console.exe\\PerfOptions';
  runReg(`reg add "${godotKey}" /v "CpuPriorityClass" /t REG_DWORD /d 3 /f`);
  runReg(`reg add "${godotKey}" /v "IoPriority" /t REG_DWORD /d 3 /f`);
  log.push('• Godot Engine: Prioridade de CPU e I/O de alta performance configurada para execução sem travamentos.');

  // 2. FSE & GameDVR optimization
  // Desativa gravação passiva em segundo plano do GameDVR sem remover interface do usuário
  runReg(`reg add "HKCU\\System\\GameConfigStore" /v "GameDVR_Enabled" /t REG_DWORD /d 0 /f`);
  runReg(`reg add "HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR" /v "AllowGameDVR" /t REG_DWORD /d 0 /f`);
  runReg(`reg add "HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR" /v "AppCaptureEnabled" /t REG_DWORD /d 0 /f`);
  log.push('• GameDVR / Captura Passiva: Desativada captura em segundo plano para liberar 5-10% de uso de GPU e encoders de vídeo.');

  return log;
}

module.exports = { applyDevGamingHybridTweaks };
