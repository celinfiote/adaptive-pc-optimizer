# ============================================================================
#  Motor de drivers do Adaptive PC Optimizer (embutido no .exe, opcao [2]).
#
#  Detecta o hardware e instala a MELHOR versao de driver pra ele, sempre de
#  fonte oficial, com a assinatura digital do fabricante conferida ANTES de
#  executar qualquer coisa:
#   1. GPU NVIDIA  -> catalogo oficial da NVIDIA: driver Game Ready WHQL mais
#                     recente QUE SUPORTA aquele modelo (ex.: RTX 3060 = 617.x;
#                     GTX 1050 Ti = 582.x, o ultimo ramo com suporte a ela).
#   2. Chipset AMD -> pagina oficial da AMD do chipset da placa-mae (B550, X570,
#                     B650...), instalador AMD Chipset Software.
#   3. Todo o resto (rede, Wi-Fi, Bluetooth, audio, GPU AMD/Intel, chipset Intel)
#                  -> catalogo de drivers do Windows Update (assinados pela
#                     Microsoft), sem deixar o Windows Update trocar o driver da
#                     NVIDIA por um mais antigo.
#
#  -SomenteVerificar: mostra o que esta instalado x o que existe, sem baixar
#  nem instalar nada.
# ============================================================================
param(
  [switch]$SomenteVerificar,
  [string]$Pasta = (Join-Path $env:TEMP 'AdaptivePCOptimizer_drivers')
)
$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$UA = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36'
$resumo = New-Object System.Collections.ArrayList
$precisaReiniciar = $false

function Titulo($t) { Write-Host "`n  == $t ==" -ForegroundColor Cyan }
function Info($t) { Write-Host "     $t" }
function Bom($t) { Write-Host "     [ok] $t" -ForegroundColor Green }
function Alerta($t) { Write-Host "     [!] $t" -ForegroundColor Yellow }
function Registrar($item, $estado) { [void]$resumo.Add([pscustomobject]@{ Item = $item; Resultado = $estado }) }

function Baixar($url, $destino, $referer) {
  New-Item -ItemType Directory -Force (Split-Path $destino) | Out-Null
  $h = @{}; if ($referer) { $h['Referer'] = $referer }
  Invoke-WebRequest $url -UseBasicParsing -UserAgent $UA -Headers $h -OutFile $destino -TimeoutSec 3600
}

# So' executa arquivo com assinatura digital valida DO FABRICANTE esperado.
function AssinaturaConfere($arquivo, $fabricante) {
  $s = Get-AuthenticodeSignature -LiteralPath $arquivo
  $sujeito = if ($s.SignerCertificate) { $s.SignerCertificate.Subject } else { '(sem assinatura)' }
  if ($s.Status -eq 'Valid' -and $sujeito -match [regex]::Escape($fabricante)) { Bom "assinatura digital valida: $fabricante"; return $true }
  Alerta "assinatura NAO confere (status $($s.Status), $sujeito) - arquivo descartado, nada foi executado"
  Remove-Item -LiteralPath $arquivo -Force -ErrorAction SilentlyContinue
  return $false
}

function Achar7z {
  foreach ($c in "$env:ProgramFiles\7-Zip\7z.exe", "${env:ProgramFiles(x86)}\7-Zip\7z.exe") { if (Test-Path $c) { return $c } }
  return $null
}

# ---------------------------------------------------------------------------
# 1. NVIDIA
# ---------------------------------------------------------------------------
# DriverVersion do Windows ("32.0.16.1664") -> versao NVIDIA ("616.64"): os 5
# ultimos digitos das duas ultimas partes.
function VersaoNvidia($driverVersion) {
  $p = $driverVersion -split '\.'
  if ($p.Count -lt 4) { return $null }
  $d = ($p[2] + $p[3].PadLeft(4, '0'))
  $d = $d.Substring($d.Length - 5)
  return [version]("{0}.{1}" -f [int]$d.Substring(0, 3), [int]$d.Substring(3))
}

