const { execSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const os = require('os');

const BACKUP_DIR = path.join(__dirname, '..', 'backups');

function ensureBackupDir() {
  if (!fs.existsSync(BACKUP_DIR)) {
    fs.mkdirSync(BACKUP_DIR, { recursive: true });
  }
}

function createRegistryBackup() {
  ensureBackupDir();
  const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
  const backupFile = path.join(BACKUP_DIR, `registry_backup_${timestamp}.reg`);
  
  const keysToBackup = [
    'HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl',
    'HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile',
    'HKCU\\Control Panel\\Mouse',
    'HKCU\\Control Panel\\Keyboard',
    'HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem',
    'HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters'
  ];

  console.log(`🛡️ Criando snapshot de segurança do Registro em: ${backupFile}`);
  
  let successCount = 0;
  for (const key of keysToBackup) {
    try {
      execSync(`reg export "${key}" "${backupFile}_${successCount}.reg" /y`, { stdio: 'ignore' });
      successCount++;
    } catch (e) {}
  }

  // Save metadata
  const metaFile = path.join(BACKUP_DIR, 'latest_backup.json');
  fs.writeFileSync(metaFile, JSON.stringify({
    machine: os.hostname(),
    timestamp,
    backupFile,
    successCount
  }, null, 2), 'utf8');

  return backupFile;
}

function restoreLatestBackup() {
  const metaFile = path.join(BACKUP_DIR, 'latest_backup.json');
  if (!fs.existsSync(metaFile)) {
    console.log('⚠️ Nenhum backup prévio encontrado.');
    return false;
  }

  const meta = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
  // Recusa backup sem origem ou de outra máquina: o export de Tcpip\Parameters inclui o
  // Hostname, e importar o backup de outro PC renomeava esta máquina.
  if (!meta.machine || meta.machine.toLowerCase() !== os.hostname().toLowerCase()) {
    console.log('❌ Backup criado em outro computador (' + (meta.machine || 'origem desconhecida') + ') — nada foi restaurado, por segurança.');
    return false;
  }
  console.log(`🔄 Restaurando snapshot de segurança de ${meta.timestamp}...`);

  const total = meta.successCount || 6;
  let restored = 0;
  for (let i = 0; i < total; i++) {
    const regFile = `${meta.backupFile}_${i}.reg`;
    if (fs.existsSync(regFile)) {
      try {
        execSync(`reg import "${regFile}"`, { stdio: 'ignore' });
        restored++;
      } catch (e) {}
    }
  }

  // Honesto: reg import só sobrescreve valores existentes no backup, não apaga os
  // que a otimização adicionou. Reversão completa = motor nativo (.exe), que usa
  // Ponto de Restauração do Windows + diário por valor.
  console.log(`${restored === total ? '✅' : '⚠️'} ${restored}/${total} chaves reimportadas (restauração parcial — valores novos não são apagados).`);
  return restored === total;
}

module.exports = { createRegistryBackup, restoreLatestBackup, BACKUP_DIR };
