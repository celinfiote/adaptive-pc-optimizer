const { execSync } = require('child_process');
const { detectHardware, runPowerShell } = require('./hardware_detector');

function runSilent(cmd, timeoutMs = 120000) {
  try {
    execSync(cmd, { stdio: 'ignore', timeout: timeoutMs });
    return true;
  } catch (e) {
    return false;
  }
}

// CORREÇÃO (bug real encontrado num relato de usuário — "winget" não reconhecido numa
// máquina real): esta função nunca verificava se o winget existia antes de usá-lo pra TUDO
// (VC++, DirectX, .NET, drivers de GPU, 7-Zip, Git, Node). Numa máquina sem winget (comum,
// principalmente logo após formatar — o "App Installer" da Microsoft Store pode não estar
// presente), toda instalação falhava silenciosamente e mesmo assim a função imprimia
// "✅ ... INSTALADOS COM SUCESSO!" no final, incondicionalmente.
function isWingetAvailable() {
  try {
    execSync('winget --version', { stdio: 'ignore', timeout: 5000 });
    return true;
  } catch (e) {
    return false;
  }
}

// Driver de GPU NÃO é baixado às cegas por winget aqui: os pacotes de driver de fabricante
// (Nvidia.GeForceExperience, AMD.RadeonSoftware, etc.) existem no catálogo, mas cada um exige
// aceitar termos próprios e frequentemente instala um app completo (GeForce Experience,
// Adrenalin) só pra conseguir o driver — pesado e não é o que o usuário provavelmente quer só
// pra instalar um driver. Abre a página oficial certa em vez disso, mesma abordagem já usada
// na versão nativa (C#) deste optimizer.
const GPU_VENDOR_PAGES = {
  NVIDIA: 'https://www.nvidia.com/Download/index.aspx',
  AMD: 'https://www.amd.com/en/support',
  Intel: 'https://www.intel.com/content/www/us/en/support/detect.html'
};

function openUrl(url) {
  // execSync já usa cmd.exe como shell por padrão no Windows, então `start` (builtin do
  // cmd) resolve direto sem precisar prefixar "cmd /c" explicitamente.
  return runSilent(`start "" "${url}"`, 5000);
}

// CORREÇÃO (pedido explícito do usuário): antes disto, cada runtime ia direto pro
// `winget install` sem checar se já existia instalado — funcionava na prática porque o
// winget tem sua própria idempotência interna (não reinstala por cima do que já está
// atualizado), mas isso acontecia "escondido" dentro do winget, sem o optimizer nunca
// confirmar/relatar isso pro usuário nem decidir por conta própria não repetir a instalação.
// Agora consulta explicitamente ANTES de instalar, com `winget list --id <id> --exact`: se
// já está instalado, pula o install em vez de disparar o instalador de novo por cima — zero
// chance de conflito. Detecção por CÓDIGO DE SAÍDA (0 = achou, 20 = não achou), não por
// parsing da tabela em texto — a largura das colunas do winget varia por pacote (o nome de
// exibição do VCRedist, por exemplo, já vem colado no Id sem 2 espaços de separação em
// algumas versões), então tentar recortar colunas por regex quebra de forma imprevisível.
function isPackageInstalled(pkgId) {
  try {
    execSync(`winget list --id ${pkgId} --exact`, { stdio: 'ignore', timeout: 15000 });
    return true;
  } catch (e) {
    return false; // exit code != 0 = "No installed package found" = não está instalado
  }
}