function DriverNvidia {
  $gpus = @(Get-CimInstance Win32_VideoController | ? { $_.Name -match 'NVIDIA' })
  if (-not $gpus) { return $false }
  $g = $gpus[0]
  $modelo = ($g.Name -replace '^NVIDIA\s+', '').Trim()
  Titulo "Placa de video: $($g.Name)"
  $atual = VersaoNvidia $g.DriverVersion
  Info ("driver instalado: " + $(if ($atual) { $atual } else { 'nenhum / generico' }))

  try {
    $cat = ([xml](Invoke-WebRequest 'https://www.nvidia.com/Download/API/lookupValueSearch.aspx?TypeID=3' -UseBasicParsing -UserAgent $UA -TimeoutSec 60).Content).LookupValueSearch.LookupValues.LookupValue
  } catch { Alerta "catalogo da NVIDIA fora do ar: $($_.Exception.Message)"; Registrar 'Driver NVIDIA' 'catalogo indisponivel'; return $true }
  $prod = $cat | ? { $_.Name -eq $modelo } | Select -First 1
  if (-not $prod) { Alerta "modelo '$modelo' nao encontrado no catalogo da NVIDIA"; Registrar 'Driver NVIDIA' 'modelo nao encontrado no catalogo'; return $true }

  $os = if ([Environment]::OSVersion.Version.Build -ge 22000) { 135 } else { 57 }
  $u = "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&psid=$($prod.ParentID)&pfid=$($prod.Value)&osID=$os&languageCode=1046&isWHQL=1&dch=1&upCRD=0&sort1=0&numberOfResults=1"
  try { $r = Invoke-RestMethod $u -UserAgent $UA -TimeoutSec 60 } catch { Alerta "consulta de driver falhou: $($_.Exception.Message)"; Registrar 'Driver NVIDIA' 'consulta falhou'; return $true }
  $info = $r.IDS[0].downloadInfo
  if (-not $info.Version -or $info.DownloadURL -notmatch '^https://[a-z0-9.-]+\.nvidia\.com/') { Alerta 'a NVIDIA nao devolveu um driver valido pra este modelo'; Registrar 'Driver NVIDIA' 'sem driver no catalogo'; return $true }
  $melhor = [version]$info.Version
  Info ("melhor driver pra este modelo: {0} Game Ready WHQL ({1})" -f $melhor, $info.ReleaseDateTime)

  if ($atual -and $atual -ge $melhor) { Bom 'ja esta na melhor versao - nada a fazer'; Registrar "Driver NVIDIA $melhor" 'ja atualizado'; return $true }
  if ($SomenteVerificar) { Info "-> seria instalado: $melhor (atual: $atual)"; Registrar "Driver NVIDIA $melhor" "disponivel (atual $atual)"; return $true }

  $arq = Join-Path $Pasta ([IO.Path]::GetFileName($info.DownloadURL))
  Info ("baixando {0} ({1}) - pode demorar alguns minutos..." -f [IO.Path]::GetFileName($arq), $info.DownloadURLFileSize)
  try { Baixar $info.DownloadURL $arq } catch { Alerta "download falhou: $($_.Exception.Message)"; Registrar "Driver NVIDIA $melhor" 'download falhou'; return $true }
  if (-not (AssinaturaConfere $arq 'NVIDIA Corporation')) { Registrar "Driver NVIDIA $melhor" 'assinatura invalida - nao instalado'; return $true }

  # Instalacao silenciosa: extrai com 7-Zip e roda o setup.exe com -s (sem janelas).
  # Sem 7-Zip, o proprio pacote repassa -s pro setup.
  Info 'instalando (a tela pode piscar algumas vezes)...'
  $z = Achar7z
  if ($z) {
    $ext = Join-Path $Pasta "nvidia_$melhor"
    & $z x $arq "-o$ext" -y | Out-Null
    $p = Start-Process (Join-Path $ext 'setup.exe') -ArgumentList '-s', '-noreboot', '-noeula' -Wait -PassThru
  } else {
    $p = Start-Process $arq -ArgumentList '-s', '-noreboot', '-noeula' -Wait -PassThru
  }
  $depois = VersaoNvidia ((Get-CimInstance Win32_VideoController | ? { $_.Name -match 'NVIDIA' } | Select -First 1).DriverVersion)
  if ($depois -ge $melhor) { Bom "driver NVIDIA $melhor instalado"; Registrar "Driver NVIDIA $melhor" 'INSTALADO'; $script:precisaReiniciar = $true }
  else { Alerta "o instalador terminou (codigo $($p.ExitCode)), mas o driver ativo ainda e' $depois - reinicie e confira"; Registrar "Driver NVIDIA $melhor" "incerto (codigo $($p.ExitCode))"; $script:precisaReiniciar = $true }
  return $true
}

