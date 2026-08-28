const { execSync } = require('child_process');

// CORREÇÃO: esta função retornava valores 100% hardcoded ("Enabled", "Active", etc.)
// independente de qualquer tweak real ter sido aplicado — a opção [3] "VERIFICAR HARDWARE
// & STATUS DE LATÊNCIA" sempre dizia que tudo estava otimizado, mesmo numa instalação nova
// do Windows onde nada foi configurado ainda. Agora lê o valor REAL do Registro pra cada
// item (mesma lógica que a versão nativa em C# usa, pra manter as duas consistentes).
function readRegValue(keyPath, valueName) {
  try {
    const output = execSync(`reg query "${keyPath}" /v "${valueName}"`, { encoding: 'utf8', stdio: ['pipe', 'pipe', 'ignore'] });
    const match = output.match(new RegExp(`${valueName}\\s+REG_[A-Z_]+\\s+(\\S+)`));
    return match ? match[1] : null;
  } catch (e) {
    return null;
  }
}

function findTcpNoDelay() {
  try {
    const interfacesKey = 'HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces';
    const output = execSync(`reg query "${interfacesKey}"`, { encoding: 'utf8' });
    const subkeys = output.split('\r\n').filter(line => line.trim().startsWith('HKEY_LOCAL_MACHINE'));
    for (const key of subkeys) {
      const shortKey = key.trim().replace('HKEY_LOCAL_MACHINE', 'HKLM');
      const value = readRegValue(shortKey, 'TCPNoDelay');
      if (value !== null) return value;
    }
  } catch (e) {}
  return null;
}

function runBenchmark() {
  const priSep = readRegValue('HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl', 'Win32PrioritySeparation');
  const tcpNoDelay = findTcpNoDelay();
  const mouseSpeed = readRegValue('HKCU\\Control Panel\\Mouse', 'MouseSpeed');
  const hags = readRegValue('HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers', 'HwSchMode');
  const ntfsCache = readRegValue('HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem', 'NtfsMemoryUsage');

  return {
    cpuScheduler: priSep ? `Win32PrioritySeparation = ${priSep}` : 'não configurado (padrão do Windows)',
    networkStatus: tcpNoDelay === '0x1' ? 'TCPNoDelay Ativo (0ms Nagle Delay)' : 'não configurado (padrão do Windows)',
    inputCurve: mouseSpeed === '0' ? 'Raw 1:1 Hardware Response (Aceleração Off)' : 'padrão do Windows (aceleração ativa)',
    gpuHags: hags === '0x2' ? 'Ativado' : 'não ativado (rode a opção [1] pra aplicar)',
    storageCache: ntfsCache === '0x2' ? 'NTFS Nível 2 (alto throughput)' : 'padrão do Windows'
  };
}

module.exports = { runBenchmark };
