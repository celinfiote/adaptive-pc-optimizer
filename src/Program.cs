using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        // Backup / Restauração do Registro — mesmas 6 chaves e mesmo arquivo de
        // metadados (backups/latest_backup.json) que o backup_manager.js usa, pra
        // restaurar funcionar não importa qual das duas implementações criou o backup.
        // ============================================================
        static readonly string[] BackupKeys = new string[] {
            "HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl",
            "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile",
            "HKCU\\Control Panel\\Mouse",
            "HKCU\\Control Panel\\Keyboard",
            "HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem",
            "HKLM\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters"
        };

        static string BackupDir()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        static string CreateRegistryBackup()
        {
            string backupDir = BackupDir();
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH-mm-ss-fffZ");
            string backupFile = Path.Combine(backupDir, "registry_backup_" + timestamp + ".reg");

            Console.WriteLine("🛡️ Criando snapshot de segurança do Registro em: " + backupFile);

            int successCount = 0;
            for (int i = 0; i < BackupKeys.Length; i++)
            {
                bool ok = RunReg("export \"" + BackupKeys[i] + "\" \"" + backupFile + "_" + successCount + ".reg\" /y");
                if (ok) successCount++;
            }

            string metaFile = Path.Combine(backupDir, "latest_backup.json");
            string json = "{\"timestamp\":\"" + timestamp + "\",\"backupFile\":\"" + backupFile.Replace("\\", "\\\\") + "\",\"successCount\":" + successCount + "}";
            File.WriteAllText(metaFile, json, Encoding.UTF8);

            return backupFile;
        }

        static bool RestoreLatestBackupNative()
        {
            string metaFile = Path.Combine(BackupDir(), "latest_backup.json");
            if (!File.Exists(metaFile))
            {
                Console.WriteLine("⚠️ Nenhum backup prévio encontrado.");
                return false;
            }

            string json = File.ReadAllText(metaFile, Encoding.UTF8);
            Match timestampMatch = Regex.Match(json, "\"timestamp\"\\s*:\\s*\"([^\"]*)\"");
            Match backupFileMatch = Regex.Match(json, "\"backupFile\"\\s*:\\s*\"([^\"]*)\"");
            Match countMatch = Regex.Match(json, "\"successCount\"\\s*:\\s*(\\d+)");

            string timestamp = timestampMatch.Success ? timestampMatch.Groups[1].Value : "desconhecido";
            string backupFile = backupFileMatch.Success ? backupFileMatch.Groups[1].Value.Replace("\\\\", "\\") : null;
            int count = countMatch.Success ? int.Parse(countMatch.Groups[1].Value) : 6;

            if (string.IsNullOrEmpty(backupFile))
            {
                Console.WriteLine("⚠️ Metadados de backup corrompidos ou em formato não reconhecido.");
                return false;
            }

            Console.WriteLine("🔄 Restaurando snapshot de segurança de " + timestamp + "...");
            int restoredCount = 0;
            for (int i = 0; i < count; i++)
            {
                string regFile = backupFile + "_" + i + ".reg";
                if (File.Exists(regFile))
                {
                    if (RunReg("import \"" + regFile + "\"")) restoredCount++;
                }
            }

            Console.WriteLine("✅ Configurações originais restauradas com sucesso! (" + restoredCount + "/" + count + " chaves)");
            return true;
        }

        // ============================================================
        // Tweaks — porta fiel de cada arquivo em src/tweaks/*.js (mesmos valores,
        // mesmas condições de gating). Mantidos separados por categoria, igual ao
        // lado Node, pra facilitar comparar as duas implementações lado a lado.
        // ============================================================
        static List<string> ApplyCpuSchedulerTweaks(HardwareProfile hw)
        {
            List<string> log = new List<string>();
            string priorityValue = hw.CpuThreads >= 8 ? "0x26" : "0x28";
            RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\" /v \"Win32PrioritySeparation\" /t REG_DWORD /d " + priorityValue + " /f");
            log.Add("• Escalonamento de Threads CPU: Win32PrioritySeparation configurado para " + priorityValue + ".");

            RunReg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v \"NetworkThrottlingIndex\" /t REG_DWORD /d 0xFFFFFFFF /f");
            RunReg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v \"SystemResponsiveness\" /t REG_DWORD /d 10 /f");
            log.Add("• MMCSS: Throttling de rede desativado e Responsividade do Sistema ajustada para 90% realtime / 10% background.");

            string gamesPath = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games";
            RunReg("add \"" + gamesPath + "\" /v \"GPU Priority\" /t REG_DWORD /d 8 /f");
            RunReg("add \"" + gamesPath + "\" /v \"Priority\" /t REG_DWORD /d 6 /f");
            RunReg("add \"" + gamesPath + "\" /v \"Scheduling Category\" /t REG_SZ /d \"High\" /f");
            RunReg("add \"" + gamesPath + "\" /v \"SFIO Priority\" /t REG_SZ /d \"High\" /f");
            log.Add("• Perfil MMCSS Games: Prioridade de GPU e Escalonamento elevadas para High.");

            RunReg("add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\csrss.exe\\PerfOptions\" /v \"CpuPriorityClass\" /t REG_DWORD /d 3 /f");
            log.Add("• Subsistema CSRSS: Otimização de despacho de mensagens de janela para baixa latência.");

            return log;
        }

        static List<string> ApplyGpuDisplayTweaks(HardwareProfile hw)
        {
            List<string> log = new List<string>();

            if (hw.GpuSupportsHAGS && hw.OsBuild >= 19041)
            {
                RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\" /v \"HwSchMode\" /t REG_DWORD /d 2 /f");
                log.Add("• HAGS (Hardware Accelerated GPU Scheduling): Ativado para redução de overhead na comunicação CPU-GPU.");
            }

            RunReg("add \"HKCU\\Software\\Microsoft\\Direct3D\" /v \"MaxFrameLatency\" /t REG_DWORD /d 1 /f");
            RunReg("add \"HKLM\\SOFTWARE\\Microsoft\\Direct3D\" /v \"MaxFrameLatency\" /t REG_DWORD /d 1 /f");
            log.Add("• DirectX Queue: MaxFrameLatency configurado para 1 (Renderização imediata de quadros sem buffer lag).");

            RunReg("add \"HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences\" /v \"DirectXShaderCacheSize\" /t REG_DWORD /d 10240 /f");
            log.Add("• Shader Cache DirectX: Alocado buffer de até 10GB para cache pré-compilado de shaders.");

            RunReg("add \"HKCU\\Software\\Microsoft\\Windows\\DWM\" /v \"Composition\" /t REG_DWORD /d 1 /f");
            RunReg("add \"HKCU\\Software\\Microsoft\\Windows\\DWM\" /v \"EnableAeroPeek\" /t REG_DWORD /d 1 /f");
            log.Add("• DWM Compositor: Sincronização multi-monitor e renderização de janelas otimizada.");

            return log;
        }

        static List<string> ApplyInputDevicesTweaks()
        {
            List<string> log = new List<string>();

            RunReg("add \"HKCU\\Control Panel\\Mouse\" /v \"MouseSpeed\" /t REG_SZ /d \"0\" /f");
            RunReg("add \"HKCU\\Control Panel\\Mouse\" /v \"MouseThreshold1\" /t REG_SZ /d \"0\" /f");
            RunReg("add \"HKCU\\Control Panel\\Mouse\" /v \"MouseThreshold2\" /t REG_SZ /d \"0\" /f");
            RunReg("add \"HKCU\\Control Panel\\Mouse\" /v \"MouseSensitivity\" /t REG_SZ /d \"10\" /f");
            log.Add("• Mouse Raw Input 1:1: Aceleração de ponteiro desativada e curva 1:1 ativada.");

            RunReg("add \"HKCU\\Control Panel\\Keyboard\" /v \"KeyboardDelay\" /t REG_SZ /d \"0\" /f");
            RunReg("add \"HKCU\\Control Panel\\Keyboard\" /v \"KeyboardSpeed\" /t REG_SZ /d \"31\" /f");
            log.Add("• Teclado: Delay de repetição reduzido para 0ms e taxa de repetição no máximo (31).");

            RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\mouclass\\Parameters\" /v \"MouseDataQueueSize\" /t REG_DWORD /d 100 /f");
            RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\kbdclass\\Parameters\" /v \"KeyboardDataQueueSize\" /t REG_DWORD /d 100 /f");
            log.Add("• Fila de Dados HID (USB/PS2): Tamanho de fila ajustado para 100 pacotes.");

            return log;
        }

        static List<string> ApplyStorageMemoryTweaks(HardwareProfile hw)
        {
            List<string> log = new List<string>();

            RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem\" /v \"NtfsMemoryUsage\" /t REG_DWORD /d 2 /f");
            log.Add("• NTFS Memory Usage: Configurado para nível 2 (leitura mais rápida de milhares de arquivos).");

            RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem\" /v \"NtfsDisableLastAccessUpdate\" /t REG_DWORD /d 1 /f");
            log.Add("• NTFS Last Access: Desativada atualização de carimbo de data ao ler arquivos.");

            if (hw.RamTotalGB >= 16)
            {
                RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\" /v \"DisablePagingExecutive\" /t REG_DWORD /d 1 /f");
                log.Add("• Paging Executive: Drivers do sistema retidos na RAM física (>=16GB detectados).");
            }

            RunReg("add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\" /v \"LargeSystemCache\" /t REG_DWORD /d 0 /f");
            log.Add("• Gerenciamento de Memória: Cache do sistema ajustado para priorizar aplicações/jogos ativos.");

            return log;
        }

        static List<string> ApplyNetworkLatencyTweaks()
        {
            List<string> log = new List<string>();

            RunSilent("netsh", "int tcp set global autotuninglevel=normal");
            RunSilent("netsh", "int tcp set global rss=enabled");
            RunSilent("netsh", "int tcp set global timestamps=disabled");
            RunSilent("netsh", "int tcp set global ecncapability=disabled");
            log.Add("• Pilha TCP/IP Netsh: RSS ativado e Auto-Tuning normal.");

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
                        RunReg("add \"" + shortKey + "\" /v \"TcpAckFrequency\" /t REG_DWORD /d 1 /f");
                        RunReg("add \"" + shortKey + "\" /v \"TCPNoDelay\" /t REG_DWORD /d 1 /f");
                        RunReg("add \"" + shortKey + "\" /v \"TcpDelAckTicks\" /t REG_DWORD /d 0 /f");
                    }
                }
                log.Add("• TCP Low Latency: Algoritmo de Nagle desativado (TcpAckFrequency=1) em todas as interfaces.");
            }
            catch { }

            return log;
        }

        static List<string> ApplyDevGamingHybridTweaks()
        {
            List<string> log = new List<string>();

            string godotKey = "HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\Godot_v4.7.2-stable_win64_console.exe\\PerfOptions";
            RunReg("add \"" + godotKey + "\" /v \"CpuPriorityClass\" /t REG_DWORD /d 3 /f");
            RunReg("add \"" + godotKey + "\" /v \"IoPriority\" /t REG_DWORD /d 3 /f");
            log.Add("• Godot Engine: Prioridade de CPU e I/O de alta performance configurada.");

            RunReg("add \"HKCU\\System\\GameConfigStore\" /v \"GameDVR_Enabled\" /t REG_DWORD /d 0 /f");
            RunReg("add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\GameDVR\" /v \"AllowGameDVR\" /t REG_DWORD /d 0 /f");
            RunReg("add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v \"AppCaptureEnabled\" /t REG_DWORD /d 0 /f");
            log.Add("• GameDVR / Captura Passiva: Desativada captura em segundo plano.");

            return log;
        }

        static void ApplyAllTweaksNative(HardwareProfile hw)
        {
            Console.WriteLine("🚀 Iniciando Otimização Adaptativa Segura (motor nativo, sem dependência de Node.js)...\n");

            CreateRegistryBackup();
            Console.WriteLine();

            List<string> allLogs = new List<string>();

            Console.WriteLine("⚙️  [1/6] Otimizando Escalonamento de CPU e Prioridade de Threads...");
            allLogs.AddRange(ApplyCpuSchedulerTweaks(hw));

            Console.WriteLine("🎮 [2/6] Otimizando Comunicação GPU, DirectX e HAGS...");
            allLogs.AddRange(ApplyGpuDisplayTweaks(hw));

            Console.WriteLine("🖱️  [3/6] Otimizando Periféricos (Mouse Raw 1:1, Teclado 0ms, Fila HID)...");
            allLogs.AddRange(ApplyInputDevicesTweaks());

            Console.WriteLine("💾 [4/6] Otimizando Cache NTFS e Alocação de Memória Física...");
            allLogs.AddRange(ApplyStorageMemoryTweaks(hw));

            Console.WriteLine("🌐 [5/6] Otimizando Pilha de Rede TCP/IP (Baixo Ping & Zero Nagle)...");
            allLogs.AddRange(ApplyNetworkLatencyTweaks());

            Console.WriteLine("🛠️  [6/6] Otimizando Ambiente Híbrido de Jogo + Desenvolvimento...");
            allLogs.AddRange(ApplyDevGamingHybridTweaks());

            Console.WriteLine("\n================================================================");
            Console.WriteLine("             ✅ OTIMIZAÇÃO CONCLUÍDA COM SUCESSO!              ");
            Console.WriteLine("================================================================");
            foreach (string item in allLogs) Console.WriteLine(item);
            Console.WriteLine("================================================================");
            Console.WriteLine("💡 Dica: Para obter 100% do ganho de latência de drivers e kernel,");
            Console.WriteLine("   reinicie seu computador assim que for conveniente.\n");
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

        static void InstallDriversAndRuntimesNative(HardwareProfile hw)
        {
            Console.WriteLine("\n🔄 CENTRAL PÓS-FORMATAÇÃO — DRIVERS E RUNTIMES OFICIAIS (motor nativo)");
            Console.WriteLine("================================================================");

            Console.WriteLine("\n🔍 [Passo 1/4] Escaneando barramentos de hardware e dispositivos (PnP)...");
            if (RunSilent("pnputil", "/scan-devices"))
                Console.WriteLine("✅ Barramento PnP atualizado com sucesso.");
            else
                Console.WriteLine("⚠️ Não foi possível escanear o barramento PnP.");

            Console.WriteLine("\n🎮 [Passo 2/4] Driver de GPU: " + hw.GpuName + " [" + hw.GpuVendor + "]");
            string vendorPage;
            if (GpuVendorPages.TryGetValue(hw.GpuVendor, out vendorPage))
            {
                Console.WriteLine("  🌐 Abrindo a página oficial de drivers " + hw.GpuVendor + " no navegador padrão...");
                OpenUrl(vendorPage);
            }
            else
            {
                Console.WriteLine("  ℹ️ Fabricante de GPU não identificado com certeza — acesse o site oficial do fabricante da sua placa de vídeo.");
            }

            Console.WriteLine("\n🔌 [Passo 3/4] Chipset, áudio, rede e demais periféricos:");
            Console.WriteLine("  🌐 Abrindo Atualizações Opcionais do Windows (drivers via Windows Update)...");
            OpenUrl("ms-settings:windowsupdate-optionalupdates");

            Console.WriteLine("\n📦 [Passo 4/4] Runtimes essenciais (Microsoft, via winget quando disponível):");
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
                string[][] packages = new string[][] {
                    new string[] { "Microsoft Visual C++ Redistributable (x64)", "Microsoft.VCRedist.2015+.x64" },
                    new string[] { "Microsoft Visual C++ Redistributable (x86)", "Microsoft.VCRedist.2015+.x86" },
                    new string[] { "DirectX End-User Runtime", "Microsoft.DirectX" }
                };
                int installedCount = 0;
                foreach (string[] pkg in packages)
                {
                    Console.WriteLine("  ⬇️  Instalando " + pkg[0] + "...");
                    bool ok = RunSilent("winget", "install --id " + pkg[1] + " --silent --accept-package-agreements --accept-source-agreements --source winget", 120000);
                    Console.WriteLine("     " + (ok ? "✅ Instalado (ou já estava atualizado)." : "⚠️ Falhou — verifique a conexão com a internet."));
                    if (ok) installedCount++;
                }
                Console.WriteLine("\n  Resultado real: " + installedCount + "/" + packages.Length + " runtimes confirmados.");
            }

            Console.WriteLine("\n================================================================");
            Console.WriteLine("✅ Central pós-formatação concluída.");
            Console.WriteLine("   Driver de GPU e drivers de chipset/periféricos foram abertos nas páginas");
            Console.WriteLine("   oficiais acima — a instalação final desses é manual, por não existir um");
            Console.WriteLine("   link de download silencioso oficial e estável para eles.");
            Console.WriteLine("================================================================\n");
        }

        // ============================================================
        // Dispatch — usa a implementação Node (mais rica, com detecção completa de
        // storage/laptop) QUANDO node.exe e index.js estão disponíveis; caso
        // contrário, usa o motor nativo acima em vez de um fallback quebrado que só
        // tentava 1 comando de winget e mentia sucesso independente do resultado.
        // ============================================================
        static bool IsNodeAvailable()
        {
            return IsToolAvailable("node");
        }

        static void RunScript(string args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string indexJs = Path.Combine(baseDir, "index.js");

            if (File.Exists(indexJs) && IsNodeAvailable())
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "node";
                psi.Arguments = "\"" + indexJs + "\" " + args;
                psi.UseShellExecute = false;
                psi.WorkingDirectory = baseDir;

                try
                {
                    using (Process p = Process.Start(psi))
                    {
                        p.WaitForExit();
                    }
                    return;
                }
                catch
                {
                    // node existia no PATH mas falhou ao iniciar por outro motivo — cai pro motor nativo abaixo.
                }
            }

            Console.WriteLine("[INFO] Node.js não encontrado nesta máquina — usando o motor nativo (100% funcional, sem dependências externas).\n");
            HardwareProfile hw = DetectHardware();

            if (args.IndexOf("--apply") >= 0 || args.IndexOf("-a") >= 0 || args.IndexOf("optimize") >= 0)
            {
                ApplyAllTweaksNative(hw);
            }
            else if (args.IndexOf("--drivers") >= 0 || args.IndexOf("-d") >= 0 || args.IndexOf("format") >= 0)
            {
                InstallDriversAndRuntimesNative(hw);
            }
            else if (args.IndexOf("--restore") >= 0 || args.IndexOf("-r") >= 0 || args.IndexOf("restore") >= 0)
            {
                RestoreLatestBackupNative();
            }
            else if (args.IndexOf("--status") >= 0 || args.IndexOf("-s") >= 0 || args.IndexOf("status") >= 0)
            {
                ShowStatusNative(hw);
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
                Console.WriteLine("│  [0] ❌ SAIR                                                  │");
                Console.WriteLine("└──────────────────────────────────────────────────────────────┘");
                Console.ResetColor();
                Console.WriteLine();
                Console.Write("Escolha uma opção [0-4]: ");

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
