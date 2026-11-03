# Compliance, Quality Assurance & Tests — Sprint 4

Projeto: [DOBU no Azure Boards](https://dev.azure.com/RM561439/DOBU).
Entrega prevista no enunciado: 04/11/2026.

## Parte A — Plano manual

Os casos possuem objetivos, entradas controladas, procedimentos e saídas esperadas no campo Steps, com vínculo Tests ao PBI correspondente. Iteração: `DOBU\Sprint 4`.

| Caso | Funcionalidade | PBI | Cobertura |
|---|---|---|---|
| [66](https://dev.azure.com/RM561439/DOBU/_workitems/edit/66) | Carteira e persistência de vacina | 22 / PBI-10 | Implementado; execução manual pendente |
| [67](https://dev.azure.com/RM561439/DOBU/_workitems/edit/67) | Próximas doses e limites de data | 23 / PBI-11 | Consulta implementada; alerta automático pendente |
| [68](https://dev.azure.com/RM561439/DOBU/_workitems/edit/68) | Alteração de data da consulta | 25 / PBI-13 | Atualização implementada; conflito pendente |
| [69](https://dev.azure.com/RM561439/DOBU/_workitems/edit/69) | Cadastro de lembrete | 29 / PBI-16 | Cadastro implementado; WhatsApp pendente |
| [70](https://dev.azure.com/RM561439/DOBU/_workitems/edit/70) | Vacina para pet inexistente | 22 | Cenário negativo |
| [71](https://dev.azure.com/RM561439/DOBU/_workitems/edit/71) | Descrição inválida no lembrete | 29 | Cenário negativo |
| [72](https://dev.azure.com/RM561439/DOBU/_workitems/edit/72) | Carteira de outro tutor | 22 | Lacuna de autorização por inspeção |
| [73](https://dev.azure.com/RM561439/DOBU/_workitems/edit/73) | Alerta automático | 23 | Bloqueado por implementação ausente |
| [74](https://dev.azure.com/RM561439/DOBU/_workitems/edit/74) | Horário ocupado | 25 | Lacuna de conflito por inspeção |
| [75](https://dev.azure.com/RM561439/DOBU/_workitems/edit/75) | WhatsApp e consentimento | 29 | Bloqueado por implementação ausente |

As datas de outubro são fixtures fixas; não representam a data atual. `C-100` é uma descrição; os IDs reais são UUIDs capturados das respostas. Preparar os cadastros auxiliares da collection antes dos casos. Usar banco QA isolado. Ao repetir, recriar as fixtures. Nunca usar telefone real para ensaio de envio.

Registrar por caso: versão/commit, ambiente, executor, data, saída real, resultado e evidência. Os casos estão em Design; nenhum resultado manual foi declarado aprovado sem execução. Os critérios completos dos PBIs continuam válidos, inclusive os ainda não implementados. É necessário resolver ou justificar as lacunas antes da entrega final.

## Parte B — Automação

Collection: `DOBU/postman/Sprint4-automacao.postman_collection.json`.
Testes HTTP de integração: `DOBU/tests/Dobu.IntegrationTests/Sprint4ComplianceTests.cs`.

Quatro testes independentes verificam carteira/persistência, filtro de próximas doses, alteração/persistência da consulta e cadastro/persistência do lembrete. Usam a API real em WebApplicationFactory e banco SQLite temporário; não validam Oracle, entrega de notificações, WhatsApp ou concorrência de agenda.

```powershell
dotnet test DOBU/tests/Dobu.IntegrationTests/Dobu.IntegrationTests.csproj --filter FullyQualifiedName~Sprint4ComplianceTests --logger "trx;LogFileName=compliance.trx" --results-directory TestResults/Compliance
```

Para Postman: iniciar a API em Development, com SQLite isolado e chave JWT de desenvolvimento; configurar `baseUrl` (padrão `http://localhost:5070`) e executar a collection inteira no Runner, incluindo a pasta de preparação. Limpar `qaEmail` e `qaVetEmail` antes de cada execução para gerar novos usuários. Não expor tokens na gravação. A collection foi preparada; seu resultado deve ser registrado após executar o Runner.

## Roteiro do vídeo obrigatório

1. Mostrar a branch develop e o commit da versão testada.
2. Mostrar os quatro casos principais no Boards, entradas, saídas e vínculo aos PBIs.
3. Mostrar a configuração do ambiente, banco isolado e ferramenta de automação.
4. Executar os quatro testes com o comando acima; expandir os quatro nomes e mostrar os resultados. Se usar Postman, executar a collection e mostrar as verificações de persistência.
5. Mostrar o relatório e explicar o alcance dos testes e as lacunas. Não apresentar cadastro de lembrete como envio WhatsApp.
6. Publicar o vídeo e inserir o link no repositório público, na branch develop.

## Conferência final

- [x] Plano manual publicado no Azure Boards com dez casos e rastreabilidade.
- [x] Quatro cenários automatizados adicionados à cópia local da branch develop.
- [x] Quatro cenários executados: 4 aprovados, 0 falhas, 0 ignorados em 03/11/2026. Suíte completa: 26 aprovados (7 unitários + 19 integração).
- [ ] Registrar execuções manuais e evidências.
- [ ] Resolver ou justificar lacunas de cobertura dos critérios de aceite.
- [x] Arquivos revisados publicados na [branch develop do repositório público](https://github.com/nanabardella/Dobu-.NET/tree/develop); implementação e relatório no commit `a6e2e96`.
- [ ] Gravar e publicar vídeo de configuração, execução e resultados.
- [ ] Inserir link do vídeo: **PENDENTE**.
- [x] Professor Orlando Clinio Patriarcha (`pf1997@fiap.com.br`, e-mail `proforlando.patriarcha@fiap.com.br`) confirmado com Basic (`express` no CLI) e membro de Project Administrators no DOBU.

Este documento substitui as afirmações do PDF antigo sobre resultados e WhatsApp: esses resultados não foram assumidos como evidência desta execução.
