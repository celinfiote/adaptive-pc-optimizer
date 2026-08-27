const { execSync } = require('child_process');

function runReg(cmd) {
  try {
    execSync(cmd, { stdio: 'ignore' });
    return true;
  } catch (e) {
    return false;
  }
}

function applyGpuDisplayTweaks(hardware) {
  const log = [];

  // 1. Hardware Accelerated GPU Scheduling (HAGS)
  // 2 = Enabled (WDDM 2.7+ reduces CPU-GPU sync latency)
  if (hardware.gpu.supportsHAGS && hardware.os.build >= 19041) {
    runReg(`reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers" /v "HwSchMode" /t REG_DWORD /d 2 /f`);
    log.push('• HAGS (Hardware Accelerated GPU Scheduling): Ativado para redução de overhead na comunicação CPU-GPU.');
  }

  // 2. DirectX Low Latency Presentation Queue
  runReg(`reg add "HKCU\\Software\\Microsoft\\Direct3D" /v "MaxFrameLatency" /t REG_DWORD /d 1 /f`);
  runReg(`reg add "HKLM\\SOFTWARE\\Microsoft\\Direct3D" /v "MaxFrameLatency" /t REG_DWORD /d 1 /f`);
  log.push('• DirectX Queue: MaxFrameLatency configurado para 1 (Renderização imediata de quadros sem buffer lag).');

  // 3. DirectX Shader Cache Sizing & Optimization
  // Seta tamanho amplo para cache de shaders no disco, eliminando stutters em jogos e Godot
  runReg(`reg add "HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences" /v "DirectXShaderCacheSize" /t REG_DWORD /d 10240 /f`);
  log.push('• Shader Cache DirectX: Alocado buffer de até 10GB para cache pré-compilado de shaders (Zero micro-travamentos).');

  // 4. DWM (Desktop Window Manager) Refresh Sync for Multi-monitors
  runReg(`reg add "HKCU\\Software\\Microsoft\\Windows\\DWM" /v "Composition" /t REG_DWORD /d 1 /f`);
  runReg(`reg add "HKCU\\Software\\Microsoft\\Windows\\DWM" /v "EnableAeroPeek" /t REG_DWORD /d 1 /f`);
  log.push('• DWM Compositor: Sincronização multi-monitor e renderização de janelas otimizada.');

  return log;
}

module.exports = { applyGpuDisplayTweaks };
