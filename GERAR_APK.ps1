param(
    # 1 = topo do jogo na esquerda do HDMI, -1 = na direita, 0 = sem giro
    [ValidateSet(1, -1, 0)][int]$Giro = 1,
    # Instala na TV Box pelo adb depois de gerar (TV Box ligada no adb)
    [switch]$Instalar,
    # Gera tambem a versao Windows (para testar no PC)
    [switch]$Windows,
    # Refaz a calibracao das jogadas (a fisica; leva alguns minutos)
    [switch]$Calibrar,
    # Nome da versao (ex.: 6.1)
    [string]$Versao = "",
    # Caminho do Unity.exe (opcional: o script procura o Unity 2022.3 instalado)
    [string]$CaminhoUnity = "",
    # Pasta de trabalho (opcional: o padrao e C:\StrikeKidsUnity)
    [string]$PastaTrabalho = ""
)
# STRIKE KIDS - EDICAO UNITY (OpenGL ES 2.0) - GERAR APK DA TV BOX.
# Rode pelo GERAR_APK.bat.
#
# Faz tudo sozinho e mostra cada passo na tela:
#  - acha o Unity 2022.3 (instalado pelo Unity Hub) e confere o suporte
#    Android dele (Android Build Support + OpenJDK + Android SDK & NDK);
#  - trabalha numa COPIA do projeto numa pasta curta e sem acentos
#    (C:\StrikeKidsUnity; o Gradle do Android nao gosta de caminho comprido):
#    o projeto original nao e alterado, e a copia guarda o que a Unity ja
#    importou (da 2a vez em diante e bem mais rapido);
#  - roda a Unity sem janela: configura o projeto (OpenGL ES 2.0, IL2CPP,
#    Android TV, a chave do projeto), calibra as jogadas se a fisica mudou e
#    gera o APK assinado;
#  - copia o APK para APK-Pronto\StrikeKids-Unity.apk (e instala, se pedir).
# 1a vez: de 20 a 60 min (a Unity importa tudo e compila o IL2CPP).
# Depois: de 3 a 10 min.
# Historico completo: Build\GERACAO-COMPLETA.log (envie se falhar).

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$Projeto = $PSScriptRoot.TrimEnd('\')
$NomeApk = "StrikeKids-Unity.apk"
$VersaoUnity = "2022.3"
$VersaoRecomendada = "2022.3.62f2"
$Metodo = "StrikeKids.Editor.Construir.Apk"
$MetodoPC = "StrikeKids.Editor.Construir.Windows"

$PastaLogs = Join-Path $Projeto "Build"
[void][IO.Directory]::CreateDirectory($PastaLogs)
$LogGeral = Join-Path $PastaLogs "GERACAO-COMPLETA.log"
$script:Etapa = 0
$script:TotalEtapas = 6
if ($Windows) { $script:TotalEtapas = 7 }
$script:Inicio = Get-Date
try { Stop-Transcript | Out-Null } catch { }
try { Start-Transcript -Path $LogGeral -Force | Out-Null } catch { }

# ------------------------------------------------------------------ tela

function Titulo([string]$Texto) {
    $script:Etapa++
    Write-Host ""
    Write-Host ("[{0}/{1}] {2}" -f $script:Etapa, $script:TotalEtapas, $Texto) -ForegroundColor Cyan
}
function Info([string]$Texto) { Write-Host "    $Texto" }
function Ok([string]$Texto) { Write-Host "    OK: $Texto" -ForegroundColor Green }
function Aviso([string]$Texto) { Write-Host "    AVISO: $Texto" -ForegroundColor Yellow }
function Erro([string]$Texto) { Write-Host "    ERRO: $Texto" -ForegroundColor Red }

function Sair([int]$Codigo) {
    $min = [int][Math]::Round(((Get-Date) - $script:Inicio).TotalMinutes)
    Write-Host ""
    Write-Host ("Tempo total: {0} min" -f $min)
    try { Stop-Transcript | Out-Null } catch { }
    exit $Codigo
}

# Junta argumentos como o Windows espera (aspas so onde precisa).
function Juntar-Argumentos([string[]]$Lista) {
    $partes = foreach ($a in $Lista) {
        if ($a -eq '') { '""' }
        elseif ($a -match '[\s";=,&()^|<>]') { '"' + (($a -replace '(\\*)"', '$1$1\"') -replace '(\\+)$', '$1$1') + '"' }
        else { $a }
    }
    return ($partes -join ' ')
}

# Mostra as linhas novas do log da Unity que interessam (o log inteiro e
# enorme): as do jogo ([StrikeKids]), os erros e o andamento da geracao.
function Mostrar-Novo([string]$Arquivo, [long]$Posicao) {
    if (-not (Test-Path -LiteralPath $Arquivo)) { return $Posicao }
    try {
        $fs = New-Object IO.FileStream($Arquivo, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
    } catch { return $Posicao }
    try {
        if ($fs.Length -le $Posicao) { return $Posicao }
        [void]$fs.Seek($Posicao, [IO.SeekOrigin]::Begin)
        $n = [int][Math]::Min([long]8MB, [long]($fs.Length - $Posicao))
        $buf = New-Object byte[] $n
        $lidos = $fs.Read($buf, 0, $n)
        if ($lidos -le 0) { return $Posicao }
        $fim = $lidos - 1
        while ($fim -ge 0 -and $buf[$fim] -ne 10) { $fim-- }
        if ($fim -lt 0) { return $Posicao }
        $texto = [Text.Encoding]::UTF8.GetString($buf, 0, $fim + 1)
        foreach ($linha in ($texto -split "`r`n|`n")) {
            $l = $linha.TrimEnd()
            if (-not $l) { continue }
            $mostrar = $false
            if ($l -match '\[StrikeKids\]') { $mostrar = $true; $l = ($l -replace '^.*\[StrikeKids\]\s*', '') }
            elseif ($l -match 'error CS\d+|Exception|Build Finished|BuildFailedException|Error building Player|FAILURE|No valid Unity Editor license|License|Gradle build failed|IL2CPP error') { $mostrar = $true }
            elseif ($l -match '^\s*(Start importing|Refreshing native plugins|Compiling shader|Building )') { $script:Andamento = $l }
            if ($mostrar) {
                if ($l.Length -gt 300) { $l = $l.Substring(0, 300) + " ..." }
                $cor = "Gray"
                if ($l -match 'error|Exception|FAILURE|failed|ERRO') { $cor = "Yellow" }
                Write-Host "      $l" -ForegroundColor $cor
                $script:UltimaSaida = Get-Date
            }
        }
        return $Posicao + $fim + 1
    } finally { $fs.Dispose() }
}

# Roda a Unity sem janela, mostrando o andamento, com tempo-limite.
# Devolve o codigo de saida (-999 = estourou o tempo).
function Rodar-Unity([string]$Exe, [string[]]$Argumentos, [string]$Log, [int]$LimiteMin, [string]$Rotulo) {
    if (Test-Path -LiteralPath $Log) { Remove-Item -LiteralPath $Log -Force -ErrorAction SilentlyContinue }
    $p = Start-Process -FilePath $Exe -ArgumentList (Juntar-Argumentos $Argumentos) -PassThru -WindowStyle Hidden
    try { $null = $p.Handle } catch { }
    $pos = 0L
    $comeco = Get-Date
    $script:UltimaSaida = $comeco
    $script:Andamento = ""
    while (-not $p.HasExited) {
        Start-Sleep -Milliseconds 1000
        $pos = Mostrar-Novo $Log $pos
        $agora = Get-Date
        $min = ($agora - $comeco).TotalMinutes
        if (($agora - $script:UltimaSaida).TotalSeconds -ge 60) {
            $extra = ""
            if ($script:Andamento) { $extra = " - " + $script:Andamento }
            if ($extra.Length -gt 120) { $extra = $extra.Substring(0, 120) + "..." }
            Write-Host ("      ... {0} trabalhando ha {1} min (normal, nao feche a janela){2}" -f $Rotulo, [int][Math]::Floor($min), $extra) -ForegroundColor DarkGray
            $script:UltimaSaida = $agora
        }
        if ($min -ge $LimiteMin) {
            Aviso "$Rotulo passou de $LimiteMin min: interrompido."
            try { & taskkill.exe /PID $p.Id /T /F | Out-Null } catch { try { $p.Kill() } catch { } }
            return -999
        }
    }
    try { $p.WaitForExit(60000) | Out-Null } catch { }
    $pos = Mostrar-Novo $Log $pos
    return [int]$p.ExitCode
}

function Ler-Texto([string]$Arquivo) {
    if (-not (Test-Path -LiteralPath $Arquivo)) { return "" }
    try {
        $fs = New-Object IO.FileStream($Arquivo, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
        try { $sr = New-Object IO.StreamReader($fs); return $sr.ReadToEnd() } finally { $fs.Dispose() }
    } catch { return "" }
}

# Explica o que deu errado olhando o log da Unity.
function Explicar-Falha([string]$Log) {
    $t = Ler-Texto $Log
    Write-Host ""
    if ($t -match 'No valid Unity Editor license|com\.unity\.editor\.headless|LICENSE SYSTEM.*(not|no)') {
        Erro "a Unity nao achou a licenca."
        Info "Abra o Unity Hub, entre na sua conta Unity (gratis) e, em Preferencias >"
        Info "Licencas, ative a licenca PERSONAL. Depois rode o GERAR_APK.bat de novo."
        return
    }
    if ($t -match 'another Unity instance is running|Multiple Unity instances cannot open the same project') {
        Erro "a Unity ja esta aberta com este projeto: feche a janela da Unity e rode de novo."
        return
    }
    if ($t -match 'Android SDK|AndroidSDKTools|SDK Platform|Unable to detect SDK|JDK not found|Java Development Kit') {
        Erro "falta o suporte Android da Unity (SDK, NDK ou OpenJDK)."
        Info "No Unity Hub: Instalacoes > (engrenagem) > Adicionar modulos > Android Build Support"
        Info "com 'OpenJDK' e 'Android SDK & NDK Tools' marcados."
        return
    }
    $linhas = @()
    foreach ($l in ($t -split "`r`n|`n")) {
        if ($l -match 'error|Exception|failed|FAILURE') { $linhas += $l.Trim() }
    }
    if ($linhas.Count -gt 0) {
        Erro "a Unity informou estes problemas (os ultimos):"
        foreach ($l in ($linhas | Select-Object -Last 25)) {
            if ($l.Length -gt 240) { $l = $l.Substring(0, 240) + " ..." }
            Write-Host "      $l" -ForegroundColor Yellow
        }
    }
}

# ---------------------------------------------------------------- Unity

# Versao "2022.3.62f2" -> numero comparavel
function Valor-Versao([string]$v) {
    if ($v -match '^(\d+)\.(\d+)\.(\d+)([abfp])(\d+)') {
        $tipo = @{ 'a' = 0; 'b' = 1; 'f' = 2; 'p' = 3 }[$Matches[4]]
        return ([long]$Matches[1] * 1000000000L) + ([long]$Matches[2] * 10000000L) + ([long]$Matches[3] * 10000L) + ($tipo * 1000L) + [long]$Matches[5]
    }
    return 0L
}

function Achar-Unity {
    if ($CaminhoUnity) {
        if (Test-Path -LiteralPath $CaminhoUnity) { return (Resolve-Path -LiteralPath $CaminhoUnity).Path }
        Aviso "nao achei $CaminhoUnity"
    }
    if ($env:UNITY_EXE -and (Test-Path -LiteralPath $env:UNITY_EXE)) { return $env:UNITY_EXE }
    $pastas = @()
    foreach ($base in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, "C:\Program Files", "D:\Program Files", "C:\Unity", "D:\Unity")) {
        if ($base) { $pastas += (Join-Path $base "Unity\Hub\Editor"); $pastas += $base }
    }
    # pasta escolhida no Unity Hub para instalar os editores
    foreach ($cfg in @((Join-Path $env:APPDATA "UnityHub\secondaryInstallPath.json"))) {
        if ($cfg -and (Test-Path -LiteralPath $cfg)) {
            try {
                $c = (Get-Content -LiteralPath $cfg -Raw).Trim().Trim('"') -replace '\\\\', '\'
                if ($c) { $pastas += $c }
            } catch { }
        }
    }
    $achados = @()
    foreach ($p in ($pastas | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $p)) { continue }
        foreach ($d in (Get-ChildItem -LiteralPath $p -Directory -ErrorAction SilentlyContinue)) {
            $exe = Join-Path $d.FullName "Editor\Unity.exe"
            if ((Test-Path -LiteralPath $exe) -and ($d.Name -like "$VersaoUnity.*")) {
                $achados += [pscustomobject]@{ Versao = $d.Name; Exe = $exe }
            }
        }
    }
    if ($achados.Count -eq 0) { return "" }
    $preferida = $achados | Where-Object { $_.Versao -eq $VersaoRecomendada } | Select-Object -First 1
    if ($preferida) { return $preferida.Exe }
    return ($achados | Sort-Object { Valor-Versao $_.Versao } -Descending | Select-Object -First 1).Exe
}

# =================================================================== passos

Write-Host "STRIKE KIDS - edicao Unity (OpenGL ES 2.0) - gerar o APK da TV Box" -ForegroundColor White
Write-Host ("Projeto: {0}" -f $Projeto)

Titulo "Procurando o Unity $VersaoUnity (Unity Hub)"
$Unity = Achar-Unity
if (-not $Unity) {
    Erro "nao achei o Unity $VersaoUnity instalado."
    Info "1) Instale o Unity Hub: https://unity.com/download"
    Info "2) No Unity Hub: Instalacoes > Instalar editor > Arquivo (Archive) e"
    Info "   escolha o Unity $VersaoRecomendada (LTS), marcando o modulo"
    Info "   'Android Build Support' com 'OpenJDK' e 'Android SDK & NDK Tools'."
    Info "3) Entre na sua conta Unity no Hub (licenca Personal, gratis)."
    Info "Ou rode: GERAR_APK.bat -CaminhoUnity ""C:\...\Editor\Unity.exe"""
    Info ""
    Info "SEM INSTALAR A UNITY: gere o APK na nuvem, pelo GitHub (aba Actions >"
    Info "'Gerar APK (TV Box)' > Run workflow). Passo a passo (uma vez so):"
    Info "docs\GERAR_APK_NA_NUVEM.md"
    Sair 1
}
$PastaEditor = Split-Path $Unity -Parent
$VersaoAchada = Split-Path (Split-Path $PastaEditor -Parent) -Leaf
Ok "Unity: $Unity"
if ($VersaoAchada -notlike "$VersaoUnity.*") { $VersaoAchada = "" }
if ($VersaoAchada -and $VersaoAchada -ne $VersaoRecomendada) {
    Aviso "versao $VersaoAchada (a recomendada e $VersaoRecomendada, com a correcao de seguranca de 2025); deve funcionar."
}

Titulo "Conferindo o suporte Android da Unity"
$Android = Join-Path $PastaEditor "Data\PlaybackEngines\AndroidPlayer"
if (-not (Test-Path -LiteralPath $Android)) {
    Erro "falta o modulo 'Android Build Support' neste Unity."
    Info "No Unity Hub: Instalacoes > (engrenagem do $VersaoAchada) > Adicionar modulos >"
    Info "Android Build Support, com 'OpenJDK' e 'Android SDK & NDK Tools' marcados."
    Sair 1
}
$falta = @()
foreach ($par in @(@("OpenJDK", "OpenJDK"), @("SDK", "Android SDK"), @("NDK", "Android NDK"))) {
    if (-not (Test-Path -LiteralPath (Join-Path $Android $par[0]))) { $falta += $par[1] }
}
if ($falta.Count -gt 0) {
    Aviso ("nao achei dentro do Unity: " + ($falta -join ", ") + ".")
    Info "Se a geracao falhar, no Unity Hub: Instalacoes > (engrenagem) > Adicionar modulos >"
    Info "Android Build Support > marque 'OpenJDK' e 'Android SDK & NDK Tools'."
} else { Ok "Android Build Support com OpenJDK, SDK e NDK" }

Titulo "Preparando a pasta de trabalho"
$Trabalho = $PastaTrabalho
if (-not $Trabalho) { $Trabalho = "C:\StrikeKidsUnity" }
try { [void][IO.Directory]::CreateDirectory($Trabalho) } catch {
    $Trabalho = Join-Path $env:LOCALAPPDATA "StrikeKidsUnity"
    [void][IO.Directory]::CreateDirectory($Trabalho)
}
if (Test-Path -LiteralPath (Join-Path $Trabalho "Temp\UnityLockfile")) {
    try {
        Remove-Item -LiteralPath (Join-Path $Trabalho "Temp\UnityLockfile") -Force -ErrorAction Stop
    } catch {
        Erro "a Unity parece estar aberta com a pasta $Trabalho : feche-a e rode de novo."
        Sair 1
    }
}
# espelha o projeto (menos o que a Unity gera: Library, Temp... ficam la e aceleram)
$rc = Join-Path $env:SystemRoot "System32\robocopy.exe"
$saidaRc = Join-Path $PastaLogs "copia.log"
& $rc $Projeto $Trabalho /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:2 /XD (Join-Path $Projeto "Library") (Join-Path $Projeto "Temp") (Join-Path $Projeto "Logs") (Join-Path $Projeto "Build") (Join-Path $Projeto "Obj") (Join-Path $Projeto "UserSettings") (Join-Path $Projeto "APK-Pronto") (Join-Path $Projeto ".git") "Library" "Temp" "Logs" "Build" "Obj" "UserSettings" "APK-Pronto" ".git" /XF "*.apk" > $saidaRc
if ($LASTEXITCODE -ge 8) {
    Erro "nao consegui copiar o projeto para $Trabalho (robocopy $LASTEXITCODE). Veja $saidaRc"
    Sair 1
}
# a versao do projeto = a do Unity achado (sem a pergunta de "versao diferente")
if ($VersaoAchada) {
    $pv = Join-Path $Trabalho "ProjectSettings\ProjectVersion.txt"
    Set-Content -LiteralPath $pv -Value ("m_EditorVersion: " + $VersaoAchada) -Encoding ASCII
}
Ok "copia em $Trabalho"

Titulo "Gerando o APK (Unity sem janela: importar, calibrar, compilar e assinar)"
Info "1a vez: de 20 a 60 min. Depois: de 3 a 10 min."
$ApkSaida = Join-Path $Trabalho "Build\Android\$NomeApk"
if (Test-Path -LiteralPath $ApkSaida) { Remove-Item -LiteralPath $ApkSaida -Force -ErrorAction SilentlyContinue }
$LogUnity = Join-Path $PastaLogs "unity-android.log"
$argsUnity = @("-batchmode", "-nographics", "-quit", "-projectPath", $Trabalho, "-buildTarget", "Android",
    "-executeMethod", $Metodo, "-logFile", $LogUnity, "-saida", $ApkSaida, "-giro", "$Giro")
if ($Versao) { $argsUnity += @("-versao", $Versao) }
if ($Calibrar) { $argsUnity += @("-calibrar") }
$cod = Rodar-Unity $Unity $argsUnity $LogUnity 150 "a Unity"
if ($cod -ne 0 -or -not (Test-Path -LiteralPath $ApkSaida)) {
    Erro ("a Unity terminou com o codigo {0} sem gerar o APK." -f $cod)
    Explicar-Falha $LogUnity
    Info "Log completo da Unity: $LogUnity"
    Sair 1
}
Ok "APK gerado"

Titulo "Copiando o APK pronto"
$Pronto = Join-Path $Projeto "APK-Pronto"
[void][IO.Directory]::CreateDirectory($Pronto)
$Destino = Join-Path $Pronto $NomeApk
Copy-Item -LiteralPath $ApkSaida -Destination $Destino -Force
$mb = [Math]::Round((Get-Item -LiteralPath $Destino).Length / 1MB, 1)
Ok ("{0} ({1} MB)" -f $Destino, $mb)
Info ("Pacote: com.lazersport.strikekids.unity - giro da tela: {0}" -f $Giro)

if ($Windows) {
    Titulo "Gerando a versao Windows (para testar no PC)"
    $LogPc = Join-Path $PastaLogs "unity-windows.log"
    $ExePc = Join-Path $Trabalho "Build\Windows\StrikeKids.exe"
    $argsPc = @("-batchmode", "-nographics", "-quit", "-projectPath", $Trabalho, "-buildTarget", "Win64",
        "-executeMethod", $MetodoPC, "-logFile", $LogPc, "-saida", $ExePc)
    $cod = Rodar-Unity $Unity $argsPc $LogPc 90 "a Unity (Windows)"
    if ($cod -eq 0 -and (Test-Path -LiteralPath $ExePc)) {
        $PcPronto = Join-Path $Projeto "PC-Pronto"
        & $rc (Join-Path $Trabalho "Build\Windows") $PcPronto /MIR /NFL /NDL /NJH /NJS /NP /R:2 /W:2 > (Join-Path $PastaLogs "copia-pc.log")
        Ok "PC-Pronto\StrikeKids.exe (A S D F G = sensores, 1 = START, 5 = ficha, 9 = menu)"
    } else {
        Aviso "a versao Windows nao saiu (o APK esta pronto). Veja $LogPc"
    }
}

Titulo "Instalar na TV Box"
if ($Instalar) {
    $adb = Join-Path $Android "SDK\platform-tools\adb.exe"
    if (-not (Test-Path -LiteralPath $adb)) { $adb = "adb.exe" }
    try {
        & $adb install -r $Destino
        if ($LASTEXITCODE -eq 0) { Ok "instalado na TV Box" } else { Aviso "o adb nao conseguiu instalar (a TV Box esta ligada no adb?)" }
    } catch { Aviso "nao achei o adb" }
} else {
    Info "Copie o APK para um pendrive e instale na TV Box (ou use: GERAR_APK.bat -Instalar)."
}
Sair 0
