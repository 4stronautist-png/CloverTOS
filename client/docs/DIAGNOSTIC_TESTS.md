# TEMPLATES DE DIAGNÓSTICO E TESTES

## 🧪 TESTE 1: VERIFICAR VCRUNTIME

### Versão PowerShell (Fácil)
```powershell
# Copie e Cole Tudo Exatamente:
Get-ItemProperty HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\* | `
    Where-Object {$_.DisplayName -like "*Visual C++*" -or $_.DisplayName -like "*Runtime*"} | `
    Select-Object DisplayName, DisplayVersion | `
    Format-Table -AutoSize

# RESULTADO ESPERADO:
# DisplayName                                DisplayVersion
# -------                                    ---------------
# Microsoft Visual C++ 2015 Redistributable  14.0.24215
# Microsoft Visual C++ 2017 Redistributable  14.16.27033
# Microsoft Visual C++ 2019 Redistributable  14.29.30139
# Microsoft Visual C++ 2022 Redistributable  14.38.33135

# SE FALTAR ALGUMA: Download em https://support.microsoft.com/en-us/help/2977003
```

### Se não tem nenhuma versão:
```powershell
# 1. Abra PowerShell como Administrator
# 2. Execute:
Start-Process "https://support.microsoft.com/en-us/help/2977003"

# 3. Download e instale TODAS as versões:
#    - Visual C++ 2015 Redistributable
#    - Visual C++ 2017 Redistributable
#    - Visual C++ 2019 Redistributable
#    - Visual C++ 2022 Redistributable

# 4. Restart Windows

# 5. Tente executar cliente novamente
```

---

## 🧪 TESTE 2: VERIFICAR DIRECTX

### Versão Gráfica (Mais Fácil)
```powershell
# Copie e Cole Exatamente:
dxdiag

# Se abrir DXDIAG:
# 1. Vá aba "Display" (ou "System")
# 2. Procure por:
#    ✅ Direct3D: ON/Enabled
#    ✅ Direct3D Feature Level: 11.0 ou superior
#    ✅ GPU: Deve estar listada com memória disponível
#
# Se algo estiver OFF/Disabled/Missing:
#    → Atualizar drivers de vídeo (NVIDIA/AMD/Intel)
#    → Ou instalar DirectX End-User Runtime
```

### Versão Command Line
```powershell
# Alternativamente, execute:
Get-WmiObject Win32_VideoController | Select-Object Name, AdapterRAM, DriverVersion | Format-Table -AutoSize

# RESULTADO ESPERADO:
# Name              AdapterRAM    DriverVersion
# ----              ----------    --------------
# NVIDIA GeForce    8589934592    546.89
# (ou similar para AMD/Intel)

# Se AdapterRAM for 0 ou não se conectar:
#    → Drivers desatualizados
#    → Download em: nvidia.com ou amd.com ou intel.com
```

---

## 🧪 TESTE 3: EXECUTAR CLIENTE COM CAPTURA DE ERRO

### Script Completo (Copie e Cole)
```powershell
# ========================================================================
# SCRIPT: Capturar erro do cliente Tree of Savior
# INSTRUÇÃO: Abra PowerShell como Administrator e cole TODO este código
# ========================================================================

$clientPath = "C:\Program Files (x86)\Steam\steamapps\common\TreeOfSavior\release\Client_tos_x64.exe"
$outputDir = "C:\"
$stdoutFile = "$outputDir\tos_stdout.txt"
$stderrFile = "$outputDir\tos_stderr.txt"

Write-Host "===== INICIANDO DIAGNÓSTICO =====" -ForegroundColor Cyan

# Verificar se arquivo existe
if (!(Test-Path $clientPath)) {
    Write-Host "❌ ERRO: Arquivo não encontrado: $clientPath" -ForegroundColor Red
    Exit 1
}
Write-Host "✅ Cliente encontrado: $clientPath" -ForegroundColor Green

# Criar processo com captura
Write-Host "⏳ Iniciando cliente (aguarde 10 segundos)..." -ForegroundColor Yellow
$pinfo = New-Object System.Diagnostics.ProcessStartInfo
$pinfo.FileName = $clientPath
$pinfo.UseShellExecute = $false
$pinfo.RedirectStandardOutput = $true
$pinfo.RedirectStandardError = $true
$pinfo.CreateNoWindow = $false

$process = [System.Diagnostics.Process]::Start($pinfo)

# Esperar 10 segundos
Start-Sleep -Seconds 10

# Verificar se ainda está rodando
if (!$process.HasExited) {
    Write-Host "⏹️  Matando processo (ainda estava rodando)..." -ForegroundColor Yellow
    $process.Kill()
    $process.WaitForExit()
}

# Capturar output
$stdout = $process.StandardOutput.ReadToEnd()
$stderr = $process.StandardError.ReadToEnd()
$exitCode = $process.ExitCode

