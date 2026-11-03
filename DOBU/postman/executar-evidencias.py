"""Replay the system-test steps against a real local API; store redacted HTTP evidence.
Run from the repository root with Python 3 and .NET 9 installed.
"""
import datetime as dt
import json
import os
from pathlib import Path
import subprocess
import time
import urllib.request
import urllib.error
import argparse

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--live', action='store_true', help='Mostra preparacao e respostas em tempo real, com pausas de leitura.')
parser.add_argument('--output', help='Diretorio de evidencias, relativo a raiz do repositorio.')
args = parser.parse_args()

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / (args.output or 'docs/compliance-sprint4/evidencias/http')
OUT.mkdir(parents=True, exist_ok=True)
API = ROOT / 'DOBU/Dobu.Api'
RUN = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%SZ')
SCRATCH = ROOT / 'TestResults' / ('http-' + RUN)
SCRATCH.mkdir(parents=True, exist_ok=True)
BASE = 'http://127.0.0.1:5077'
token = ''
events = []
cases = {}

def redact(value):
    if isinstance(value, dict):
        return {key: '[REDACTED]' if key.lower() in ('token', 'senha', 'password') else redact(item) for key,item in value.items()}
    if isinstance(value, list): return [redact(item) for item in value]
    return value

def call(case, label, method, path, data=None, expected=200, check=None):
    if args.live:
        print(f'@@STEP|{case}|{label}', flush=True)
        print(f'\n>>> {method} {path}', flush=True)
        if data is not None:
            print('ENTRADA: '+json.dumps(redact(data),ensure_ascii=False),flush=True)
        print(f'Esperado: HTTP {expected}. Executando requisicao...',flush=True)
        time.sleep(.6)
    body = json.dumps(data).encode() if data is not None else None
    headers = {'Content-Type': 'application/json'}
    if token: headers['Authorization'] = 'Bearer '+token
    request = urllib.request.Request(BASE+path, body, headers, method=method)
    start = time.perf_counter()
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            status, raw = response.status, response.read().decode()
    except urllib.error.HTTPError as error:
        status, raw = error.code, error.read().decode()
    try: result = json.loads(raw)
    except ValueError: result = raw
    passed = status == expected and (check(result) if check and status == expected else True)
    event = {'case':case,'label':label,'timestamp':dt.datetime.now(dt.timezone.utc).isoformat(),
             'method':method,'url':path,'request':redact(data),'status':status,
             'expectedStatus':expected,'response':redact(result),'passed':bool(passed),
             'durationMs':round((time.perf_counter()-start)*1000)}
    events.append(event)
    if args.live:
        output=json.dumps(redact(result),ensure_ascii=False,indent=2)
        print('RESPOSTA REAL: HTTP '+str(status)+'\n'+(output[:1600]+'\n[trecho; resposta completa no JSON]' if len(output)>1600 else output),flush=True)
    print(f'{"PASS" if passed else "FAIL"} | {case} | {label} | {method} {path} -> {status}', flush=True)
    (OUT/'requisicoes.json').write_text(json.dumps(events,ensure_ascii=False,indent=2),encoding='utf-8')
    if not passed: raise AssertionError(label)
    if args.live: time.sleep(3)
    return result

env = os.environ.copy()
env.update({'ASPNETCORE_ENVIRONMENT':'Development','ASPNETCORE_URLS':BASE,
    'Database__Provider':'Sqlite','ConnectionStrings__DobuSqlite':'Data Source='+str(SCRATCH/'qa.db'),
    'VaccineAlerts__Enabled':'false'})
