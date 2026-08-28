using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace AdaptivePCOptimizer
{
    class Program
    {
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

        static void RunScript(string args)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string indexJs = Path.Combine(baseDir, "index.js");

            if (File.Exists(indexJs))
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
                    // Fallback to PowerShell if node is not on PATH
                }
            }

            // Fallback native execution via PowerShell
            Console.WriteLine("[INFO] Executando rotina nativa via PowerShell...");
            ProcessStartInfo psPsi = new ProcessStartInfo();
            psPsi.FileName = "powershell";
            psPsi.Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"winget install --id Microsoft.VCRedist.2015+.x64 --silent --accept-package-agreements ; pnputil /scan-devices\"";
            psPsi.UseShellExecute = false;
            using (Process p = Process.Start(psPsi))
            {
                p.WaitForExit();
            }
        }

        static void Main(string[] args)
        {
            Console.Title = "Adaptive PC Optimizer — Universal Tweak & Driver Engine";

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
