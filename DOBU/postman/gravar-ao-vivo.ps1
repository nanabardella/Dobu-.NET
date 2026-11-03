param([Parameter(Mandatory=$true)][string]$EncoderPath,
      [Parameter(Mandatory=$true)][string]$PythonPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Text;
public static class LiveQaProcess {
 public static readonly ConcurrentQueue<string> Lines = new ConcurrentQueue<string>();
 public static Process Start(string python, string script, string cwd) {
  var info = new ProcessStartInfo(python, "-u \""+script+"\"");
  info.WorkingDirectory=cwd; info.UseShellExecute=false;
  info.CreateNoWindow=true; info.WindowStyle=ProcessWindowStyle.Hidden;
  info.RedirectStandardOutput=true; info.RedirectStandardError=true;
  info.StandardOutputEncoding=Encoding.UTF8; info.StandardErrorEncoding=Encoding.UTF8;
  info.EnvironmentVariables["PYTHONIOENCODING"]="utf-8";
  info.EnvironmentVariables["DOTNET_CLI_UI_LANGUAGE"]="pt-BR";
  var p=new Process(); p.StartInfo=info;
  p.OutputDataReceived+=(s,e)=>{if(e.Data!=null)Lines.Enqueue(e.Data);};
  p.ErrorDataReceived+=(s,e)=>{if(e.Data!=null)Lines.Enqueue(e.Data);};
  p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); return p;
 }
}
'@
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output=Join-Path $repo 'docs\compliance-sprint4\evidencias\ao-vivo'
New-Item -ItemType Directory -Force $output | Out-Null
$form=[Windows.Forms.Form]::new()
$form.Text='DOBU QA Sprint4 - execucao real'
$form.Size=[Drawing.Size]::new(1240,720)
$form.StartPosition='CenterScreen'
$form.BackColor=[Drawing.Color]::FromArgb(10,19,32)
$form.TopMost=$true
$form.MaximizeBox=$false
$form.FormBorderStyle='FixedSingle'
$title=[Windows.Forms.Label]::new()
$title.Text='DOBU / SPRINT 4 / AUTOMACAO AO VIVO'
$title.SetBounds(20,12,1150,36)
$title.Font=[Drawing.Font]::new('Segoe UI',20,[Drawing.FontStyle]::Bold)
$title.ForeColor=[Drawing.Color]::FromArgb(104,228,181)
$form.Controls.Add($title)
$clock=[Windows.Forms.Label]::new()
$clock.SetBounds(20,50,1160,27)
$clock.ForeColor=[Drawing.Color]::LightGray
$clock.Font=[Drawing.Font]::new('Consolas',12)
$form.Controls.Add($clock)
$console=[Windows.Forms.RichTextBox]::new()
$console.SetBounds(20,84,1180,485)
$console.BackColor=[Drawing.Color]::FromArgb(19,32,49)
$console.ForeColor=[Drawing.Color]::FromArgb(235,241,250)
$console.Font=[Drawing.Font]::new('Consolas',11)
$console.ReadOnly=$true
$console.WordWrap=$true
$form.Controls.Add($console)
$caption=[Windows.Forms.Label]::new()
$caption.SetBounds(22,581,1170,88)
$caption.ForeColor=[Drawing.Color]::White
$caption.Font=[Drawing.Font]::new('Segoe UI',18,[Drawing.FontStyle]::Bold)
$caption.Text='Preparando a gravacao da janela e a execucao real dos testes.'
$form.Controls.Add($caption)
$screen=[Windows.Forms.PictureBox]::new()
$screen.Dock='Fill'
$screen.SizeMode='Zoom'
$form.Controls.Add($screen)
$screen.BringToFront()
$script:liveScreen=$screen
$script:liveForm=$form
$script:liveConsole=$console
$script:liveCaption=$caption
$script:liveClock=$clock
$captions=@{
 'PREP'='Preparacao real: contas de teste, especie, raca e pet Thor. Os IDs retornados alimentam os casos.'
 '66'='Caso 66: registrar V10 e consultar novamente para verificar a persistencia na carteira de Thor.'
 '67'='Caso 67: validar inclusao no limite da proxima dose e exclusao apos a data informada.'
 '68'='Caso 68: reagendar a consulta e confirmar que data e vinculos foram persistidos.'
 '69'='Caso 69: cadastrar e consultar o lembrete PENDENTE. Este cadastro nao envia WhatsApp.'
 '70'='Caso 70: recusar vacina para um pet inexistente com HTTP 400.'
 '71'='Caso 71: recusar descricao invalida e verificar que ela nao foi persistida.'
 '72'='Caso 72: impedir acesso de outro tutor e bloquear acesso sem autenticacao.'
 '73'='Caso 73: gerar alertas internos com relogio controlado, sem duplicar nem expor outro tutor.'
 '74'='Caso 74: recusar horario ocupado com HTTP 409 e preservar a consulta anterior.'
}
$script:runner=$null
$script:recorder=$null
$script:done=$false
$script:started=[DateTime]::UtcNow
$script:exitCode=-1
$script:log=[Collections.Generic.List[string]]::new()
$script:subtitle=[Collections.Generic.List[object]]::new()
$timer=[Windows.Forms.Timer]::new()
$timer.Interval=250
function Write-LiveFrame {
 $bitmap=[Drawing.Bitmap]::new(1280,720)
 $graphics=[Drawing.Graphics]::FromImage($bitmap)
 $fontTitle=[Drawing.Font]::new('Segoe UI',24,[Drawing.FontStyle]::Bold)
 $fontBody=[Drawing.Font]::new('Consolas',14)
 $fontSmall=[Drawing.Font]::new('Consolas',12)
 $fontCaption=[Drawing.Font]::new('Segoe UI',18,[Drawing.FontStyle]::Bold)
 try {
  $graphics.Clear([Drawing.Color]::FromArgb(10,19,32))
  $graphics.TextRenderingHint='AntiAliasGridFit'
  $graphics.DrawString('DOBU / SPRINT 4 / AUTOMACAO AO VIVO',$fontTitle,[Drawing.Brushes]::Aquamarine,24,12)
  $graphics.DrawString($script:liveClock.Text,$fontSmall,[Drawing.Brushes]::LightGray,24,57)
  $graphics.FillRectangle([Drawing.Brushes]::MidnightBlue,20,90,1240,490)
  $wrapped=[Collections.Generic.List[string]]::new()
  foreach($row in ($script:liveConsole.Text -split "`n")) {
   $row=$row.TrimEnd("`r")
   while($row.Length -gt 100){ $wrapped.Add($row.Substring(0,100)); $row=$row.Substring(100) }
   $wrapped.Add($row)
  }
  $visible=($wrapped | Select-Object -Last 21) -join "`n"
  $graphics.DrawString($visible,$fontBody,[Drawing.Brushes]::White,[Drawing.RectangleF]::new(30,98,1220,475))
  $graphics.DrawString($script:liveCaption.Text,$fontCaption,[Drawing.Brushes]::White,[Drawing.RectangleF]::new(24,598,1220,104))
  $memory=[IO.MemoryStream]::new()
  try {
   $bitmap.Save($memory,[Drawing.Imaging.ImageFormat]::Jpeg)
   $bytes=$memory.ToArray()
   $script:recorder.StandardInput.BaseStream.Write($bytes,0,$bytes.Length)
   $script:recorder.StandardInput.BaseStream.Flush()
  } finally { $memory.Dispose() }
  $oldImage=$script:liveScreen.Image
  $script:liveScreen.Image=$bitmap
  if($oldImage){$oldImage.Dispose()}
  $script:liveScreen.Refresh()
 } finally {
  $graphics.Dispose(); $fontTitle.Dispose(); $fontBody.Dispose(); $fontSmall.Dispose(); $fontCaption.Dispose()
 }
}
$timer.Add_Tick({
 $script:liveClock.Text='Execucao real | '+[DateTime]::Now.ToString('dd/MM/yyyy HH:mm:ss')+' | Sem narracao | Tokens e senhas ocultados'
 $line=$null
 $count=0
 while ($count -lt 80 -and [LiveQaProcess]::Lines.TryDequeue([ref]$line)) {
  $script:log.Add($line)
  if($line.StartsWith('@@CAPTION|')) {
   $script:liveCaption.Text=$line.Substring(10)
   $script:subtitle.Add(@{seconds=([DateTime]::UtcNow-$script:started).TotalSeconds;text=$script:liveCaption.Text})
  } elseif($line.StartsWith('@@STEP|')) {
   $part=$line.Split('|')
   $script:liveCaption.Text=$captions[$part[1]]
   $script:subtitle.Add(@{seconds=([DateTime]::UtcNow-$script:started).TotalSeconds;text=$script:liveCaption.Text})
   $script:liveConsole.Clear()
  } else {
   $script:liveConsole.AppendText($line+"`r`n")
   $script:liveConsole.SelectionStart=$script:liveConsole.TextLength
   $script:liveConsole.ScrollToCaret()
  }
  $count++
 }
 $script:liveConsole.Refresh()
 $script:liveCaption.Refresh()
 $script:liveClock.Refresh()
 $script:liveForm.Refresh()
 Write-LiveFrame
 if($script:runner -and $script:runner.HasExited -and [LiveQaProcess]::Lines.IsEmpty -and -not $script:done) {
  $script:exitCode=$script:runner.ExitCode
  $script:done=$true
  $timer.Stop()
  if($script:recorder -and -not $script:recorder.HasExited) {
   $script:recorder.StandardInput.Close()
   if(-not $script:recorder.WaitForExit(15000)){ $script:recorder.Kill() }
  }
  $form.Close()
 }
})
$form.Add_Shown({
 try {
 $form.Activate()
 [Windows.Forms.Application]::DoEvents()
 $capture=Join-Path $output 'captura-original.mkv'
 $info=[Diagnostics.ProcessStartInfo]::new()
 $info.FileName=$EncoderPath
 $info.Arguments='-hide_banner -nostats -loglevel error -y -use_wallclock_as_timestamps 1 -f image2pipe -framerate 4 -vcodec mjpeg -i pipe:0 -an -vf format=yuv420p -fps_mode vfr -c:v libx264 -preset ultrafast -crf 24 "'+$capture+'"'
 $info.UseShellExecute=$false
 $info.CreateNoWindow=$true
 $info.WindowStyle='Hidden'
 $info.RedirectStandardInput=$true
 $info.RedirectStandardError=$true
 $script:recorder=[Diagnostics.Process]::new()
 $script:recorder.StartInfo=$info
 $script:recorder.Start() | Out-Null
 Start-Sleep -Milliseconds 1200
 if($script:recorder.HasExited) { throw ('Falha na captura: '+$script:recorder.StandardError.ReadToEnd()) }
 $script:started=[DateTime]::UtcNow
 $script:runner=[LiveQaProcess]::Start($PythonPath,(Join-Path $PSScriptRoot 'execucao-filmada.py'),$repo)
 $timer.Start()
 } catch { $script:log.Add($_.ToString()); $script:done=$true; $form.Close() }
})
$form.Add_FormClosing({
 if(-not $script:done) {
  if($script:runner -and -not $script:runner.HasExited){ $script:runner.Kill() }
  if($script:recorder -and -not $script:recorder.HasExited){ $script:recorder.StandardInput.Close(); $script:recorder.WaitForExit(10000) | Out-Null }
 }
})
[Windows.Forms.Application]::Run($form)
[IO.File]::WriteAllLines((Join-Path $output 'execucao-console.txt'),$script:log,[Text.UTF8Encoding]::new($false))
$script:subtitle | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $output 'legendas-tempos.json') -Encoding UTF8
if($script:recorder){ $script:recorder.StandardError.ReadToEnd() | Set-Content (Join-Path $output 'captura-log.txt') -Encoding UTF8 }
if($script:exitCode -ne 0){ throw ('A execucao real falhou ou foi interrompida. Codigo: '+$script:exitCode+'. Confira runner-completo.txt.') }
$final=Join-Path $repo 'docs\compliance-sprint4\evidencias\sprint4-execucao-ao-vivo.mp4'
& $EncoderPath -hide_banner -loglevel error -y -i (Join-Path $output 'captura-original.mkv') -c:v libx264 -preset fast -crf 23 -pix_fmt yuv420p -movflags +faststart $final
if($LASTEXITCODE){ throw 'Falha ao converter a captura para MP4' }
Get-Item $final | Select-Object FullName,Length
