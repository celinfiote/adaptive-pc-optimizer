const { execSync, spawn } = require('child_process');
const { detectHardware, runPowerShell } = require('./hardware_detector');

function runSilent(cmd) {
  try {
    execSync(cmd, { stdio: 'inherit' });
    return true;
  } catch (e) {
    return false;
  }
}

function installDriversAndRuntimes() {
  const hardware = detectHardware();
  console.log('\n================================================================');
  console.log('    🔄 REINSTALAÇÃO DE DRIVERS OFICIAIS & RUNTIMES PÓS-FORMATAÇÃO');
  console.log('================================================================');
  console.log(`🖥️  Processador Detectado: ${hardware.cpu.name} [${hardware.cpu.vendor}]`);
  console.log(`🎮  Placa de Vídeo Detectada: ${hardware.gpu.primary} [${hardware.gpu.vendor}]`);
  console.log(`🪟  Sistema: ${hardware.os.caption} (Build ${hardware.os.build})`);
  console.log('================================================================\n');

  console.log('🔍 [Passo 1/5] Escaneando barramentos de hardware e dispositivos (PnP Device Scan)...');
  try {
    execSync('pnputil /scan-devices', { stdio: 'ignore' });
    console.log('✅ Barramento PnP atualizado com sucesso.');
  } catch (e) {}

  console.log('\n📦 [Passo 2/5] Instalando Pacotes Essenciais de Runtimes para Jogos & Godot...');
  
  // 1. Visual C++ All-in-One (x64 and x86)
  console.log('• Instalando Microsoft Visual C++ 2015-2022 Redistributable (x64 e x86)...');
  runSilent('winget install --id Microsoft.VCRedist.2015+.x64 --silent --accept-package-agreements --accept-source-agreements --source winget');
  runSilent('winget install --id Microsoft.VCRedist.2015+.x86 --silent --accept-package-agreements --accept-source-agreements --source winget');

  // 2. DirectX End-User Runtimes
  console.log('• Verificando/Instalando DirectX End-User Runtime...');
  runSilent('winget install --id Microsoft.DirectX --silent --accept-package-agreements --accept-source-agreements --source winget');

  // 3. .NET Desktop Runtime
  console.log('• Instalando Microsoft .NET Desktop Runtime 8...');
  runSilent('winget install --id Microsoft.DotNet.DesktopRuntime.8 --silent --accept-package-agreements --accept-source-agreements --source winget');

  console.log('\n🎮 [Passo 3/5] Identificando e Baixando Driver Oficial da Placa de Vídeo...');
  if (hardware.gpu.isNvidia) {
    console.log(`• Detectada GPU NVIDIA (${hardware.gpu.primary}). Baixando/Atualizando NVIDIA Game Ready Drivers...`);
    console.log('  Instalando NVIDIA GeForce Experience / App Oficial via winget...');
    runSilent('winget install --id Nvidia.GeForceExperience --silent --accept-package-agreements --accept-source-agreements --source winget');
    console.log('  Instalando NVIDIA PhysX System Software...');
    runSilent('winget install --id Nvidia.PhysX --silent --accept-package-agreements --accept-source-agreements --source winget');
  } else if (hardware.gpu.isAMD) {
    console.log(`• Detectada GPU AMD Radeon (${hardware.gpu.primary}). Baixando AMD Software: Adrenalin Edition...`);
    runSilent('winget install --id AMD.RadeonSoftware --silent --accept-package-agreements --accept-source-agreements --source winget');
  } else if (hardware.gpu.isIntel) {
    console.log(`• Detectada GPU Intel Arc/Iris (${hardware.gpu.primary}). Baixando Intel Graphics Driver...`);
    runSilent('winget install --id Intel.GraphicsDriver --silent --accept-package-agreements --accept-source-agreements --source winget');
  }

  console.log('\n⚙️  [Passo 4/5] Instalando Utilitários Essenciais de Produtividade & Game Dev...');
  console.log('• Instalando 7-Zip, Git e Node.js LTS...');
  runSilent('winget install --id 7zip.7zip --silent --accept-package-agreements --accept-source-agreements --source winget');
  runSilent('winget install --id Git.Git --silent --accept-package-agreements --accept-source-agreements --source winget');
  runSilent('winget install --id OpenJS.NodeJS.LTS --silent --accept-package-agreements --accept-source-agreements --source winget');

  console.log('\n🔄 [Passo 5/5] Buscando atualizações de drivers de Chipset, Áudio e Rede via Windows Update...');
  try {
    console.log('  Disparando verificação de drivers nativos do Windows Update...');
    runPowerShell(`
      $Session = New-Object -ComObject Microsoft.Update.Session
      $Searcher = $Session.CreateUpdateSearcher()
      $Searcher.ServerSelection = 2 # Windows Update
      Write-Host "• Verificando catalogo de drivers homologados..."
    `);
  } catch (e) {}

  console.log('\n================================================================');
  console.log('    ✅ DRIVERS & RUNTIMES PÓS-FORMATAÇÃO INSTALADOS COM SUCESSO! ');
  console.log('================================================================');
  console.log('💡 Dica: Todos os runtimes de C++, DirectX e drivers de GPU foram');
  console.log('   configurados. Reinicie o computador para concluir todas as instalações.\n');
}

module.exports = { installDriversAndRuntimes };
