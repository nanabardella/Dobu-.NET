"""Runs actual commands for the live recording window; does not replay stored results."""
from pathlib import Path
import os
import subprocess
import sys
import time

root=Path(__file__).resolve().parents[2]
os.chdir(root)
transcript=root/'docs/compliance-sprint4/evidencias/ao-vivo/runner-completo.txt'
transcript.parent.mkdir(parents=True,exist_ok=True)
class Transcript:
    def __init__(self,original):
        self.original=original
        self.file=transcript.open('w',encoding='utf-8')
    def write(self,text):
        self.file.write(text); self.file.flush()
        self.original.write(text); self.original.flush()
    def flush(self): self.file.flush(); self.original.flush()
sys.stdout=Transcript(sys.stdout)
sys.stderr=sys.stdout
def say(text): print(text,flush=True)
def run(command):
    say('\n$ '+' '.join(command))
    process=subprocess.Popen(command,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
        text=True,encoding='utf-8',errors='replace',creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    for line in process.stdout: print(line,end='',flush=True)
    code=process.wait()
    say(f'\nCodigo de saida: {code}')
    if code: raise SystemExit(code)

say('@@CAPTION|Preparacao: vamos conferir a versao e executar os testes reais desta branch.')
say('DOBU - COMPLIANCE SPRINT 4 - EXECUCAO REAL GRAVADA')
say('Ferramentas: .NET 9 / xUnit e roteiro de automacao HTTP Python.')
say('Ambiente: Development; SQLite temporario isolado; sem dados de producao.')
say('Este processo executa os comandos agora; nao reproduz resultados anteriores.')
run(['git','branch','--show-current'])
run(['git','rev-parse','--short','HEAD'])
run(['dotnet','--version'])
say('Configuracao: fixtures Thor/V10, datas fixas de outubro de 2026 em UTC.')
say('A API sera iniciada na porta 5077 e encerrada automaticamente no final.')
say('Tokens e senhas sao ocultados pelo roteiro antes de aparecerem na janela.')
time.sleep(10)
say('@@CAPTION|Automacao de integracao: os testes executam agora com banco temporario.')
run(['dotnet','test','DOBU/tests/Dobu.IntegrationTests/Dobu.IntegrationTests.csproj',
     '--no-restore','-m:1','--filter','FullyQualifiedName~Sprint4ComplianceTests',
     '--logger','trx;LogFileName=filmagem.trx','--results-directory',
     'docs/compliance-sprint4/evidencias/ao-vivo/testes','--verbosity','minimal',
     '--logger','console;verbosity=minimal'])
time.sleep(8)
say('@@CAPTION|Teste de sistema por HTTP: vamos acompanhar preparacao, entradas e respostas reais.')
run([sys.executable,'-u','DOBU/postman/executar-evidencias.py','--live','--output',
     'docs/compliance-sprint4/evidencias/ao-vivo/http'])
say('@@CAPTION|Execucao encerrada: confira o resumo real abaixo e os arquivos de evidencia.')
say('\nCONCLUSAO DA EXECUCAO')
say('Nove testes de integracao especificos passaram se o comando acima terminou com sucesso.')
say('As 33 requisicoes HTTP passaram se o roteiro terminou com codigo zero.')
say('Fluxos principais: carteira de vacina, proximas doses, reagendamento e lembrete.')
say('Casos complementares: dados invalidos, isolamento, alertas internos e conflito.')
say('WhatsApp continua bloqueado: nao ha integracao/provedor e nenhuma mensagem foi enviada.')
say('Ensaios realizados em SQLite; nao validam Oracle ou implantacao de producao.')
say('O video mostra execucao automatizada ao vivo, sem narracao, com legendas.')
time.sleep(15)
