# Usar o Vigília

## Regras de acesso

Em **Nova regra**, informe um nome e um ou mais domínios, um por linha. Um domínio também inclui seus subdomínios. Endereços agrupados compartilham a mesma cota.

Escolha o modo:

- **Bloquear sempre pela extensão:** impede acesso até uma pausa válida.
- **Permitir apenas no horário + limite diário:** exige que dia, horário e cota permitam o acesso.
- **Bloquear durante uma faixa de horário:** bloqueia na faixa selecionada. Fora dela, uma cota diária adicional ainda pode valer.
- **Cota de minutos na faixa · livre fora dela:** limita os minutos apenas naquela faixa. Fora dela e nos dias não selecionados, o uso é livre.

Horários iguais significam o dia inteiro. Numa faixa de 22h a 6h, o dia selecionado é o de início. O consumo dessa faixa não reinicia à meia-noite. O valor zero significa ausência de cota diária nos modos que o aceitam.

O painel pode ficar fechado. A extensão precisa permanecer conectada ao conector para receber regras novas. Quando a conexão falha, ela bloqueia os sites já configurados até reconectar.

## Alterações e estudo

Reduzir a cota ou aumentar a restrição é imediato. Aumentar o tempo, remover domínios, excluir ou arquivar uma regra que restringe acesso exige 24 horas. Uma mudança que restringe um período e libera outro também espera. O consumo já registrado participa da comparação.

O botão **Pendentes** permite revisar ou cancelar pedidos. Há um pedido por regra. Substituir uma proposta por outra diferente reinicia a espera. Uma restrição imediata cancela a proposta anterior dessa regra. Reativar uma regra arquivada é imediato e preserva o consumo do período.

Em **Sessão de estudo**, selecione os sites e a duração. O prazo continua correndo com o painel fechado ou o computador suspenso. Ao terminar, as demais regras voltam a determinar o acesso.

## Pausas de bloqueios permanentes

Um pedido espera 30 minutos e libera 10 minutos de relógio, mesmo sem navegação. Um novo pedido para o mesmo site/grupo fica disponível 24 horas após o início previsto da liberação anterior. X e Twitter compartilham esse prazo. Grupos independentes têm prazos independentes.

Cancelar uma pausa não reinicia o prazo para outro pedido. Uma sessão de estudo ativa impede um novo pedido. A tela guarda pedidos antes de enviá-los ao serviço, para permitir conferência após falhas de comunicação. Pedidos sem confirmação não liberam a extensão. A reconciliação e a tentativa de confirmar cancelamentos pendentes ocorrem com a tela de pausa aberta.

O serviço opcional `FocoLocalGuard` fica em `%ProgramFiles%\FocoLocalGuard`. Seus dados de recuperação ficam em `%ProgramData%\FocoLocalGuard`. Ele usa um canal local autorizado para o usuário da instalação e não abre porta de rede.

Durante uma pausa, o serviço remove somente as entradas correspondentes dentro do bloco `BLOQUEADOR-SIMPLES-LOCAL` no hosts e guarda o necessário para restaurá-las. Outros filtros, como DNS, permanecem independentes. O hosts não encerra conexões já estabelecidas, por isso a extensão continua necessária para bloquear uma aba aberta ao terminar a pausa. Uma falha persistente do serviço pode atrasar a restauração do hosts.

Para remover o serviço, um administrador deve primeiro pará-lo, conferir a restauração do bloco no hosts e então excluir o serviço. Preserve os dados de recuperação até confirmar a restauração.

## Acompanhamento

O painel mostra os últimos 14 dias:

- Verde: sem uso registrado de sites permanentemente bloqueados.
- Cinza: uso dentro da pausa permitida de 10 minutos.
- Vermelho: uso fora da pausa ou acima desse limite.
- Traço: uso antigo sem informação suficiente para classificar.
- Contorno vazio: período anterior ao acompanhamento.

Passe o mouse sobre um dia para ver sua data e classificação. Hoje tem uma borda clara e mostra o estado até o momento. **Registrar uso** permite informar o que aconteceu em outro dispositivo. Um dia vermelho não é reduzido para cinza por um registro posterior. Qualquer uso, inclusive o cinza, reinicia a sequência de dias sem apps. O recorde é preservado.

## Arquivos locais

`painel/dados.json` contém regras, consumo e acompanhamento. `dados.anterior.json` mantém uma versão anterior. Backups adicionais são criados ao atualizar. O manifesto `painel/native-host.json` é gerado pela compilação e contém o caminho local do conector.

O aplicativo de hosts guarda backups em `backups/` e operações em `operacoes/`. Esses arquivos estão no `.gitignore` e não devem ser publicados. Não adicione capturas de tela com dados pessoais ao repositório.