function installDriversAndRuntimes() {
  const hardware = detectHardware();
  console.log('\n================================================================');
  console.log('    🔄 CENTRAL PÓS-FORMATAÇÃO — DRIVERS E RUNTIMES OFICIAIS');
  console.log('================================================================');
  console.log(`🖥️  Processador Detectado: ${hardware.cpu.name} [${hardware.cpu.vendor}]`);
  console.log(`🎮  Placa de Vídeo Detectada: ${hardware.gpu.primary} [${hardware.gpu.vendor}]`);
  console.log(`🪟  Sistema: ${hardware.os.caption} (Build ${hardware.os.build})`);
  console.log('================================================================\n');

  console.log('🔍 [Passo 1/4] Escaneando barramentos de hardware e dispositivos (PnP Device Scan)...');
  try {
    execSync('pnputil /scan-devices', { stdio: 'ignore' });
    console.log('✅ Barramento PnP atualizado com sucesso.');
  } catch (e) {
    console.log('⚠️ Não foi possível escanear o barramento PnP.');
  }

  console.log('\n🎮 [Passo 2/4] Driver de GPU:', `${hardware.gpu.primary} [${hardware.gpu.vendor}]`);
  const vendorPage = GPU_VENDOR_PAGES[hardware.gpu.vendor];
  if (vendorPage) {
    console.log(`  🌐 Abrindo a página oficial de drivers ${hardware.gpu.vendor} no navegador padrão...`);
    if (!openUrl(vendorPage)) {
      console.log(`  ⚠️ Não consegui abrir o navegador automaticamente. Acesse manualmente: ${vendorPage}`);
    }
  } else {
    console.log('  ℹ️ Fabricante de GPU não identificado com certeza — acesse o site oficial do fabricante da sua placa de vídeo.');
  }

  console.log('\n🔌 [Passo 3/4] Chipset, áudio, rede e demais periféricos:');
  console.log('  🌐 Abrindo Atualizações Opcionais do Windows (drivers via Windows Update)...');
  if (!openUrl('ms-settings:windowsupdate-optionalupdates')) {
    console.log('  ⚠️ Abra manualmente em Configurações > Windows Update > Atualizações opcionais.');
  }

  console.log('\n📦 [Passo 4/4] Runtimes essenciais (Microsoft, via winget):');
  let installedCount = 0;
  let totalPackages = 0;

  if (!isWingetAvailable()) {
    console.log('  ⚠️ \'winget\' não encontrado nesta máquina — runtimes NÃO foram instalados.');
    console.log('     Instale o \'App Installer\' pela Microsoft Store (gratuito e oficial) e rode esta opção de novo,');
    console.log('     ou baixe manualmente: Visual C++ Redistributable (https://aka.ms/vs/17/release/vc_redist.x64.exe)');
    console.log('     e DirectX (https://www.microsoft.com/en-us/download/details.aspx?id=35).');
  } else {
    const packages = [
      { name: 'Microsoft Visual C++ 2015-2022 Redistributable (x64)', id: 'Microsoft.VCRedist.2015+.x64' },
      { name: 'Microsoft Visual C++ 2015-2022 Redistributable (x86)', id: 'Microsoft.VCRedist.2015+.x86' },
      { name: 'DirectX End-User Runtime', id: 'Microsoft.DirectX' },
      { name: 'Microsoft .NET Desktop Runtime 8', id: 'Microsoft.DotNet.DesktopRuntime.8' },
      // Node.js LTS de propósito: instalar ele aqui destrava a implementação completa
      // (index.js, com detecção de storage/laptop que a versão nativa em C# não tem) pras
      // próximas vezes que o optimizer rodar, em vez de ficar preso no motor nativo pra sempre.
      { name: 'Node.js LTS', id: 'OpenJS.NodeJS.LTS' }
    ];
    totalPackages = packages.length;
    for (const pkg of packages) {
      if (isPackageInstalled(pkg.id)) {
        console.log(`  ✔️  ${pkg.name}: já instalado — pulando (sem reinstalar por cima, evita qualquer conflito).`);
        installedCount++;
        continue;
      }
      console.log(`  ⬇️  Instalando ${pkg.name}...`);
      const ok = runSilent(`winget install --id ${pkg.id} --silent --accept-package-agreements --accept-source-agreements --source winget`);
      console.log(`     ${ok ? '✅ Instalado com sucesso.' : '⚠️ Falhou — verifique a conexão com a internet ou se o pacote existe no catálogo winget.'}`);
      if (ok) installedCount++;
    }
  }

  console.log('\n================================================================');
  if (totalPackages > 0) {
    console.log(`✅ Central pós-formatação concluída: ${installedCount}/${totalPackages} runtimes confirmados.`);
  } else {
    console.log('⚠️ Central pós-formatação concluída SEM instalar runtimes (winget indisponível — ver aviso acima).');
  }
  console.log('   Driver de GPU e drivers de chipset/periféricos foram abertos nas páginas');
  console.log('   oficiais acima — a instalação final desses é manual, por não existir um');
  console.log('   link de download silencioso oficial e estável para eles.');
  console.log('================================================================\n');

  return { installedCount, totalPackages, wingetAvailable: totalPackages > 0, gpuVendor: hardware.gpu.vendor };
}

module.exports = { installDriversAndRuntimes, isWingetAvailable, isPackageInstalled, GPU_VENDOR_PAGES };
