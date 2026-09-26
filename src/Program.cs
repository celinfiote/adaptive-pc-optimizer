using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;

namespace AdaptivePCOptimizer
{
    // Perfil mínimo de hardware necessário pras decisões de gating dos tweaks nativos —
    // espelha (só os campos realmente usados) o hardware_detector.js, pra manter as duas
    // implementações (Node e nativa) tomando exatamente as mesmas decisões.
    class HardwareProfile
    {
        public int CpuThreads = 4;
        public string GpuName = "Unknown GPU";
        public string GpuVendor = "Generic"; // "NVIDIA" | "AMD" | "Intel" | "Generic"
        public bool GpuSupportsHAGS = false;
        public double RamTotalGB = 16;
        public int OsBuild = 19045;
    }

    // Estado ORIGINAL de um único valor do Registro, capturado imediatamente antes de o
    // programa alterá-lo — permite desfazer exatamente o que foi mudado (inclusive apagar
    // valores que não existiam antes), coisa que "reg export/import" de chaves inteiras
    // não consegue: import só sobrescreve, nunca apaga o que foi adicionado depois.
    class JournalEntry
    {
        public string Key;
        public string Name;
        public bool Existed;
        public string Type;
        public string Data;
    }

    class Program
    {
        // ============================================================
        // Administração / elevação (inalterado da versão anterior)
        // ============================================================
        static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        static void RelaunchAsAdmin(string[] args)
        {
            ProcessStartInfo proc = new ProcessStartInfo();
            proc.UseShellExecute = true;
            proc.WorkingDirectory = Environment.CurrentDirectory;
            proc.FileName = Process.GetCurrentProcess().MainModule.FileName;
            proc.Arguments = string.Join(" ", args);
            proc.Verb = "runas";

            try
            {
                Process.Start(proc);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Erro ao solicitar privilegios de Administrador: " + ex.Message);
                Console.ResetColor();
                Console.WriteLine("Pressione qualquer tecla para sair...");
                Console.ReadKey();
            }
        }