log = (SCRATCH/'api.log').open('w',encoding='utf-8')
process = subprocess.Popen(['dotnet',str(API/'bin/Debug/net9.0/Dobu.Api.dll')],cwd=API,env=env,
    stdout=log,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
try:
    for attempt in range(100):
        if process.poll() is not None: raise RuntimeError('API terminou: '+str(SCRATCH/'api.log'))
        try:
            with urllib.request.urlopen(BASE+'/health',timeout=1): break
        except (OSError,urllib.error.URLError): time.sleep(.2)
    else: raise RuntimeError('API indisponivel')
    auth = call('PREP','Cadastrar tutor QA','POST','/api/auth/register',{'nome':'Tutor QA','email':f'tutor-{RUN}@example.test','senha':'Senha123','tipoUsuario':'Responsavel'})
    token = auth['token']
    owner = auth['usuarioId']
    vet = call('PREP','Cadastrar veterinario QA','POST','/api/auth/register',{'nome':'Veterinario QA','email':f'vet-{RUN}@example.test','senha':'Senha123','tipoUsuario':'Veterinario'})['usuarioId']
    species = call('PREP','Criar especie','POST','/api/especies',{'nome':'Cachorro QA','descricao':'Animal domestico'},201)['id']
    breed = call('PREP','Criar raca','POST','/api/racas',{'nome':'Vira-lata QA','porte':'Medio','expectativaVida':12,'descricao':'Raca de teste','cuidados':'Banho e passeio','especieId':species},201)['id']
    pet = call('PREP','Criar Thor','POST','/api/pets',{'nome':'Thor','idade':4,'racaId':breed,'responsavelId':owner},201)['id']
    vaccine = {'nome':'V10','dataAplicacao':'2026-10-19T10:00:00Z','dataProximaDose':'2026-10-25T10:00:00Z','petId':pet}
    vid = call(66,'Registrar V10','POST','/api/vacinas',vaccine,201)['id']
    call(66,'Confirmar persistencia','GET','/api/vacinas/'+vid,check=lambda x:x['nome']=='V10' and x['petId']==pet and x['dataProximaDose'].startswith('2026-10-25T10:00:00'))
    call(66,'Consultar carteira de Thor','GET','/api/vacinas/pet/'+pet,check=lambda x:any(i['id']==vid for i in x))
    control = call(67,'Preparar vacina fora do limite','POST','/api/vacinas',dict(vaccine,nome='Controle QA',dataProximaDose='2026-11-15T10:00:00Z'),201)['id']
    call(67,'Aplicar limite controlado','GET','/api/vacinas/proxima-dose?ate=2026-10-30T23:59:59Z',check=lambda x:any(i['id']==vid for i in x) and not any(i['id']==control for i in x))
    call(67,'Incluir exatamente no limite','GET','/api/vacinas/proxima-dose?ate=2026-10-25T10:00:00Z',check=lambda x:any(i['id']==vid for i in x))
    call(67,'Excluir um segundo antes','GET','/api/vacinas/proxima-dose?ate=2026-10-25T09:59:59Z',check=lambda x:not any(i['id']==vid for i in x))
    consultation={'dataConsulta':'2026-10-22T14:00:00Z','descricao':'C-100 consulta anual','valor':150,'petId':pet,'veterinarioId':vet}
    cid=call(68,'Criar C-100','POST','/api/consultas',consultation,201)['id']
    changed=dict(consultation,dataConsulta='2026-10-24T10:00:00Z',descricao='C-100 reagendada')
    call(68,'Reagendar','PUT','/api/consultas/'+cid,changed)
    call(68,'Confirmar nova data persistida','GET','/api/consultas/'+cid,check=lambda x:x['dataConsulta'].startswith('2026-10-24T10:00:00') and x['petId']==pet and x['veterinarioId']==vet and x['valor']==150)
    reminder={'descricao':'Lembrete controlado QA','dataLembrete':'2026-10-21T14:00:00Z','status':'PENDENTE','petId':pet}
    rid=call(69,'Cadastrar lembrete','POST','/api/lembretes',reminder,201)['id']
    call(69,'Confirmar lembrete persistido','GET','/api/lembretes/'+rid,check=lambda x:x['descricao']==reminder['descricao'] and x['status']=='PENDENTE' and x['petId']==pet)
    call(69,'Consultar lembretes do pet','GET','/api/lembretes/pet/'+pet,check=lambda x:any(i['id']==rid for i in x))
    absent='00000000-0000-0000-0000-000000000001'
    call(70,'Confirmar pet inexistente','GET','/api/pets/'+absent,expected=404)
    call(70,'Recusar vacina sem pet','POST','/api/vacinas',dict(vaccine,petId=absent),400,lambda x:x=='Pet informado nao existe.')
    call(71,'Recusar descricao curta','POST','/api/lembretes',dict(reminder,descricao='abc'),400,lambda x:x=='Descricao do lembrete invalida')
    call(71,'Confirmar ausencia do lembrete invalido','GET','/api/lembretes/pet/'+pet,check=lambda x:not any(i['descricao']=='abc' for i in x))
    other=call('PREP','Cadastrar outro tutor','POST','/api/auth/register',{'nome':'Outro Tutor','email':f'outro-{RUN}@example.test','senha':'Senha123','tipoUsuario':'Responsavel'})
    originalToken=token
    token=other['token']
    call(72,'Bloquear carteira de outro tutor','GET','/api/vacinas/pet/'+pet,expected=404)
    call(72,'Isolar listagem geral','GET','/api/vacinas',check=lambda x:not any(i['id']==vid for i in x))
    token=''
    call(72,'Bloquear acesso anonimo','GET','/api/vacinas/pet/'+pet,expected=401)
    token=originalToken
    call(73,'Gerar alertas no relogio controlado','POST','/api/alertas-vacina/gerar?referencia=2026-10-19T10:00:00Z',check=lambda x:x['criados']==1)
    call(73,'Consultar alerta interno','GET','/api/alertas-vacina',check=lambda x:len(x)==1 and 'V10' in x[0]['descricao'] and '2026-10-25' in x[0]['descricao'] and x[0]['petId']==pet)
    call(73,'Nao duplicar na reexecucao','POST','/api/alertas-vacina/gerar?referencia=2026-10-19T10:00:00Z',check=lambda x:x['criados']==0)
    token=other['token']
    call(73,'Nao expor alerta a outro tutor','GET','/api/alertas-vacina',check=lambda x:x==[])
    token=originalToken
    occupied=dict(consultation,dataConsulta='2026-10-25T10:00:00Z',descricao='C-200 ocupada')
    call(74,'Reservar horario com C-200','POST','/api/consultas',occupied,201)
    call(74,'Recusar reagendamento ocupado','PUT','/api/consultas/'+cid,dict(changed,dataConsulta=occupied['dataConsulta']),409)
    call(74,'Preservar C-100 apos conflito','GET','/api/consultas/'+cid,check=lambda x:x['dataConsulta'].startswith('2026-10-24T10:00:00'))
    for case in range(66,75):
        case_events=[event for event in events if event['case']==case]
        cases[str(case)]={'outcome':'Passed' if all(event['passed'] for event in case_events) else 'Failed','mode':'Execucao assistida HTTP, automatizada pelo roteiro; nao substitui revisao humana','steps':len(case_events)}
    cases['75']={'outcome':'Blocked','reason':'Sem implementacao/provedor de WhatsApp; usuario informou nao possuir conta de teste. Nenhuma mensagem enviada.'}
finally:
    process.terminate()
    try: process.wait(timeout=10)
    except subprocess.TimeoutExpired: process.kill()
    log.close()
    summary={'run':RUN,'baseUrl':BASE,'environment':'Development; SQLite temporario isolado; worker desativado neste replay; rotina automatica validada em teste de integracao separado',
             'mode':'Execucao assistida de passos do plano de sistema via HTTP; sem declaracao de execucao manual humana',
             'cases':cases,'requests':len(events),'passed':sum(event['passed'] for event in events)}
    (OUT/'resumo.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(summary,ensure_ascii=False,indent=2 if args.live else None),flush=True)
