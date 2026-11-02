# Refatoração de legibilidade

## Análise antes das alterações

O código tem testes sobre comportamento relevante, mas a estrutura dificulta localizar as decisões e a ordem dos efeitos. No início desta revisão, `Dashboard.cs` tinha 53 linhas acima de 160 caracteres, com máximo de 1.389. `Painel.cs` tinha 21, com máximo de 1.032. `Core.cs` tinha 20, com máximo de 734. A contagem descreve a concentração visual, não é uma medida isolada de qualidade.

| Área | Problema | Risco na manutenção |
| --- | --- | --- |
| Motor | Contagem, projeção de regras e persistência em métodos com várias tarefas | Alterar um contador ou uma projeção pode afetar regras sobrepostas |
| Alterações | Comparação semanal e dívida de uso misturadas com instalação de regras | Uma extração descuidada pode liberar cota já consumida |
| Interface | Construtores e lambdas acumulam layout, validação e operações de arquivo | Dificulta revisar eventos, seleção e estado dos botões |
| Serviço | Framing, comandos, escrita do hosts e diário de recuperação comprimidos | A ordem entre persistir o diário, escrever hosts e limpar registros é crítica |
| Extensão | Estado assíncrono e renderização concentrados em callbacks | Dificulta conferir reconexão, cache e acesso após falhas |

Não há necessidade de trocar framework, arquitetura de armazenamento ou protocolo para resolver essa etapa. A persistência a cada amostra, o endurecimento do serviço e as demais limitações de segurança continuam documentados separadamente.

## Plano

1. Manter como referência os 202 checks C#, sete cenários da extensão, integrações Native Messaging e testes PowerShell existentes.
2. Separar a implementação em arquivos com responsáveis distintos para evitar edições concorrentes no mesmo arquivo. Motor, interface e comunicação são revisados em paralelo. A integração e a extensão ficam com o responsável principal.
3. Extrair funções privadas por responsabilidade. Preservar APIs públicas, nomes de campos serializados, esquema 8, textos, coordenadas da interface, prazos e ordem dos efeitos.
4. Aplicar indentação convencional, uma instrução por linha e nomes que expliquem as etapas. Evitar novas camadas, dependências e abstrações usadas uma única vez sem benefício de leitura.
5. Compilar todos os executáveis e executar novamente os testes existentes. Conferir a extensão em navegador temporário e revisar o diff, especialmente locks, `await`, limites de tempo e diário de restauração.
6. Registrar o resultado e os limites da validação. Atualizar a documentação técnica e publicar somente código e documentação.

## Critério de conclusão

Os métodos que coordenam operações devem permitir ler a sequência principal sem percorrer detalhes de layout, framing ou cálculo. Os testes existentes devem continuar passando sem enfraquecer as assertivas. A refatoração deve preservar o comportamento observável, inclusive os casos de recuperação e restrições combinadas.

## Implementação

- `Core.cs`: contagem única, consumo por regra, clipping de faixa, projeção de linhas e leitura/gravação do estado têm funções próprias.
- `Changes.cs`: coleta de dívidas, cálculo da disponibilidade, preservação de consumo e aplicação de propostas têm funções próprias.
- Formulários: construção por seção, atualização dos indicadores, seleção de linhas, descrição de modos e comandos de persistência saíram das lambdas extensas.
- Serviço e conector: framing, validação de amostra, dispatch, codificação do hosts e escrita com rollback foram separados. A ordem de salvar o diário antes de retirar linhas e limpar os registros depois da escrita foi mantida.
- Extensão: seleção do domínio ativo, instalação das regras de rede, recebimento de snapshots e renderização têm funções nomeadas. Intervalos e timeouts receberam constantes.
- `.editorconfig`: define indentação e convenções para novas edições. Não exige nova dependência de compilação.

O trabalho foi dividido entre três agentes com arquivos exclusivos. Depois houve revisão cruzada do motor, da interface, do serviço e da extensão. A integração ficou com o responsável principal. A formatação C# foi normalizada com Roslyn, seguida de quebras manuais nas expressões extensas.

| Método coordenador | Instruções antes | Instruções depois |
| --- | ---: | ---: |
| `Engine.Charge` | 43 | 17 |
| `Engine.View` | 20 | 8 |
| `Changes.NeedsWait` | 62 | 13 |
| `Dashboard.Reload` | 87 | 21 |
| `Program.Main` | 27 | 9 |

Contagem por nós `StatementSyntax` do parser C#, incluindo instruções aninhadas e excluindo blocos. A lógica foi distribuída entre funções, não eliminada. A medida mostra a redução do trabalho concentrado nos coordenadores, não uma prova isolada de qualidade.

## Validação

Passaram os 202 checks C#, os sete cenários da extensão, as duas integrações Native Messaging e os testes PowerShell existentes. Os executáveis do painel, conector, serviço e gerador de ícones compilaram. As assertivas existentes foram preservadas. Nos arquivos de teste C# e nos arquivos de arte apenas formatados, a igualdade da sequência de tokens também foi conferida com Roslyn. O tema teve depois uma extração adicional da seleção de ícones, mantendo a precedência das condições.

Capturas com os mesmos dados fictícios deram imagens idênticas para o painel e o editor. Na tela de pausa, a diferença observada foi a frase aleatória já existente, sem mudança de layout. A extensão também foi conferida em perfis temporários do Chrome e Edge, incluindo conexão, aplicação de regras e encaminhamento do site bloqueado no Chrome.

Os testes de navegador usam o conector já registrado. Não comprovam o novo binário do conector, que foi validado separadamente pelas integrações em diretórios temporários. Não foram exercitados o serviço elevado contra o hosts real nem todos os eventos de edição do Windows Forms. As limitações anteriores de segurança e armazenamento continuam descritas em `SECURITY.md` e `docs/engenharia.md`.