# Salvar em arquivos
$stdout | Out-File $stdoutFile -Encoding UTF8
$stderr | Out-File $stderrFile -Encoding UTF8

# Exibir resultado
Write-Host "`n===== RESULTADO =====" -ForegroundColor Cyan
Write-Host "Exit Code: $exitCode" -ForegroundColor $(if ($exitCode -eq 0) { 'Green' } else { 'Red' })
Write-Host "Stdout arquivo: $stdoutFile" -ForegroundColor Green
Write-Host "Stderr arquivo: $stderrFile" -ForegroundColor Green

Write-Host "`n===== STDOUT (primeiras 50 linhas) =====" -ForegroundColor Cyan
if ($stdout) {
    $stdout.Split("`n") | Select-Object -First 50 | Write-Host
} else {
    Write-Host "(vazio)" -ForegroundColor Yellow
}

Write-Host "`n===== STDERR (primeiras 50 linhas) =====" -ForegroundColor Cyan
if ($stderr) {
    $stderr.Split("`n") | Select-Object -First 50 | Write-Host
} else {
    Write-Host "(vazio)" -ForegroundColor Yellow
}

Write-Host "`n===== PRÓXIMOS PASSOS =====" -ForegroundColor Cyan
if ($exitCode -eq 0) {
    Write-Host "✅ Cliente foi executado com sucesso!" -ForegroundColor Green
    Write-Host "Se a GUI não apareceu mesmo assim, pode estar rodando em background"
} elseif ($exitCode -eq -1073740940) {
    Write-Host "❌ Erro de Heap Corruption (0xC0000374)" -ForegroundColor Red
    Write-Host "Possível causa: VC++ Runtime ou DirectX incompatível" -ForegroundColor Red
    Write-Host "Ação: Execute TESTE 1 e TESTE 2 acima" -ForegroundColor Yellow
} else {
    Write-Host "❌ Erro desconhecido (code: $exitCode)" -ForegroundColor Red
    Write-Host "Verifique os arquivos de erro em:" -ForegroundColor Yellow
    Write-Host "  Stdout: $stdoutFile" -ForegroundColor Yellow
    Write-Host "  Stderr: $stderrFile" -ForegroundColor Yellow
}

# Oferecer abrir arquivos
Write-Host "`nDeseja abrir os arquivos de diagnóstico? (S/N)" -ForegroundColor Cyan
$response = Read-Host
if ($response -eq "S" -or $response -eq "s") {
    notepad.exe $stderrFile
}
```

### Para usar:
1. Abra PowerShell como **Administrator**
2. Cole TODO o código acima
3. Pressione Enter
4. Aguarde ~15 segundos
5. Leia o resultado na tela
6. Arquivos salvos em: `C:\tos_stdout.txt` e `C:\tos_stderr.txt`

---

## 🧪 TESTE 4: VERIFICAR EVENT VIEWER

### Procurar erros recentes
```powershell
# Copie exatamente:
Get-EventLog -LogName Application -Newest 100 | `
    Where-Object {$_.TimeGenerated -gt (Get-Date).AddHours(-2)} | `
    Where-Object {$_.Source -like "*TOSClient*" -or $_.Source -like "*Client*" -or $_.EntryType -eq "Error"} | `
    Format-Table -AutoSize TimeGenerated, Source, EventID, Message | `
    Out-String | Tee-Object -FilePath "C:\Event-Logs.txt"

# Resultado salvo em: C:\Event-Logs.txt
# Procure por qualquer mensagem relacionada ao cliente ou a DLLs faltando
```

---

## 🧪 TESTE 5: TESTAR CLIENTE 32-BIT

### Se existe versão 32-bit
```powershell
# Verificar se existe:
if (Test-Path "C:\Program Files (x86)\Steam\steamapps\common\TreeOfSavior\release\Client_tos.exe") {
    Write-Host "✅ Cliente 32-bit encontrado, tentando executar..."
    & "C:\Program Files (x86)\Steam\steamapps\common\TreeOfSavior\release\Client_tos.exe"
}  else {
    Write-Host "❌ Cliente 32-bit não encontrado"
}

# Se funciona:
#    → Problema é com versão 64-bit
#    → Use 32-bit como workaround
#    → Problema pode ser VC++ Runtime apenas para x64

# Se não funciona:
#    → Problema é comum a ambas versões
#    → Provavelmente VC++ Runtime ou DirectX
```

---

## 🧪 TESTE 6: EXECUTAR DE LOCALIZAÇÃO ALTERNATIVA

### Copiar para C:\Games e tentar
```powershell
# ========================================================================
# SCRIPT: Copiar cliente para localização sem proteções
# ========================================================================

$source = "C:\Program Files (x86)\Steam\steamapps\common\TreeOfSavior\release"
$dest = "C:\Games\TreeOfSavior\release"

# Criar pasta
if (!(Test-Path "C:\Games")) {
    New-Item -ItemType Directory -Path "C:\Games" -Force | Out-Null
}

