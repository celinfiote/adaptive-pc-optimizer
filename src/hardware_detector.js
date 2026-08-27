const { execSync } = require('child_process');

function runPowerShell(cmd) {
  try {
    const stdout = execSync(`powershell -NoProfile -NonInteractive -Command "${cmd}"`, {
      encoding: 'utf8',
      stdio: ['pipe', 'pipe', 'ignore'],
      timeout: 10000
    });
    return stdout.trim();
  } catch (e) {
    return '';
  }
}

function detectHardware() {
  const profile = {
    cpu: {
      name: 'Unknown CPU',
      vendor: 'Generic',
      cores: 4,
      threads: 4,
      isAMD: false,
      isIntel: false
    },
    gpu: {
      primary: 'Unknown GPU',
      vendor: 'Generic',
      isNvidia: false,
      isAMD: false,
      isIntel: false,
      supportsHAGS: false
    },
    ram: {
      totalGB: 16,
      freeGB: 8,
      category: '16GB' // '<8GB', '8-16GB', '16-32GB', '32GB+'
    },
    storage: {
      isNVMe: false,
      isSSD: true,
      hasHDD: false
    },
    os: {
      caption: 'Windows',
      version: '10.0',
      isWin11: false,
      build: 19045
    },
    isLaptop: false
  };

  try {
    // 1. CPU Detection
    const cpuJson = runPowerShell(`Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, Manufacturer | ConvertTo-Json -Compress`);
    if (cpuJson) {
      const parsed = JSON.parse(cpuJson);
      profile.cpu.name = parsed.Name || 'Unknown CPU';
      profile.cpu.cores = parsed.NumberOfCores || 4;
      profile.cpu.threads = parsed.NumberOfLogicalProcessors || 4;
      profile.cpu.isAMD = /AMD|Ryzen/i.test(profile.cpu.name);
      profile.cpu.isIntel = /Intel|Core/i.test(profile.cpu.name);
      profile.cpu.vendor = profile.cpu.isAMD ? 'AMD' : (profile.cpu.isIntel ? 'Intel' : 'Generic');
    }

    // 2. GPU Detection
    const gpuJson = runPowerShell(`Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion | ConvertTo-Json -Compress`);
    if (gpuJson) {
      const parsed = JSON.parse(gpuJson);
      const gpus = Array.isArray(parsed) ? parsed : [parsed];
      // Filter out basic display / APU if dedicated GPU exists
      const dedicated = gpus.find(g => /NVIDIA|GeForce|RTX|GTX|Radeon RX/i.test(g.Name)) || gpus[0];
      if (dedicated) {
        profile.gpu.primary = dedicated.Name || 'Unknown GPU';
        profile.gpu.isNvidia = /NVIDIA|GeForce|RTX|GTX/i.test(profile.gpu.primary);
        profile.gpu.isAMD = /Radeon|AMD/i.test(profile.gpu.primary);
        profile.gpu.isIntel = /Intel|Arc|Iris/i.test(profile.gpu.primary);
        profile.gpu.vendor = profile.gpu.isNvidia ? 'NVIDIA' : (profile.gpu.isAMD ? 'AMD' : (profile.gpu.isIntel ? 'Intel' : 'Generic'));
        profile.gpu.supportsHAGS = profile.gpu.isNvidia || (/RX 5600|RX 5700|RX 6000|RX 7000/i.test(profile.gpu.primary));
      }
    }

    // 3. RAM Detection
    const osJson = runPowerShell(`Get-CimInstance Win32_OperatingSystem | Select-Object Caption, Version, BuildNumber, TotalVisibleMemorySize, FreePhysicalMemory | ConvertTo-Json -Compress`);
    if (osJson) {
      const parsed = JSON.parse(osJson);
      profile.os.caption = parsed.Caption || 'Microsoft Windows';
      profile.os.version = parsed.Version || '10.0';
      profile.os.build = parseInt(parsed.BuildNumber, 10) || 19045;
      profile.os.isWin11 = profile.os.build >= 22000;

      const totalMB = (parsed.TotalVisibleMemorySize || 16777216) / 1024;
      const freeMB = (parsed.FreePhysicalMemory || 8388608) / 1024;
      profile.ram.totalGB = Math.round((totalMB / 1024) * 10) / 10;
      profile.ram.freeGB = Math.round((freeMB / 1024) * 10) / 10;

      if (profile.ram.totalGB < 8) profile.ram.category = '<8GB';
      else if (profile.ram.totalGB <= 16) profile.ram.category = '8-16GB';
      else if (profile.ram.totalGB <= 32) profile.ram.category = '16-32GB';
      else profile.ram.category = '32GB+';
    }

    // 4. Storage Detection
    const diskJson = runPowerShell(`Get-PhysicalDisk | Select-Object MediaType, BusType | ConvertTo-Json -Compress`);
    if (diskJson) {
      const parsed = JSON.parse(diskJson);
      const disks = Array.isArray(parsed) ? parsed : [parsed];
      profile.storage.isNVMe = disks.some(d => /NVMe/i.test(d.BusType || ''));
      profile.storage.isSSD = disks.some(d => /SSD/i.test(d.MediaType || '')) || profile.storage.isNVMe;
      profile.storage.hasHDD = disks.some(d => /HDD/i.test(d.MediaType || ''));
    }

    // 5. Battery / Laptop check
    const battery = runPowerShell(`Get-CimInstance Win32_Battery | Select-Object Status`);
    profile.isLaptop = battery.length > 0;

  } catch (e) {
    // Fallback safe defaults
  }

  return profile;
}

module.exports = { detectHardware, runPowerShell };
