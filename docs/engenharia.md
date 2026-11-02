# Decisões técnicas e revisão de funcionamento

Revisão de 02/11/2026. O escopo inclui motor de regras, extensão, conector, compilação, instalação e serviço opcional. As verificações automatizadas são evidência de cenários específicos, não garantia de ausência de bugs.

## Caminhos principais

| Componente | Responsabilidade |
| --- | --- |
| `painel/Core.cs` | Horários, consumo, pausas, histórico e armazenamento |
| `painel/Changes.cs` | Compara restrições e agenda flexibilizações para 24 horas depois |
| `painel/NativeHost.cs` | Protocolo Native Messaging e amostragem da aba ativa |
| `painel/extensao/background.js` | Conexão, bloqueios de rede e encaminhamento de abas |
| `painel/Dashboard.cs` | Painel Windows Forms |
| `painel/GuardCore.cs`, `GuardService.cs` | Pausas e restauração opcional do hosts |

O motor fica no conector para compartilhar cotas entre Chrome e Edge e continuar funcionando com o painel fechado. As regras usam uma interseção de permissões: qualquer regra correspondente pode bloquear. Um intervalo de uso consome as cotas correspondentes, mas entra uma única vez no total do dia.

O arquivo JSON usa mutex e substituição atômica com backup. A escolha simplifica a instalação, mas implica ler e regravar o estado a cada amostra do conector, normalmente a cada segundo. Ainda não há benchmark de disco, escala ou sincronização em nuvem. Um banco local ou gravação agrupada exigiria preservar a concorrência entre navegadores e definir a perda máxima de contagem após uma falha.

O bloqueio de rede e o encaminhamento visual são separados. Se a navegação falhar antes de a aba revelar seu endereço, os eventos de navegação ainda permitem encaminhar o usuário à página do Vigília. A regra de rede permanece aplicada durante essa transição.

## Problemas corrigidos nesta revisão

| Problema | Efeito | Correção e evidência |
| --- | --- | --- |
| Bloqueios pessoais nos valores iniciais da extensão | Uma instalação vazia bloqueava sites não escolhidos | Valores iniciais vazios, preservação do cache existente e teste de instalação nova |
| Inicialização rejeitada permanentemente após erro transitório | A extensão podia parar de sincronizar até ser recarregada | Inicialização permite nova tentativa, com erro simulado no teste |
| Botão de reabrir preservado após erro de comunicação | A página mantinha uma ação baseada em estado antigo | Remove a ação ao falhar e testa a transição conectado → erro |
| Resposta do conector com histórico e regras arquivadas | Exposição desnecessária ao contexto da extensão | Resposta mínima, verificada no executável de teste e no motor |
| Mesmo limite de serialização para IPC e diário de restauração | Crescimento do estado podia impedir salvar e restaurar bloqueios | Orçamentos separados, admissão limitada, teste com 2.000 entradas e falha simulada de gravação |
| Instalador aceitava ausência do conector ou manifesto de outra pasta | Instalação aparentemente concluída, mas sem conexão | Validação antes de registrar o Native Messaging |
| Teste de navegador exigia pelo menos um bloqueio | Instalação válida sem regras era tratada como erro | Verificação da conexão e correspondência com as regras, inclusive lista vazia |
| Espera assíncrona do teste de navegador podia terminar com resultado falso | O teste podia passar sem confirmar conexão | Consulta assíncrona explícita, repetida até confirmação, seguida de assertiva |

## Verificação

Os comandos estão no README. A revisão executou 202 verificações C#, sete cenários da extensão, integração com conectores em diretórios temporários e testes PowerShell em hosts fictício. Também compilou uma cópia limpa dos arquivos-fonte, sem dados pessoais, registro do conector ou instalação do serviço.

Os testes de navegador confirmaram conexão e correspondência das regras em perfis temporários do Chrome e Edge. No Chrome, também confirmaram o encaminhamento de um site com cota esgotada à página do Vigília. São opcionais e usam a instalação já registrada. Não fazem parte da CI e não substituem testes em uma máquina Windows limpa. O serviço elevado não foi instalado nem exercitado contra o hosts real nesta revisão.

## Pendências que afetam a maturidade

- Resolver as limitações do [serviço privilegiado](../SECURITY.md) antes de apresentá-lo como componente pronto para distribuição ampla.
- A refatoração de legibilidade foi aplicada ao motor, às alterações, aos formulários, ao serviço, ao conector e à extensão. Consulte a [análise, plano e validação](refatoracao.md). A lógica de cotas e concorrência ainda exige atenção nas próximas alterações.
- Criar um teste de navegador com conector e dados inteiramente isolados, executável na CI. Hoje há testes de protocolo e navegador em caminhos separados.
- Validar instalação, atualização, desinstalação, escalas de tela e recuperação em uma máquina Windows limpa. A compilação isolada não comprova toda essa experiência.
- Definir política de retenção, localização dos dados e recuperação de arquivo corrompido. A leitura atual falha sem sobrescrever o arquivo inválido, mas não recupera automaticamente o backup.
- Escolher uma licença antes de anunciar permissões de reutilização do código. O repositório público ainda não contém uma licença.

O projeto pode ser demonstrado como um protótipo funcional com limitações declaradas. A compilação e os testes ajudam a avaliar o trabalho, mas não sustentam promessas de bloqueio inviolável, proteção empresarial ou produção já endurecida.