Write-Host "⏳ Copiando cliente para $dest (pode levar 2-3 minutos)..." -ForegroundColor Yellow
Copy-Item -Path $source -Destination $dest -Recurse -Force

Write-Host "✅ Cópia concluída!" -ForegroundColor Green
Write-Host "🚀 Tentando executar dari nova localização..." -ForegroundColor Cyan

& "$dest\Client_tos_x64.exe"

# Se funciona dari nova localização:
#    → Problema era permissões em Program Files
#    → Use esta cópia para jogar
#    → Coloque atalho em Desktop se quiser
```

### Criar atalho (se funcionou nessa localização)
```powershell
$target = "C:\Games\TreeOfSavior\release\Client_tos_x64.exe"
$shortcut = "C:\Users\$env:USERNAME\Desktop\TreeOfSavior.lnk"

$WshShell = New-Object -ComObject WScript.Shell
$shortcutObj = $WshShell.CreateShortcut($shortcut)
$shortcutObj.TargetPath = $target
$shortcutObj.WorkingDirectory = "C:\Games\TreeOfSavior\release"
$shortcutObj.Save()

Write-Host "✅ Atalho criado no Desktop: TreeOfSavior.lnk" -ForegroundColor Green
```

---

## 🧪 TESTE 7: DESABILITAR ANTIVÍRUS TEMPORARIAMENTE

### Windows Defender
```powershell
# Desabilitar:
Set-MpPreference -DisableRealtimeMonitoring $true
Write-Host "✅ Windows Defender real-time protection desabilitado" -ForegroundColor Green
Write-Host "⚠️  NÃO ESQUEÇA DE HABILITAR NOVAMENTE depois do teste!" -ForegroundColor Yellow

# Tentar executar cliente:
& "C:\Program Files (x86)\Steam\steamapps\common\TreeOfSavior\release\Client_tos_x64.exe"

# Para habilitar novamente:
Set-MpPreference -DisableRealtimeMonitoring $false
Write-Host "✅ Windows Defender re-habilitado" -ForegroundColor Green
```

### Outros Antivírus
```
Para Avast, McAfee, Norton, etc:
1. Abra interface do antivírus
2. Procure por "Protection", "Real-time Monitoring", ou "Shields"
3. Desabilitar temporariamente
4. Tentar executar cliente
5. IMPORTANTE: Habilitar novamente após teste
```

---

## 📋 MATRIZ DE DIAGNÓSTICO

Após executar tests acima, preencha:

```
RESULTADO DE TESTES:

[__] TESTE 1 - VC++ Runtime
     Resultado: ✅ Tudo instalado / ❌ Falta versão ___

[__] TESTE 2 - DirectX
     Direct3D: ✅ On / ❌ Off
     Feature Level: (versão: ___)
     GPU Detectada: ✅ Sim / ❌ Não

[__] TESTE 3 - Cliente Com Captura
     Exit Code: ___
     Stderr: (primeiras 5 linhas): ...
     Stderr: (primeiras 5 linhas): ...

[__] TESTE 4 - Event Viewer
     Erros encontrados: ✅ Sim / ❌ Não
     Relevância: (descreva)

[__] TESTE 5 - Cliente 32-bit
     Funciona: ✅ Sim / ❌ Não / ⏸️ Não testado

[__] TESTE 6 - C:\Games Cópia
     Funciona: ✅ Sim / ❌ Não / ⏸️ Não testado

[__] TESTE 7 - Sem Antivírus
     Funciona: ✅ Sim / ❌ Não / ⏸️ Não testado

CONCLUSÃO:
Se GUI aparece agora: ✅ RESOLVIDO
Se ainda falha: ❌ Precisa diagnóstico avançado
```

---

## 💾 ARQUIVOS DE DIAGNÓSTICO ESPERADOS

Após executar tests, você deverá ter em C:\:

```
C:\
├── tos_stdout.txt                 (output do cliente)
├── tos_stderr.txt                 (erros do cliente)
├── Event-Logs.txt                 (logs do Event Viewer)
├── Games\TreeOfSavior\            (cópia de teste)
└── [dados com seus resultados]
```

---

## 🎯 PASSO A PASSO RECOMENDADO

### Se tem 30 min:
1. Execute TESTE 1 (VC++ Runtime)
2. Se não tem: Instale, restart, tente novamente
3. Se ainda falha: Execute TESTE 2 (DirectX)

### Se tem 1 hora:
1. TESTE 1 + 2 (VC++ e DirectX)
2. Se não funciona: TESTE 6 (C:\Games localização alternativa)
3. Se não funciona: TESTE 7 (Desabilitar antivírus)

### Se tem 2+ horas:
1. Execute TODOS os tests acima
2. Observe qual resolve (se algum)
3. Execute TESTE 3 com captura de erro
4. Reporte resultado com dados completos

---

*Guia de Testes - Versão 1.0*
*Cole scripts exatamente como estão*
*Todos testes são reversíveis/seguros*
