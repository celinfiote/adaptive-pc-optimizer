using System.Reflection;
using System.Runtime.InteropServices;

// Identidade do binário.
//
// Por que isto existe: sem estes atributos o .exe sai com FileVersion 0.0.0.0 e sem
// Produto/Empresa/Descrição nenhum — e aí o prompt do UAC mostra "Editor: Desconhecido",
// com o SmartScreen avisando que o Windows "protegeu seu PC". Pra quem recebe o programa
// de outra pessoa, um executável anônimo que pede Administrador e escreve no Registro é
// indistinguível de malware. Os metadados não substituem assinatura digital (que exige
// certificado pago), mas fazem o Windows mostrar nome, versão e autor em vez de nada.
//
// Mantenha a versão em sincronia com o "version" do package.json.
[assembly: AssemblyTitle("Adaptive PC Optimizer")]
[assembly: AssemblyProduct("Adaptive PC Optimizer")]
[assembly: AssemblyDescription("Otimizador de desempenho e baixa latencia + central pos-formatacao de drivers para Windows 10 e 11")]
[assembly: AssemblyCompany("celinfiote")]
[assembly: AssemblyCopyright("Copyright (c) 2026 celinfiote - Licenca MIT")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]

// O programa é 64-bit puro (compilado com o csc do Framework64) e não expõe COM.
[assembly: ComVisible(false)]
