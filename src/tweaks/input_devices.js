const { execSync } = require('child_process');

function runReg(cmd) {
  try {
    execSync(cmd, { stdio: 'ignore' });
    return true;
  } catch (e) {
    return false;
  }
}

function applyInputDevicesTweaks() {
  const log = [];

  // 1. True 1:1 Raw Mouse Input Curve (Disables Windows pointer acceleration curve while keeping standard DPI/Sensitivity)
  runReg(`reg add "HKCU\\Control Panel\\Mouse" /v "MouseSpeed" /t REG_SZ /d "0" /f`);
  runReg(`reg add "HKCU\\Control Panel\\Mouse" /v "MouseThreshold1" /t REG_SZ /d "0" /f`);
  runReg(`reg add "HKCU\\Control Panel\\Mouse" /v "MouseThreshold2" /t REG_SZ /d "0" /f`);
  runReg(`reg add "HKCU\\Control Panel\\Mouse" /v "MouseSensitivity" /t REG_SZ /d "10" /f`);
  log.push('• Mouse Raw Input 1:1: Aceleração de ponteiro desativada e curva 1:1 ativada para mira precisa e sem atraso.');

  // 2. Keyboard Latency & Repeat Speed Optimization
  // KeyboardDelay: 0 (Instant key repeat on press)
  // KeyboardSpeed: 31 (Max hardware repeat rate for fluid movement and fast coding)
  runReg(`reg add "HKCU\\Control Panel\\Keyboard" /v "KeyboardDelay" /t REG_SZ /d "0" /f`);
  runReg(`reg add "HKCU\\Control Panel\\Keyboard" /v "KeyboardSpeed" /t REG_SZ /d "31" /f`);
  log.push('• Teclado: Delay de repetição reduzido para 0ms e taxa de repetição configurada no máximo (31).');

  // 3. HID & USB Data Queue Buffers (Prevents buffer drops at 1000Hz polling rate)
  runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Services\\mouclass\\Parameters" /v "MouseDataQueueSize" /t REG_DWORD /d 100 /f`);
  runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Services\\kbdclass\\Parameters" /v "KeyboardDataQueueSize" /t REG_DWORD /d 100 /f`);
  log.push('• Fila de Dados HID (USB/PS2): Tamanho de fila ajustado para 100 pacotes (Ideal para mouses 1000Hz/8000Hz).');

  return log;
}

module.exports = { applyInputDevicesTweaks };