# ---------------------------------------------------------------------------
# 2. Chipset AMD
# ---------------------------------------------------------------------------
$SOQUETE = @{
  'A320' = 'am4'; 'B350' = 'am4'; 'X370' = 'am4'; 'B450' = 'am4'; 'X470' = 'am4'; 'A520' = 'am4'; 'B550' = 'am4'; 'X570' = 'am4'
  'A620' = 'am5'; 'B650' = 'am5'; 'B650E' = 'am5'; 'X670' = 'am5'; 'X670E' = 'am5'; 'B840' = 'am5'; 'B850' = 'am5'; 'X870' = 'am5'; 'X870E' = 'am5'
}

function ChipsetAmd {
  $cpu = Get-CimInstance Win32_Processor | Select -First 1
  if ($cpu.Manufacturer -notmatch 'AMD') { return }
  $placa = Get-CimInstance Win32_BaseBoard
  Titulo "Chipset AMD: $($placa.Manufacturer) $($placa.Product)"
  $m = [regex]::Match("$($placa.Product)", '\b([ABX]\d{3}E?)')
  $chip = if ($m.Success) { $m.Groups[1].Value.ToUpper() } else { $null }
  if (-not $chip -or -not $SOQUETE.ContainsKey($chip)) {
    Alerta "nao identifiquei o chipset pelo nome da placa - fica pro Windows Update"
    Registrar 'Chipset AMD' 'chipset nao identificado (Windows Update cobre o basico)'; return
  }
  $inst = Get-ItemProperty 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*', 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
    ? { $_.DisplayName -match '^AMD Chipset Software' } | Select -First 1
  $atual = if ($inst -and $inst.DisplayVersion) { try { [version]$inst.DisplayVersion } catch { $null } } else { $null }
  Info ("chipset $chip | instalado: " + $(if ($atual) { $atual } else { 'nenhum' }))

  $pagina = "https://www.amd.com/en/support/downloads/drivers.html/chipsets/$($SOQUETE[$chip])/$($chip.ToLower()).html"
  try { $html = (Invoke-WebRequest $pagina -UseBasicParsing -UserAgent $UA -TimeoutSec 60).Content } catch { Alerta "pagina da AMD indisponivel: $($_.Exception.Message)"; Registrar 'Chipset AMD' 'pagina indisponivel'; return }
  $link = [regex]::Matches($html, 'https://drivers\.amd\.com/drivers/AMD_Chipset_Software_([\d.]+)\.exe', 'IgnoreCase') |
    Sort-Object { [version]$_.Groups[1].Value } -Descending | Select -First 1
  if (-not $link) { Alerta 'nao achei o instalador na pagina oficial da AMD'; Registrar 'Chipset AMD' 'link nao encontrado'; return }
  $melhor = [version]$link.Groups[1].Value
  $melhorTexto = $link.Groups[1].Value   # como a AMD escreve (8.08.12.551), so' pra exibir
  Info "versao oficial mais recente: $melhorTexto"
  $nomeItem = "Chipset AMD $melhorTexto"
  if ($atual -and $atual -ge $melhor) { Bom 'ja esta na versao mais recente'; Registrar $nomeItem 'ja atualizado'; return }
  if ($SomenteVerificar) { Info "-> seria instalado: $melhorTexto"; Registrar $nomeItem 'disponivel'; return }

  $arq = Join-Path $Pasta ([IO.Path]::GetFileName($link.Value))
  Info 'baixando da AMD...'
  try { Baixar $link.Value $arq 'https://www.amd.com/' } catch { Alerta "download falhou: $($_.Exception.Message)"; Registrar $nomeItem 'download falhou'; return }
  if (-not (AssinaturaConfere $arq 'Advanced Micro Devices')) { Registrar $nomeItem 'assinatura invalida - nao instalado'; return }
  Info 'instalando (silencioso)...'
  $p = Start-Process $arq -ArgumentList '/S' -Wait -PassThru
  if ($p.ExitCode -eq 0) { Bom "AMD Chipset Software $melhorTexto instalado"; Registrar $nomeItem 'INSTALADO'; $script:precisaReiniciar = $true }
  else { Alerta "instalador terminou com codigo $($p.ExitCode)"; Registrar $nomeItem "falhou (codigo $($p.ExitCode))" }
}

