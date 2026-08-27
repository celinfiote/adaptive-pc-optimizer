const { execSync } = require('child_process');

function runCmd(cmd) {
  try {
    execSync(cmd, { stdio: 'ignore' });
    return true;
  } catch (e) {
    return false;
  }
}

function applyNetworkLatencyTweaks() {
  const log = [];

  // 1. Global Netsh TCP Tweaks (Low-latency packet delivery)
  runCmd('netsh int tcp set global autotuninglevel=normal');
  runCmd('netsh int tcp set global rss=enabled');
  runCmd('netsh int tcp set global timestamps=disabled');
  runCmd('netsh int tcp set global ecncapability=disabled');
  log.push('• Pilha TCP/IP Netsh: Ativado Receive Side Scaling (RSS) e Auto-Tuning normal para distribuição em todos os núcleos da CPU.');

  // 2. TCP No Delay & TCP Ack Frequency for all active interfaces
  try {
    const interfacesKey = 'HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces';
    // Get subkeys
    const regOutput = execSync(`reg query "${interfacesKey}"`, { encoding: 'utf8' });
    const subkeys = regOutput.split('\r\n').filter(line => line.trim().startsWith('HKEY_LOCAL_MACHINE'));

    for (const key of subkeys) {
      const shortKey = key.replace('HKEY_LOCAL_MACHINE', 'HKLM');
      runCmd(`reg add "${shortKey}" /v "TcpAckFrequency" /t REG_DWORD /d 1 /f`);
      runCmd(`reg add "${shortKey}" /v "TCPNoDelay" /t REG_DWORD /d 1 /f`);
      runCmd(`reg add "${shortKey}" /v "TcpDelAckTicks" /t REG_DWORD /d 0 /f`);
    }
    log.push('• TCP Low Latency: Algoritmo de Nagle desativado e TcpAckFrequency=1 (Zero atraso de confirmação de pacotes em jogos e streaming de APIs).');
  } catch (e) {}

  return log;
}

module.exports = { applyNetworkLatencyTweaks };
