# Compliance, Quality Assurance & Tests — Sprint 4

**Projeto:** [DOBU no Azure Boards](https://dev.azure.com/RM561439/DOBU). **Entrega prevista:** 04/11/2026, no horário definido no portal.

## Links para correção

- [Repositório público — branch develop](https://github.com/nanabardella/Dobu-.NET/tree/develop).
- [Vídeo legendado em MP4 — abrir ou baixar](https://raw.githubusercontent.com/nanabardella/Dobu-.NET/develop/docs/compliance-sprint4/evidencias/sprint4-execucao-ao-vivo.mp4).
- [Arquivo do vídeo no GitHub](evidencias/sprint4-execucao-ao-vivo.mp4).
- [Plano de testes no Azure Boards](https://dev.azure.com/RM561439/DOBU/_workitems).
- [Relatório navegável das chamadas reais](evidencias/relatorio.html) — baixar e abrir no navegador; o GitHub exibe o código HTML.
- [Resultados do replay HTTP](evidencias/http/resumo.json) e [requisições/respostas completas](evidencias/http/requisicoes.json).

O vídeo principal é uma **captura contínua de janela durante execução real**, com legendas e sem narração. Mostra a branch, a revisão de base, a versão .NET, os comandos executados, nove testes de integração aprovados e as 33 chamadas HTTP, com entradas, respostas e verificações aparecendo enquanto a API executa. Usa uma janela dedicada ao roteiro de automação HTTP equivalente; não é Postman. Tokens e senhas são ocultados antes de aparecer. O vídeo anterior de apresentação dos registros foi mantido como material complementar, mas não é mais o link principal.

## Parte A — Plano de sistema no Azure Boards

Os dez casos possuem objetivo, entradas controladas, passos com saídas esperadas e vínculo Tests ao PBI. Iteração: `DOBU\Sprint 4`. Os casos 66 a 74 têm anexos JSON e PNG derivados das respostas reais, além do histórico da execução assistida. A revisão humana/manual permanece pendente; não foram declaradas execuções humanas inexistentes.

| Caso | Funcionalidade | PBI | Resultado da execução assistida |
|---|---|---|---|
| [66](https://dev.azure.com/RM561439/DOBU/_workitems/edit/66) | Carteira e persistência de vacina | 22 / PBI-10 | PASS |
| [67](https://dev.azure.com/RM561439/DOBU/_workitems/edit/67) | Próximas doses, limite exato e exclusão | 23 / PBI-11 | PASS |
| [68](https://dev.azure.com/RM561439/DOBU/_workitems/edit/68) | Reagendamento e persistência | 25 / PBI-13 | PASS |
| [69](https://dev.azure.com/RM561439/DOBU/_workitems/edit/69) | Cadastro e consulta de lembrete | 29 / PBI-16 | PASS; cobertura parcial de WhatsApp |
| [70](https://dev.azure.com/RM561439/DOBU/_workitems/edit/70) | Vacina para pet inexistente | 22 | PASS — HTTP 400 |
| [71](https://dev.azure.com/RM561439/DOBU/_workitems/edit/71) | Descrição inválida de lembrete | 29 | PASS — HTTP 400 |
| [72](https://dev.azure.com/RM561439/DOBU/_workitems/edit/72) | Acesso por outro tutor ou anônimo | 22 | PASS — HTTP 404/401 |
| [73](https://dev.azure.com/RM561439/DOBU/_workitems/edit/73) | Alertas internos, repetição e isolamento | 23 | PASS; worker validado separadamente |
| [74](https://dev.azure.com/RM561439/DOBU/_workitems/edit/74) | Reagendamento em horário ocupado | 25 | PASS — HTTP 409 e data preservada |
| [75](https://dev.azure.com/RM561439/DOBU/_workitems/edit/75) | WhatsApp e consentimento | 29 | BLOCKED — sem implementação/provedor |

As datas de outubro são fixtures fixas em UTC; não representam a data atual. `C-100` é uma descrição; os IDs reais são UUIDs capturados das respostas. Executar a preparação e os casos em ordem: a C-100 termina o caso 68 em 24/10/2026 às 10:00; no caso 74, a C-200 ocupa 25/10/2026 às 10:00. O roteiro usa banco novo a cada execução.

O enunciado exige um **plano de testes manuais** no Azure Boards, com objetivos, entradas, saídas e procedimentos. Não exige expressamente executar todos os casos manualmente. O plano está cadastrado. A revisão humana adicional é opcional para esta conferência de requisitos; as evidências publicadas são assistidas e não devem ser apresentadas como um teste feito por uma pessoa.

## Parte B — Automação e reprodução

Validação em 03/11/2026: **31 testes aprovados — 7 unitários e 24 de integração, zero falhas**. A classe `Sprint4ComplianceTests` contém nove testes, incluindo os quatro fluxos principais e cenários complementares. O replay externo realizou **33 chamadas HTTP verificadas**, cobrindo nove casos disponíveis. Relatórios TRX completos na pasta [evidencias](evidencias).

Requisitos: .NET 9 SDK e Python 3. Executar na raiz do repositório:

```powershell
dotnet test DOBU/Dobu.sln -m:1 --logger "trx;LogFilePrefix=sprint4" --results-directory TestResults/Validacao
python DOBU/postman/executar-evidencias.py
```

O segundo comando inicia a API compilada na porta local 5077 em Development, configura SQLite temporário isolado, executa os casos e encerra a API. Não iniciar outra instância nessa porta. É necessário compilar primeiro com `dotnet test` ou `dotnet build DOBU/Dobu.sln -m:1`. Para executar somente os testes específicos:

```powershell
dotnet test DOBU/tests/Dobu.IntegrationTests/Dobu.IntegrationTests.csproj --filter FullyQualifiedName~Sprint4ComplianceTests
```

O roteiro é uma ferramenta equivalente de automação HTTP, com verificações de status, corpo, vínculos e consultas posteriores de persistência. Também existe a collection `DOBU/postman/Sprint4-automacao.postman_collection.json`; ela herda autenticação Bearer do token do tutor. Para Postman, iniciar a API, configurar `baseUrl`, limpar `qaEmail`/`qaVetEmail` antes de uma nova execução e executar a collection inteira com preparação. O Runner do Postman não foi executado nesta validação; o vídeo mostra o roteiro HTTP equivalente.

## Mudanças da aplicação

- Carteira de vacinação e lembretes exigem autenticação e restringem dados ao tutor responsável, inclusive por ID, pet, listagem e operações de escrita.
- Criação e reagendamento de consulta recusam o mesmo instante para o mesmo veterinário, retornando HTTP 409 e preservando a consulta anterior.
- Worker consulta vacinas a cada minuto e gera lembretes internos PENDENTE do dia atual até sete dias à frente, inclusive, em UTC. Janela configurável em `VaccineAlerts:WindowDays`; desligar com `VaccineAlerts:Enabled=false`.
- Para QA, `POST /api/alertas-vacina/gerar?referencia=2026-10-19T10:00:00Z` chama a mesma rotina em Development, somente para o tutor autenticado. Em Production esse hook retorna 404. `GET /api/alertas-vacina` consulta os lembretes de vacina do próprio tutor.

O replay desativa o worker para manter a fixture determinística e usa o hook Development. O teste `Alertas_RotinaEmSegundoPlano_GeraSemRequisicaoManual` inicia o worker real com TimeProvider fixo e verifica criação sem requisição de geração. A implementação segue o [uso de escopo em BackgroundService documentado pela Microsoft](https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service).

## Limites e pendências

- **WhatsApp bloqueado:** a equipe informou não possuir conta de teste. Não há integração de envio nem gestão de consentimento; nenhuma mensagem foi enviada. Cadastro de lembrete não comprova envio. O critério do PBI-16 permanece pendente e pode reduzir a avaliação.
- Resultados publicados são de execução assistida/automatizada. Não foi realizada execução manual humana; ela não está expressamente exigida como entregável no enunciado fornecido.
- Ensaios usam SQLite. Oracle não foi validado nesta rodada. Não houve deploy em produção.
- Conflito cobre o mesmo instante, sem duração de consulta ou teste de corrida entre processos; a checagem não garante exclusão em concorrência distribuída.
- Deduplicação de alertas vale para mesma descrição, pet e data na rotina sequencial. Atualizar vacina não invalida automaticamente lembretes já emitidos; múltiplas instâncias do worker exigem coordenação adicional. Esta versão acadêmica não é uma validação de produção.

## Entrega

Professor Orlando Clinio Patriarcha (`pf1997@fiap.com.br`, e-mail `proforlando.patriarcha@fiap.com.br`) confirmado com Basic e membro de Project Administrators do DOBU. Repositório público, arquivos na branch develop.

Entregar o link desta branch no portal da atividade. Este README contém os links do Azure Boards e do vídeo; o enunciado de Compliance não exige PDF nem uma plataforma específica para o vídeo. Para apresentar no YouTube, a equipe pode baixar o MP4, enviá-lo como não listado e atualizar o link. O envio no portal acadêmico não foi realizado.

## Gravação ao vivo

A nova gravação executou novamente nove testes de integração (9 aprovados, zero falhas) e 33 chamadas HTTP verificadas. [Transcrição da execução](evidencias/ao-vivo/runner-completo.txt), [relatório TRX](evidencias/ao-vivo/testes/filmagem.trx) e [JSON desta rodada](evidencias/ao-vivo/http/resumo.json). O vídeo grava os quadros da janela gráfica do executor enquanto os comandos rodam; não reconstrói a execução a partir de logs anteriores. Os quadros da janela são enviados ao FFmpeg durante a execução, com legendas incorporadas. A captura intermediária MKV é mantida localmente; o MP4 preserva a sequência, com recompressão e sem cortes.

Para reproduzir no Windows: `DOBU/postman/gravar-ao-vivo.ps1 -EncoderPath <caminho-do-ffmpeg.exe> -PythonPath <caminho-do-python.exe>`. Necessita FFmpeg, Python 3, .NET 9 e dependências restauradas. O script executa `execucao-filmada.py`, que lança os comandos reais e alimenta a janela com suas saídas. Não é uma captura do Swagger ou do Postman.