        // ============================================================
        // Helpers de processo — todos usam ferramentas NATIVAS do Windows
        // (reg.exe, netsh.exe, pnputil.exe, powershell.exe) que sempre existem,
        // ao contrário de node/winget, que podem não estar instalados.
        // ============================================================
        static bool RunSilent(string exe, string args, int timeoutMs = 15000)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(timeoutMs);
                    return p.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        static string RunCapture(string exe, string args, int timeoutMs = 15000)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(timeoutMs);
                    return output;
                }
            }
            catch
            {
                return "";
            }
        }

        static bool RunReg(string args)
        {
            return RunSilent("reg", args);
        }

        static string RunPowerShell(string cmd)
        {
            string escaped = cmd.Replace("\"", "\\\"");
            return RunCapture("powershell", "-NoProfile -NonInteractive -Command \"" + escaped + "\"", 15000).Trim();
        }

        // Scripts multi-linha: -EncodedCommand (UTF-16LE em base64) evita qualquer problema
        // de aspas/quebra de linha ao passar o script como argumento de linha de comando.
        static string RunPowerShellScript(string script, int timeoutMs)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            return RunCapture("powershell", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded, timeoutMs).Trim();
        }

        static bool AskYesNo(string prompt)
        {
            Console.Write(prompt);
            try
            {
                while (true)
                {
                    ConsoleKeyInfo k = Console.ReadKey(true);
                    char c = char.ToUpperInvariant(k.KeyChar);
                    if (c == 'S' || c == 'Y') { Console.WriteLine("S"); return true; }
                    if (c == 'N') { Console.WriteLine("N"); return false; }
                }
            }
            catch
            {
                // Sem console interativo (entrada redirecionada) — escolhe o caminho seguro.
                Console.WriteLine();
                return false;
            }
        }

        static bool IsToolAvailable(string exe)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(exe, "--version");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit(5000);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // Detecção de hardware — só os campos usados nas decisões de gating dos
        // tweaks (ver comentário de cada tweak abaixo pra saber qual campo usa o quê).
        // ============================================================
        static HardwareProfile DetectHardware()
        {
            HardwareProfile hw = new HardwareProfile();

            try
            {
                string threadsStr = RunPowerShell("(Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty NumberOfLogicalProcessors)");
                int threads;
                if (int.TryParse(threadsStr, out threads) && threads > 0) hw.CpuThreads = threads;
            }
            catch { }

            try
            {
                // "Radeon RX" (não "Radeon" sozinho) de propósito — uma GPU integrada AMD
                // se chama "Radeon(TM) Graphics", não "Radeon RX ...". Sem essa distinção,
                // uma máquina com APU AMD + GPU dedicada NVIDIA escolhia a integrada errada
                // (bug real encontrado testando isto de verdade nesta máquina: RTX 3060
                // presente, mas detectava "AMD Radeon(TM) Graphics" como GPU primária).
                string gpuName = RunPowerShell("(Get-CimInstance Win32_VideoController | Where-Object { $_.Name -match 'NVIDIA|GeForce|RTX|GTX|Radeon RX' } | Select-Object -First 1 -ExpandProperty Name)");
                if (string.IsNullOrEmpty(gpuName))
                {
                    gpuName = RunPowerShell("(Get-CimInstance Win32_VideoController | Select-Object -First 1 -ExpandProperty Name)");
                }
                if (!string.IsNullOrEmpty(gpuName))
                {
                    hw.GpuName = gpuName;
                    bool isNvidia = Regex.IsMatch(gpuName, "NVIDIA|GeForce|RTX|GTX", RegexOptions.IgnoreCase);
                    bool isAmd = Regex.IsMatch(gpuName, "Radeon|AMD", RegexOptions.IgnoreCase);
                    bool isIntel = Regex.IsMatch(gpuName, "Intel|Arc|Iris", RegexOptions.IgnoreCase);
                    hw.GpuVendor = isNvidia ? "NVIDIA" : (isAmd ? "AMD" : (isIntel ? "Intel" : "Generic"));
                    hw.GpuSupportsHAGS = isNvidia || Regex.IsMatch(gpuName, "RX 5600|RX 5700|RX 6000|RX 7000", RegexOptions.IgnoreCase);
                }
            }
            catch { }

            try
            {
                string buildStr = RunPowerShell("([System.Environment]::OSVersion.Version.Build)");
                int build;
                if (int.TryParse(buildStr, out build) && build > 0) hw.OsBuild = build;
            }
            catch { }

            try
            {
                // Pega os bytes brutos (um inteiro, sem ambiguidade de formatação) e faz a
                // divisão/arredondamento aqui no C# — bug real encontrado testando isto de
                // verdade: pedir pro PowerShell já formatar o número (Math]::Round(...,1))
                // devolve "27,9" em vez de "27.9" numa máquina com Windows em pt-BR (vírgula
                // como separador decimal), e double.TryParse com InvariantCulture então lia
                // a vírgula como separador de milhar, virando 279 em vez de 27.9.
                string bytesStr = RunPowerShell("(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory");
                long bytes;
                if (long.TryParse(bytesStr, out bytes) && bytes > 0)
                {
                    hw.RamTotalGB = Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 1);
                }
            }
            catch { }

            return hw;
        }

        // ============================================================
        // Segurança em duas camadas, criadas ANTES de qualquer mudança:
        //
        // 1. Ponto de Restauração do Windows — reversão completa e oficial do sistema
        //    (cobre tudo, inclusive os ajustes de rede via netsh, que não moram num valor
        //    de Registro simples). É a rede de segurança principal.
        // 2. Diário por valor (journal) — guarda o estado original de CADA valor que o
        //    programa altera (inclusive "não existia"). A opção [4] usa isso pra desfazer
        //    exatamente o que foi mudado, sem precisar reiniciar no modo de restauração.
        //
        // O diário é amarrado ao nome do computador em que foi criado. Bug real corrigido:
        // a versão anterior exportava chaves inteiras (incluindo Tcpip\Parameters, que
        // contém o Hostname) e restaurava sem checar a origem — copiar a pasta pra outro PC
        // e clicar em "Restaurar" importava o nome e a rede da máquina original nele.
        // ============================================================
        static List<JournalEntry> journal = new List<JournalEntry>();
        static HashSet<string> journaledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        static List<string> failedWrites = new List<string>();
        static int okWrites = 0;
        static string journalPath = null;

        static string BackupDir()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        static string B64(string s)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(s ?? ""));
        }

        static string FromB64(string s)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(s ?? ""));
        }

        static string JsonEscape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        static bool CreateSystemRestorePoint()
        {
            Console.WriteLine("🛡️ Criando Ponto de Restauração do Windows (pode levar até 1 minuto)...");

            // Compara o maior SequenceNumber antes/depois em vez da contagem: quando o
            // Windows atinge o limite de espaço, ele apaga pontos antigos ao criar um novo,
            // e a contagem pode ficar igual mesmo com sucesso.
            // SystemRestorePointCreationFrequency = 0 temporariamente: sem isso o Windows
            // recusa em silêncio se já houver um ponto criado nas últimas 24h.
            string script = @"
$ErrorActionPreference = 'SilentlyContinue'
$before = (Get-ComputerRestorePoint | Measure-Object -Property SequenceNumber -Maximum).Maximum
if ($null -eq $before) { $before = 0 }
Enable-ComputerRestore -Drive ($env:SystemDrive + '\')
$k = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore'
$old = (Get-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency).SystemRestorePointCreationFrequency
Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value 0 -Type DWord
Checkpoint-Computer -Description 'Adaptive PC Optimizer' -RestorePointType MODIFY_SETTINGS
if ($null -eq $old) { Remove-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency } else { Set-ItemProperty -Path $k -Name SystemRestorePointCreationFrequency -Value $old -Type DWord }
$after = (Get-ComputerRestorePoint | Measure-Object -Property SequenceNumber -Maximum).Maximum
if ($null -ne $after -and $after -gt $before) { 'RESTOREPOINT_OK' } else { 'RESTOREPOINT_FAIL' }
";
            string output = RunPowerShellScript(script, 180000);
            return output.Contains("RESTOREPOINT_OK");
        }

        // Lê o estado atual de um valor. Formato de saída do reg.exe (não traduzido pelo
        // idioma do Windows): "    Nome    REG_TIPO    dado".
        static bool QueryRegValue(string key, string name, out string type, out string data)
        {
            type = null;
            data = null;
            string output = RunCapture("reg", "query \"" + key + "\" /v \"" + name + "\"");
            string[] lines = output.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                Match m = Regex.Match(line, "^\\s+" + Regex.Escape(name) + "\\s+(REG_[A-Z_0-9]+)\\s*(.*)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    type = m.Groups[1].Value;
                    data = m.Groups[2].Value.TrimEnd();
                    return true;
                }
            }
            return false;
        }

        static void BeginJournal()
        {
            journal.Clear();
            journaledIds.Clear();
            failedWrites.Clear();
            okWrites = 0;

            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss-fffZ");
            string fileName = "journal_" + timestamp + ".txt";
            journalPath = Path.Combine(BackupDir(), fileName);
            SaveJournal();

            string metaFile = Path.Combine(BackupDir(), "latest_backup.json");
            string json = "{\"format\":2,\"machine\":\"" + JsonEscape(Environment.MachineName) +
                          "\",\"timestamp\":\"" + timestamp + "\",\"journalFile\":\"" + fileName + "\"}";
            File.WriteAllText(metaFile, json, Encoding.UTF8);
            Console.WriteLine("🛡️ Diário de alterações (pra desfazer pela opção [4]): " + journalPath);
        }

        // Regravado inteiro a cada entrada nova: se o programa for interrompido no meio,
        // o diário em disco já tem tudo que foi alterado até ali.
        static void SaveJournal()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("machine\t").Append(B64(Environment.MachineName)).Append("\r\n");
            foreach (JournalEntry e in journal)
            {
                sb.Append("entry\t").Append(B64(e.Key)).Append("\t").Append(B64(e.Name)).Append("\t")
                  .Append(e.Existed ? "1" : "0").Append("\t").Append(e.Type ?? "").Append("\t")
                  .Append(B64(e.Data)).Append("\r\n");
            }
            File.WriteAllText(journalPath, sb.ToString(), Encoding.UTF8);
        }

        // Toda escrita de Registro dos tweaks passa por aqui: registra o valor original
        // (uma vez por valor), aplica, e contabiliza o resultado REAL da escrita.
        static bool SetReg(string key, string name, string type, string data)
        {
            string id = key + "|" + name;
            if (journalPath != null && !journaledIds.Contains(id))
            {
                JournalEntry e = new JournalEntry();
                e.Key = key;
                e.Name = name;
                string oldType, oldData;
                e.Existed = QueryRegValue(key, name, out oldType, out oldData);
                e.Type = oldType;
                e.Data = oldData;
                journal.Add(e);
                journaledIds.Add(id);
                SaveJournal();
            }

            bool ok = RunReg("add \"" + key + "\" /v \"" + name + "\" /t " + type + " /d \"" + data + "\" /f");
            if (ok) okWrites++;
            else failedWrites.Add(key + "\\" + name);
            return ok;
        }

        static void OfferSystemRestore()
        {
            Console.WriteLine();
            Console.WriteLine("💡 Reversão 100% completa (inclusive rede/netsh): use o Ponto de Restauração");
            Console.WriteLine("   'Adaptive PC Optimizer' criado antes da otimização, na Restauração do Sistema do Windows.");
            if (AskYesNo("   Abrir a Restauração do Sistema agora? (S/N): "))
            {
                try { Process.Start("rstrui.exe"); }
                catch (Exception ex) { Console.WriteLine("   ⚠️ Não foi possível abrir: " + ex.Message); }
            }
        }

        static bool RestoreLatestBackupNative()
        {
            string metaFile = Path.Combine(BackupDir(), "latest_backup.json");
            if (!File.Exists(metaFile))
            {
                Console.WriteLine("⚠️ Nenhum diário de alterações encontrado nesta pasta (a opção [1] nunca rodou aqui).");
                OfferSystemRestore();
                return false;
            }

            string json = File.ReadAllText(metaFile, Encoding.UTF8);
            Match fileMatch = Regex.Match(json, "\"journalFile\"\\s*:\\s*\"([^\"]*)\"");
            Match tsMatch = Regex.Match(json, "\"timestamp\"\\s*:\\s*\"([^\"]*)\"");
            if (!fileMatch.Success)
            {
                Console.WriteLine("⚠️ Backup em formato antigo, sem identificação do computador de origem —");
                Console.WriteLine("   por segurança ele NÃO será aplicado (poderia ser de outra máquina).");
                OfferSystemRestore();
                return false;
            }

            string path = Path.Combine(BackupDir(), Path.GetFileName(fileMatch.Groups[1].Value));
            if (!File.Exists(path))
            {
                Console.WriteLine("⚠️ Arquivo do diário não encontrado: " + path);
                OfferSystemRestore();
                return false;
            }

            string machine = null;
            List<JournalEntry> entries = new List<JournalEntry>();
            try
            {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string[] p = line.Split('\t');
                    if (p.Length == 2 && p[0] == "machine")
                    {
                        machine = FromB64(p[1]);
                    }
                    else if (p.Length == 6 && p[0] == "entry")
                    {
                        JournalEntry e = new JournalEntry();
                        e.Key = FromB64(p[1]);
                        e.Name = FromB64(p[2]);
                        e.Existed = p[3] == "1";
                        e.Type = p[4];
                        e.Data = FromB64(p[5]);
                        entries.Add(e);
                    }
                }
            }
            catch
            {
                Console.WriteLine("⚠️ Diário de alterações corrompido — nada foi restaurado.");
                OfferSystemRestore();
                return false;
            }

            if (machine == null || !string.Equals(machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Este diário foi criado em OUTRO computador (" + (machine ?? "desconhecido") + ") —");
                Console.WriteLine("   este computador é " + Environment.MachineName + ". Nada foi restaurado, por segurança.");
                Console.ResetColor();
                return false;
            }

            Console.WriteLine("🔄 Desfazendo " + entries.Count + " alterações de " + (tsMatch.Success ? tsMatch.Groups[1].Value : "data desconhecida") + "...");
            int ok = 0;
            List<string> failed = new List<string>();
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                JournalEntry e = entries[i];
                bool r;
                if (e.Existed)
                {
                    r = RunReg("add \"" + e.Key + "\" /v \"" + e.Name + "\" /t " + e.Type + " /d \"" + e.Data + "\" /f");
                }
                else
                {
                    string t, d;
                    // Se o valor já não existe mais, o estado original (ausente) já está correto.
                    r = !QueryRegValue(e.Key, e.Name, out t, out d) || RunReg("delete \"" + e.Key + "\" /v \"" + e.Name + "\" /f");
                }
                if (r) ok++; else failed.Add(e.Key + "\\" + e.Name);
            }

            if (failed.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ " + ok + "/" + entries.Count + " valores do Registro voltaram ao estado original.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️ " + ok + "/" + entries.Count + " valores restaurados. Falharam:");
                foreach (string f in failed) Console.WriteLine("   - " + f);
                Console.ResetColor();
            }
            Console.WriteLine("ℹ️ Os ajustes de rede feitos via netsh não ficam no diário — só o Ponto de Restauração os desfaz.");
            OfferSystemRestore();
            return failed.Count == 0;
        }

        // ============================================================
        // Tweaks — toda escrita passa por SetReg (diário + contagem real de sucesso).
        // ============================================================
        const string MmKey = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile";
        const string IfeoKey = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options";

        static List<string> ApplyCpuSchedulerTweaks(HardwareProfile hw)
        {
            List<string> log = new List<string>();

            // 0x26 = quantum curto + variável + boost 3:1 pro programa em primeiro plano.
            // Bug corrigido: antes, máquinas com menos de 8 threads recebiam 0x28 (quantum
            // FIXO e SEM boost de primeiro plano) — o oposto do prometido, e justamente o
            // caso de PCs de entrada (ex.: i5 de 4 threads com GTX 1050 Ti).
            SetReg("HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl", "Win32PrioritySeparation", "REG_DWORD", "0x26");
            log.Add("• Escalonamento de CPU: Win32PrioritySeparation = 0x26 (quantum curto e variável, boost 3:1 pro programa em primeiro plano).");

            SetReg(MmKey, "NetworkThrottlingIndex", "REG_DWORD", "0xFFFFFFFF");
            SetReg(MmKey, "SystemResponsiveness", "REG_DWORD", "10");
            log.Add("• MMCSS: throttling de rede desativado e responsividade do sistema em 90% realtime / 10% background.");

            string gamesPath = MmKey + "\\Tasks\\Games";
            SetReg(gamesPath, "GPU Priority", "REG_DWORD", "8");
            SetReg(gamesPath, "Priority", "REG_DWORD", "6");
            SetReg(gamesPath, "Scheduling Category", "REG_SZ", "High");
            SetReg(gamesPath, "SFIO Priority", "REG_SZ", "High");
            log.Add("• Perfil MMCSS Games: prioridade de GPU e escalonamento em High.");

            SetReg(IfeoKey + "\\csrss.exe\\PerfOptions", "CpuPriorityClass", "REG_DWORD", "3");
            log.Add("• CSRSS: prioridade de despacho de mensagens de janela elevada.");

            return log;
        }

        static List<string> ApplyGpuDisplayTweaks(HardwareProfile hw)
        {
            List<string> log = new List<string>();

            if (hw.GpuSupportsHAGS && hw.OsBuild >= 19041)
            {
                SetReg("HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", "HwSchMode", "REG_DWORD", "2");
                log.Add("• HAGS (Agendamento de GPU acelerado por hardware): ativado — vale a partir do próximo reinício.");
            }

            SetReg("HKCU\\Software\\Microsoft\\Direct3D", "MaxFrameLatency", "REG_DWORD", "1");
            SetReg("HKLM\\SOFTWARE\\Microsoft\\Direct3D", "MaxFrameLatency", "REG_DWORD", "1");
            log.Add("• Direct3D: MaxFrameLatency = 1 (fila de quadros mínima).");

            SetReg("HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences", "DirectXShaderCacheSize", "REG_DWORD", "10240");
            log.Add("• Cache de shaders DirectX: limite configurado em 10GB.");

            SetReg("HKCU\\Software\\Microsoft\\Windows\\DWM", "Composition", "REG_DWORD", "1");
            SetReg("HKCU\\Software\\Microsoft\\Windows\\DWM", "EnableAeroPeek", "REG_DWORD", "1");
            log.Add("• DWM: composição de janelas configurada.");

            return log;
        }

        static List<string> ApplyInputDevicesTweaks()
        {
            List<string> log = new List<string>();

            SetReg("HKCU\\Control Panel\\Mouse", "MouseSpeed", "REG_SZ", "0");
            SetReg("HKCU\\Control Panel\\Mouse", "MouseThreshold1", "REG_SZ", "0");
            SetReg("HKCU\\Control Panel\\Mouse", "MouseThreshold2", "REG_SZ", "0");
            SetReg("HKCU\\Control Panel\\Mouse", "MouseSensitivity", "REG_SZ", "10");
            log.Add("• Mouse 1:1: aceleração de ponteiro (\"Aumentar precisão do ponteiro\") desativada.");

            SetReg("HKCU\\Control Panel\\Keyboard", "KeyboardDelay", "REG_SZ", "0");
            SetReg("HKCU\\Control Panel\\Keyboard", "KeyboardSpeed", "REG_SZ", "31");
            log.Add("• Teclado: menor atraso antes da repetição e taxa de repetição máxima (31).");

            // Removido: MouseDataQueueSize/KeyboardDataQueueSize = 100 — 100 JÁ é o padrão
            // do Windows, então o ajuste não mudava nada e o log afirmava um ganho que não existia.

            return log;
        }

        static List<string> ApplyStorageMemoryTweaks(HardwareProfile hw)
        {
            List<string> log = new List<string>();

            SetReg("HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem", "NtfsMemoryUsage", "REG_DWORD", "2");
            log.Add("• NTFS: cache de metadados em nível 2 (leitura mais rápida de muitos arquivos).");

            SetReg("HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem", "NtfsDisableLastAccessUpdate", "REG_DWORD", "1");
            log.Add("• NTFS: desativada a gravação de data de último acesso a cada leitura.");

            string mmKey = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management";
            if (hw.RamTotalGB >= 16)
            {
                SetReg(mmKey, "DisablePagingExecutive", "REG_DWORD", "1");
                log.Add("• Paging Executive: drivers do kernel mantidos na RAM física (16GB+ detectados).");
            }

            SetReg(mmKey, "LargeSystemCache", "REG_DWORD", "0");
            log.Add("• Memória: cache do sistema priorizando os programas/jogos abertos.");

            return log;
        }

        static List<string> ApplyNetworkLatencyTweaks()
        {
            List<string> log = new List<string>();

            // netsh não passa pelo diário (não é um valor de Registro simples) — é
            // revertido pelo Ponto de Restauração do Windows.
            RunSilent("netsh", "int tcp set global autotuninglevel=normal");
            RunSilent("netsh", "int tcp set global rss=enabled");
            RunSilent("netsh", "int tcp set global timestamps=disabled");
            RunSilent("netsh", "int tcp set global ecncapability=disabled");
            log.Add("• TCP/IP (netsh): RSS ativado e auto-tuning normal.");

            try
            {
                string interfacesKey = "HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces";
                string regOutput = RunCapture("reg", "query \"" + interfacesKey + "\"");
                string[] lines = regOutput.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("HKEY_LOCAL_MACHINE"))
                    {
                        string shortKey = "HKLM" + trimmed.Substring("HKEY_LOCAL_MACHINE".Length);
                        SetReg(shortKey, "TcpAckFrequency", "REG_DWORD", "1");
                        SetReg(shortKey, "TCPNoDelay", "REG_DWORD", "1");
                        SetReg(shortKey, "TcpDelAckTicks", "REG_DWORD", "0");
                    }
                }
                log.Add("• TCP baixa latência: algoritmo de Nagle desativado em todas as interfaces de rede.");
            }
            catch { }

            return log;
        }

        static List<string> ApplyDevGamingHybridTweaks()
        {
            List<string> log = new List<string>();

            string godotKey = IfeoKey + "\\Godot_v4.7.2-stable_win64_console.exe\\PerfOptions";
            SetReg(godotKey, "CpuPriorityClass", "REG_DWORD", "3");
            SetReg(godotKey, "IoPriority", "REG_DWORD", "3");
            log.Add("• Godot Engine: prioridade de CPU e I/O alta (sem efeito se o Godot não estiver instalado).");

            SetReg("HKCU\\System\\GameConfigStore", "GameDVR_Enabled", "REG_DWORD", "0");
            SetReg("HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR", "AllowGameDVR", "REG_DWORD", "0");
            SetReg("HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR", "AppCaptureEnabled", "REG_DWORD", "0");
            log.Add("• GameDVR: gravação em segundo plano da Xbox Game Bar desativada.");

            return log;
        }

        static void ApplyAllTweaksNative(HardwareProfile hw)
        {
            Console.WriteLine("🚀 Iniciando Otimização Adaptativa Segura...\n");

            if (CreateSystemRestorePoint())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Ponto de Restauração criado — dá pra desfazer TUDO pela Restauração do Sistema do Windows.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️ Não foi possível criar o Ponto de Restauração do Windows");
                Console.WriteLine("   (Proteção do Sistema bloqueada por política, ou sem espaço em disco).");
                Console.WriteLine("   O diário próprio do programa (opção [4]) ainda desfaz os ajustes de Registro,");
                Console.WriteLine("   mas NÃO os ajustes de rede via netsh.");
                Console.ResetColor();
                if (!AskYesNo("   Continuar mesmo assim? (S/N): "))
                {
                    Console.WriteLine("\nCancelado — nada foi alterado.");
                    return;
                }
            }
            Console.WriteLine();

            BeginJournal();
            Console.WriteLine();

            List<string> allLogs = new List<string>();

            Console.WriteLine("⚙️  [1/6] Otimizando Escalonamento de CPU e Prioridade de Threads...");
            allLogs.AddRange(ApplyCpuSchedulerTweaks(hw));

            Console.WriteLine("🎮 [2/6] Otimizando Comunicação GPU, DirectX e HAGS...");
            allLogs.AddRange(ApplyGpuDisplayTweaks(hw));

            Console.WriteLine("🖱️  [3/6] Otimizando Periféricos (Mouse 1:1, Teclado)...");
            allLogs.AddRange(ApplyInputDevicesTweaks());

            Console.WriteLine("💾 [4/6] Otimizando Cache NTFS e Alocação de Memória Física...");
            allLogs.AddRange(ApplyStorageMemoryTweaks(hw));

            Console.WriteLine("🌐 [5/6] Otimizando Pilha de Rede TCP/IP (Baixo Ping & Zero Nagle)...");
            allLogs.AddRange(ApplyNetworkLatencyTweaks());

            Console.WriteLine("🛠️  [6/6] Otimizando Ambiente Híbrido de Jogo + Desenvolvimento...");
            allLogs.AddRange(ApplyDevGamingHybridTweaks());

            Console.WriteLine("\n================================================================");
            if (failedWrites.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  ✅ OTIMIZAÇÃO CONCLUÍDA — " + okWrites + " ajustes de Registro aplicados, nenhuma falha.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ⚠️ OTIMIZAÇÃO PARCIAL — " + okWrites + " ajustes aplicados, " + failedWrites.Count + " falharam:");
                foreach (string f in failedWrites) Console.WriteLine("     - " + f);
            }
            Console.ResetColor();
            Console.WriteLine("================================================================");
            foreach (string item in allLogs) Console.WriteLine(item);
            Console.WriteLine("================================================================");
            Console.WriteLine("💡 Reinicie o computador pra todos os ajustes (principalmente HAGS) valerem.\n");
        }
        // ============================================================
        // Status — LÊ O VALOR REAL ATUAL DO REGISTRO em vez de afirmar um estado
        // fixo/otimista. Corrige o mesmo bug que existia em benchmark.js (valores
        // hardcoded que sempre diziam "ativado" mesmo sem nenhum tweak ter rodado).
        // ============================================================
        static string ReadRegValue(string keyPath, string valueName)
        {
            string output = RunCapture("reg", "query \"" + keyPath + "\" /v \"" + valueName + "\"");
            Match m = Regex.Match(output, valueName + "\\s+REG_[A-Z_]+\\s+(\\S+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        static void ShowStatusNative(HardwareProfile hw)
        {
            Console.WriteLine("📊 Métricas e Status REAL de Baixa Latência (lido agora do Registro):\n");

            string priSep = ReadRegValue("HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl", "Win32PrioritySeparation");
            Console.WriteLine("• CPU Scheduler (Win32PrioritySeparation): " + (priSep != null ? priSep : "não configurado (padrão do Windows)"));

            string tcpNoDelay = null;
            string ifacesOut = RunCapture("reg", "query \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\"");
            string[] ifaceLines = ifacesOut.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            foreach (string line in ifaceLines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("HKEY_LOCAL_MACHINE"))
                {
                    string shortKey = "HKLM" + trimmed.Substring("HKEY_LOCAL_MACHINE".Length);
                    string v = ReadRegValue(shortKey, "TCPNoDelay");
                    if (v != null) { tcpNoDelay = v; break; }
                }
            }
            Console.WriteLine("• Rede (TCPNoDelay / Nagle): " + (tcpNoDelay == "0x1" ? "Ativo (0ms delay)" : "não configurado (padrão do Windows)"));

            string mouseSpeed = ReadRegValue("HKCU\\Control Panel\\Mouse", "MouseSpeed");
            Console.WriteLine("• Mouse (aceleração de ponteiro): " + (mouseSpeed == "0" ? "Desativada (Raw 1:1)" : "padrão do Windows (aceleração ativa)"));

            string hags = ReadRegValue("HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers", "HwSchMode");
            Console.WriteLine("• GPU HAGS: " + (hags == "0x2" ? "Ativado" : (hw.GpuSupportsHAGS ? "Suportado pela GPU, mas não ativado ainda" : "GPU/build do Windows não suporta")));

            string ntfsCache = ReadRegValue("HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem", "NtfsMemoryUsage");
            Console.WriteLine("• Cache NTFS: " + (ntfsCache == "0x2" ? "Nível 2 (alto throughput)" : "padrão do Windows"));

            Console.WriteLine("\n🖥️  Hardware detectado agora:");
            Console.WriteLine("   CPU: " + hw.CpuThreads + " threads lógicas");
            Console.WriteLine("   GPU: " + hw.GpuName + " [" + hw.GpuVendor + "]" + (hw.GpuSupportsHAGS ? " (suporta HAGS)" : ""));
            Console.WriteLine("   RAM: " + hw.RamTotalGB + " GB");
            Console.WriteLine("   Windows Build: " + hw.OsBuild);
            Console.WriteLine("\n💡 Se nada foi \"configurado\" acima, rode a opção [1] pra aplicar os tweaks.\n");
        }

        // ============================================================
        // Drivers & Runtimes pós-formatação — CORRIGE dois bugs reais encontrados:
        // 1. Nunca verificava se o winget existia antes de usá-lo pra tudo — numa
        //    máquina sem winget (comum, especialmente logo após formatar, já que o
        //    "App Installer" da Microsoft Store pode não estar presente), TODA
        //    instalação falhava silenciosamente e mesmo assim imprimia "SUCESSO!".
        // 2. Driver de GPU nunca é baixado/instalado às cegas por script — cada
        //    fabricante exige aceitar termos próprios e o link de versão fica
        //    desatualizado rápido. Abre a página oficial certa em vez disso.
        // ============================================================
        static readonly Dictionary<string, string> GpuVendorPages = new Dictionary<string, string> {
            { "NVIDIA", "https://www.nvidia.com/Download/index.aspx" },
            { "AMD", "https://www.amd.com/en/support" },
            { "Intel", "https://www.intel.com/content/www/us/en/support/detect.html" }
        };

        static void OpenUrl(string url)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("cmd", "/c start \"\" \"" + url + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch { }
        }

        // Motor de drivers (src/drivers_engine.ps1, embutido no .exe como recurso):
        // detecta o hardware e instala a MELHOR versao de driver pra ele, so' de
        // fonte oficial e so' depois de conferir a assinatura digital do fabricante —
        // NVIDIA (Game Ready WHQL mais recente que suporta aquele modelo), chipset
        // AMD (pagina oficial do chipset da placa-mae) e o resto pelo catalogo de
        // drivers do Windows Update. Roda no mesmo console, com saida ao vivo.
        static bool RunDriverEngine(bool somenteVerificar)
        {
            string tmp = Path.Combine(Path.GetTempPath(), "AdaptivePCOptimizer_drivers_engine.ps1");
            try
            {
                using (Stream res = Assembly.GetExecutingAssembly().GetManifestResourceStream("drivers_engine.ps1"))
                {
                    if (res == null) { Console.WriteLine("  ⚠️ Motor de drivers ausente neste .exe (compilação sem o recurso)."); return false; }
                    using (FileStream f = File.Create(tmp)) res.CopyTo(f);
                }
                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + tmp + "\"" + (somenteVerificar ? " -SomenteVerificar" : ""));
                psi.UseShellExecute = false;   // herda o console: o usuario acompanha cada passo
                Process p = Process.Start(psi);
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("  ⚠️ Motor de drivers falhou: " + ex.Message);
                return false;
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
            }
        }

        static void CheckDriversNative()
        {
            Console.WriteLine("\n🔎 VERIFICAR DRIVERS — compara o instalado com a melhor versão oficial (não instala nada)");
            Console.WriteLine("================================================================");
            RunDriverEngine(true);
            Console.WriteLine("\n💡 Para instalar o que estiver desatualizado, use a opção [2].");
        }

        static void InstallDriversAndRuntimesNative(HardwareProfile hw)
        {
            Console.WriteLine("\n🔄 CENTRAL PÓS-FORMATAÇÃO — DRIVERS E RUNTIMES OFICIAIS (motor nativo)");
            Console.WriteLine("================================================================");

            Console.WriteLine("\n🔍 [Passo 1/3] Escaneando barramentos de hardware e dispositivos (PnP)...");
            if (RunSilent("pnputil", "/scan-devices"))
                Console.WriteLine("✅ Barramento PnP atualizado com sucesso.");
            else
                Console.WriteLine("⚠️ Não foi possível escanear o barramento PnP.");

            Console.WriteLine("\n🎮 [Passo 2/3] Drivers: melhor versão oficial para o seu hardware");
            Console.WriteLine("   (fonte oficial + assinatura digital do fabricante conferida antes de instalar)");
            if (!RunDriverEngine(false))
            {
                // Sem o motor (ou sem internet): cai no caminho manual de antes.
                string vendorPage;
                if (GpuVendorPages.TryGetValue(hw.GpuVendor, out vendorPage))
                {
                    Console.WriteLine("  🌐 Abrindo a página oficial de drivers " + hw.GpuVendor + " no navegador...");
                    OpenUrl(vendorPage);
                }
                OpenUrl("ms-settings:windowsupdate-optionalupdates");
            }

            Console.WriteLine("\n📦 [Passo 3/3] Runtimes essenciais (Microsoft, via winget quando disponível):");
            bool wingetOk = IsToolAvailable("winget");
            if (!wingetOk)
            {
                Console.WriteLine("  ⚠️ 'winget' não encontrado nesta máquina — runtimes NÃO foram instalados.");
                Console.WriteLine("     Instale o 'App Installer' pela Microsoft Store (é gratuito e oficial) e rode esta opção de novo,");
                Console.WriteLine("     ou baixe manualmente: Visual C++ Redistributable (https://aka.ms/vs/17/release/vc_redist.x64.exe)");
                Console.WriteLine("     e DirectX (https://www.microsoft.com/en-us/download/details.aspx?id=35).");
            }
            else
            {
                // Node.js saiu da lista: não é necessário pra quem só joga, e o .exe não
                // depende mais dele (sempre usa este motor nativo).
                string[][] packages = new string[][] {
                    new string[] { "Microsoft Visual C++ Redistributable (x64)", "Microsoft.VCRedist.2015+.x64" },
                    new string[] { "Microsoft Visual C++ Redistributable (x86)", "Microsoft.VCRedist.2015+.x86" },
                    new string[] { "DirectX End-User Runtime", "Microsoft.DirectX" },
                    new string[] { "Microsoft .NET Desktop Runtime 8", "Microsoft.DotNet.DesktopRuntime.8" }
                };
                int installedCount = 0;
                foreach (string[] pkg in packages)
                {
                    // Checa ANTES de instalar (pedido explícito do usuário: evitar conflito
                    // reinstalando por cima do que já está presente). RunSilent aqui já
                    // devolve exatamente o código de saída do winget: 0 = achou instalado,
                    // != 0 = "No installed package found" — não tenta parsear a tabela em
                    // texto, cuja largura de coluna varia por pacote e quebra com regex.
                    if (RunSilent("winget", "list --id " + pkg[1] + " --exact", 15000))
                    {
                        Console.WriteLine("  ✔️  " + pkg[0] + ": já instalado — pulando (sem reinstalar por cima, evita qualquer conflito).");
                        installedCount++;
                        continue;
                    }
                    Console.WriteLine("  ⬇️  Instalando " + pkg[0] + "...");
                    bool ok = RunSilent("winget", "install --id " + pkg[1] + " --silent --accept-package-agreements --accept-source-agreements --source winget", 120000);
                    Console.WriteLine("     " + (ok ? "✅ Instalado com sucesso." : "⚠️ Falhou — verifique a conexão com a internet."));
                    if (ok) installedCount++;
                }
                Console.WriteLine("\n  Resultado real: " + installedCount + "/" + packages.Length + " runtimes confirmados.");
            }

            Console.WriteLine("\n================================================================");
            Console.WriteLine("✅ Central pós-formatação concluída.");
            Console.WriteLine("   Se algum driver foi instalado, REINICIE o PC antes de jogar.");
            Console.WriteLine("================================================================\n");
        }

        // ============================================================
        // ============================================================
        // Dispatch — SEMPRE o motor nativo. Bug corrigido: antes, se Node.js existisse
        // na máquina (a própria opção [2] instalava), o .exe passava a rodar a engine JS,
        // que não tinha as correções de segurança deste arquivo. A engine JS continua no
        // repositório pra uso de desenvolvimento (npm run ...), mas não é mais usada aqui.
        // ============================================================
        static void RunScript(string args)
        {
            HardwareProfile hw = DetectHardware();

            // Compara argumentos INTEIROS. Antes era IndexOf (pedaco de texto): qualquer
            // argumento contendo "-d" — como "--check-drivers" — disparava a INSTALACAO
            // de drivers em vez da verificacao.
            HashSet<string> a = new HashSet<string>(args.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            Func<string[], bool> tem = delegate(string[] nomes) { foreach (string n in nomes) if (a.Contains(n)) return true; return false; };

            if (tem(new string[] { "--check-drivers", "verificar-drivers" }))
            {
                CheckDriversNative();
            }
            else if (tem(new string[] { "--apply", "-a", "optimize" }))
            {
                ApplyAllTweaksNative(hw);
            }
            else if (tem(new string[] { "--drivers", "-d", "format" }))
            {
                InstallDriversAndRuntimesNative(hw);
            }
            else if (tem(new string[] { "--restore", "-r", "restore" }))
            {
                RestoreLatestBackupNative();
            }
            else if (tem(new string[] { "--status", "-s", "status" }))
            {
                ShowStatusNative(hw);
            }
            else
            {
                Console.WriteLine("Argumento desconhecido: " + args + "  (use --apply, --drivers, --check-drivers, --status ou --restore)");
            }
        }

        static void Main(string[] args)
        {
            Console.Title = "Adaptive PC Optimizer — Universal Tweak & Driver Engine";
            // NÃO define Console.OutputEncoding = Encoding.UTF8 aqui — testado de verdade e
            // isso QUEBRA os acentos/box-drawing que já funcionavam (vira mojibake tipo
            // "â€¢" em vez de "•"), porque csc compila os literais de string em UTF-16 e o
            // console legado do Windows já lida bem com eles no codepage padrão; forçar
            // UTF-8 sem também ajustar o codepage do console (chcp 65001) piora, não ajuda.

            if (!IsAdministrator())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚡ Solicitando permissao de Administrador para aplicar ajustes de hardware...");
                Console.ResetColor();
                RelaunchAsAdmin(args);
                return;
            }

            if (args.Length > 0)
            {
                RunScript(string.Join(" ", args));
                return;
            }

            while (true)
            {
                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("================================================================");
                Console.WriteLine("       ⚡ ADAPTIVE PC OPTIMIZER — UNIVERSAL TWEAK ENGINE        ");
                Console.WriteLine("       Performance, Low Latency & Post-Formatting Driver Hub    ");
                Console.WriteLine("================================================================");
                Console.ResetColor();

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("┌──────────────────────────────────────────────────────────────┐");
                Console.WriteLine("│                      MENU DE OPÇÕES                          │");
                Console.WriteLine("├──────────────────────────────────────────────────────────────┤");
                Console.WriteLine("│  [1] ⚡ OTIMIZAR MEU PC (Adaptive PC Optimizer)               │");
                Console.WriteLine("│  [2] 🔄 FORMATEI MEU PC AGORA (Instalar Drivers e Runtimes)  │");
                Console.WriteLine("│  [3] 📊 VERIFICAR HARDWARE & STATUS DE LATÊNCIA              │");
                Console.WriteLine("│  [4] 🛡️ RESTAURAR BACKUP ORIGINAL DO REGISTRO                │");
                Console.WriteLine("│  [5] 🔎 VERIFICAR SE HÁ DRIVERS NOVOS (sem instalar)         │");
                Console.WriteLine("│  [0] ❌ SAIR                                                  │");
                Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("Escolha uma opção [0-5]: ");

                ConsoleKeyInfo key = Console.ReadKey(true);
                Console.WriteLine(key.KeyChar);
                Console.WriteLine();

                switch (key.KeyChar)
                {
                    case '1':
                        RunScript("--apply");
                        Console.WriteLine("\nPressione qualquer tecla para voltar ao menu...");
                        Console.ReadKey();
                        break;
                    case '2':
                        RunScript("--drivers");
                        Console.WriteLine("\nPressione qualquer tecla para voltar ao menu...");
                        Console.ReadKey();
                        break;
                    case '3':
                        RunScript("--status");
                        Console.WriteLine("\nPressione qualquer tecla para voltar ao menu...");
                        Console.ReadKey();
                        break;
                    case '4':
                        RunScript("--restore");
                        Console.WriteLine("\nPressione qualquer tecla para voltar ao menu...");
                        Console.ReadKey();
                        break;
                    case '5':
                        RunScript("--check-drivers");
                        Console.WriteLine("\nPressione qualquer tecla para voltar ao menu...");
                        Console.ReadKey();
                        break;
                    case '0':
                        return;
                    default:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Opção inválida.");
                        Console.ResetColor();
                        System.Threading.Thread.Sleep(1000);
                        break;
                }
            }
        }
    }
}