# ---------------------------------------------------------------------------
# 3. Windows Update (drivers de todo o resto)
# ---------------------------------------------------------------------------
function DriversWindowsUpdate($temNvidia) {
  Titulo 'Demais dispositivos (rede, Wi-Fi, Bluetooth, audio, video integrado...) - Windows Update'
  try {
    $sessao = New-Object -ComObject Microsoft.Update.Session
    $sessao.ClientApplicationID = 'Adaptive PC Optimizer'
    $busca = $sessao.CreateUpdateSearcher()
    Info 'procurando drivers no catalogo da Microsoft (pode levar 1-2 minutos)...'
    $res = $busca.Search("IsInstalled=0 and Type='Driver' and IsHidden=0")
  } catch { Alerta "Windows Update indisponivel: $($_.Exception.Message)"; Registrar 'Windows Update' 'indisponivel'; return }

  $lista = New-Object -ComObject Microsoft.Update.UpdateColl
  foreach ($u in $res.Updates) {
    # O driver de video NVIDIA ja veio direto da NVIDIA (mais novo); o do Windows
    # Update costuma ser mais antigo e poderia substitui-lo.
    if ($temNvidia -and $u.Title -match 'NVIDIA' -and ($u.DriverClass -eq 'Display' -or $u.Title -match 'Display|Video')) { Info "ignorado (NVIDIA ja cuidada): $($u.Title)"; continue }
    Info "encontrado: $($u.Title)"
    if (-not $u.EulaAccepted) { try { $u.AcceptEula() } catch { } }
    [void]$lista.Add($u)
  }
  if ($lista.Count -eq 0) { Bom 'nenhum driver pendente'; Registrar 'Windows Update (drivers)' 'nada pendente'; return }
  if ($SomenteVerificar) { Registrar 'Windows Update (drivers)' "$($lista.Count) disponivel(is)"; return }

  Info "baixando $($lista.Count) driver(s)..."
  $dl = $sessao.CreateUpdateDownloader(); $dl.Updates = $lista; [void]$dl.Download()
  $inst = $sessao.CreateUpdateInstaller(); $inst.Updates = $lista
  Info 'instalando...'
  $r = $inst.Install()
  $okN = 0
  for ($i = 0; $i -lt $lista.Count; $i++) {
    $c = $r.GetUpdateResult($i).ResultCode   # 2 = sucesso, 3 = sucesso com erros
    if ($c -eq 2 -or $c -eq 3) { $okN++; Bom $lista.Item($i).Title } else { Alerta "falhou: $($lista.Item($i).Title) (codigo $c)" }
  }
  if ($r.RebootRequired) { $script:precisaReiniciar = $true }
  Registrar 'Windows Update (drivers)' "$okN de $($lista.Count) instalado(s)"
}

# ---------------------------------------------------------------------------
Write-Host ''
if ($SomenteVerificar) { Write-Host '  (modo verificacao: nada sera baixado nem instalado)' -ForegroundColor DarkGray }
New-Item -ItemType Directory -Force $Pasta | Out-Null
$temNvidia = DriverNvidia
ChipsetAmd
DriversWindowsUpdate $temNvidia

Write-Host "`n  ---------------- RESUMO DOS DRIVERS ----------------" -ForegroundColor Cyan
$resumo | % { Write-Host ("   {0,-34} {1}" -f $_.Item, $_.Resultado) }
if ($precisaReiniciar) { Write-Host "`n   REINICIE o PC para concluir a instalacao dos drivers." -ForegroundColor Yellow }
if (-not $SomenteVerificar) { Remove-Item -LiteralPath $Pasta -Recurse -Force -ErrorAction SilentlyContinue }
