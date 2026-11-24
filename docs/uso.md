# Usar o Vigília

## Regras de acesso

Em **Nova regra**, informe um nome e um ou mais domínios, um por linha. Um domínio também inclui seus subdomínios. Endereços agrupados compartilham a mesma cota.

Escolha o modo:

- **Bloquear sempre pela extensão:** impede acesso até uma pausa válida.
- **Permitir apenas no horário + limite diário:** exige que dia, horário e cota permitam o acesso.
- **Bloquear durante uma faixa de horário:** bloqueia na faixa selecionada. Fora dela, uma cota diária adicional ainda pode valer.
- **Cota de minutos na faixa · livre fora dela:** limita os minutos apenas naquela faixa. Fora dela e nos dias não selecionados, o uso é livre.

Horários iguais significam o dia inteiro. Numa faixa de 22h a 6h, o dia selecionado é o de início. O consumo dessa faixa não reinicia à meia-noite. O valor zero significa ausência de cota diária nos modos que o aceitam.

O painel pode ficar fechado. A extensão precisa permanecer conectada ao conector para receber regras novas. Se a conexão cair por pouco tempo (o navegador reinicia a extensão em segundo plano ou o arquivo de dados fica ocupado por alguns segundos), ela mantém por até 90 segundos as últimas decisões recebidas: o que estava bloqueado continua bloqueado e o que estava liberado continua liberado, enquanto reconecta. Nesse intervalo o uso não é contado. Essa tolerância vale uma vez a cada 10 minutos: outra queda nesse período bloqueia na hora. Depois de 90 segundos sem o painel, todos os sites configurados ficam bloqueados até reconectar.

Quando a extensão bloqueia uma aba, a página do Vigília guarda o endereço completo. Se o bloqueio foi só de conexão, a aba volta sozinha para a mesma página (o mesmo vídeo ou post) quando a conexão retorna; nos outros casos, o botão de abrir leva à página exata, e não à inicial do site.

No popup da extensão, os limites de tempo (cota diária, cota por faixa e horários) aparecem primeiro e os bloqueios permanentes por último.

### Combinar limite por faixa com limite diário

Você pode cadastrar o mesmo domínio em várias regras. O acesso só fica disponível quando **todas** as regras correspondentes permitem. Uma regra de subdomínio também combina com as regras do domínio principal.

Para manter uma cota na faixa e acrescentar um teto de 40 minutos no Instagram:

1. Mantenha a regra atual de `instagram.com` no modo **Cota de minutos na faixa · livre fora dela**.
2. Crie outra regra, como **Instagram · máximo diário**, com `instagram.com`.
3. Escolha **Permitir apenas no horário + limite diário**, informe **40 minutos**, coloque início e fim em **00:00** (dia inteiro) e marque todos os dias.

Um minuto dentro da faixa consome um minuto das duas cotas, não dois minutos do dia. Fora da faixa, apenas o limite diário restringe o acesso. Esgotar qualquer uma das cotas bloqueia o site enquanto aquela regra estiver restringindo. Sessões de estudo e bloqueios permanentes também prevalecem. A espera de 24 horas para flexibilizar cada regra continua valendo.

Ao criar uma regra, ela aproveita o consumo conhecido do dia para seus domínios. Em dados antigos, sem detalhamento por domínio, o consumo só pode ser reaproveitado de uma regra com exatamente os mesmos domínios. Não é possível reconstruir quanto um único site consumiu dentro de um grupo antigo com vários sites. O total do painel usa contagem única para os novos intervalos e preserva o total anterior na migração.

Depois de atualizar o aplicativo, clique em **Recarregar** na extensão do Chrome e do Edge para ativar a versão atual. A combinação de regras nas abas já abertas está disponível desde a versão 1.3.0.

Na versão 1.3.1, a navegação bloqueada abre a página do Vigília com motivo, consumo e renovação da cota, em vez do erro genérico do Chrome. A permissão `webNavigation` reconhece o início e o erro de navegação para encaminhar os sites configurados à página local. Não adiciona leitura do conteúdo de páginas nem gravação de histórico completo. O endereço mostrado nessa página contém apenas o domínio, sem caminho ou parâmetros da página original. Se o navegador pedir confirmação das permissões ao recarregar, confirme manualmente para ativar a nova versão. Em janelas privadas, permanece a página em branco por restrição do modo de execução da extensão.

A versão 1.3.2 começa sem bloqueios pré-definidos em instalações novas, tenta novamente após um erro de inicialização e retira o botão de reabrir se a página perder comunicação com a extensão. Instalações existentes preservam as regras e o cache. A redução dos dados enviados ao navegador exige atualizar também o conector compilado.

## Alterações e estudo

Reduzir a cota ou aumentar a restrição é imediato. Aumentar o tempo, remover domínios, excluir ou arquivar uma regra que restringe acesso exige 24 horas. Uma mudança que restringe um período e libera outro também espera. O consumo já registrado participa da comparação.

O botão **Pendentes** permite revisar ou cancelar pedidos. Há um pedido por regra. Substituir uma proposta por outra diferente reinicia a espera. Uma restrição imediata cancela a proposta anterior dessa regra. Reativar uma regra arquivada é imediato e preserva o consumo do período.

Em **Sessão de estudo**, selecione os sites e a duração. O prazo continua correndo com o painel fechado ou o computador suspenso. Ao terminar, as demais regras voltam a determinar o acesso.

Enquanto a sessão está ativa, ela aparece no cabeçalho do painel ("Sessão de estudo até HH:mm · faltam …"), no botão ("Estudo até HH:mm"), no título da janela, no topo do popup da extensão e na página de bloqueio, que informa quando a sessão termina. Os sites incluídos mostram o motivo "Sessão de estudo até …".

## Pausas de bloqueios permanentes

Um pedido espera 30 minutos e libera 10 minutos de relógio, mesmo sem navegação. Um novo pedido para o mesmo site/grupo fica disponível 24 horas após o início previsto da liberação anterior. X e Twitter compartilham esse prazo. Grupos independentes têm prazos independentes.

Cancelar uma pausa não reinicia o prazo para outro pedido. Uma sessão de estudo ativa impede um novo pedido. A tela guarda pedidos antes de enviá-los ao serviço, para permitir conferência após falhas de comunicação. Pedidos sem confirmação não liberam a extensão. A reconciliação e a tentativa de confirmar cancelamentos pendentes ocorrem com a tela de pausa aberta.

O serviço opcional `FocoLocalGuard` fica em `%ProgramFiles%\FocoLocalGuard`. Seus dados de recuperação ficam em `%ProgramData%\FocoLocalGuard`. O painel conecta a um named pipe neste computador, cuja ACL autoriza o usuário da instalação e o sistema. Isso não substitui a rejeição explícita de clientes remotos nem a autenticação do servidor, ainda pendentes. Consulte [as limitações do serviço](../SECURITY.md).

Durante uma pausa, o serviço remove somente as entradas correspondentes dentro do bloco `BLOQUEADOR-SIMPLES-LOCAL` no hosts e guarda o necessário para restaurá-las. Outros filtros, como DNS, permanecem independentes. O hosts não encerra conexões já estabelecidas, por isso a extensão continua necessária para bloquear uma aba aberta ao terminar a pausa. Uma falha persistente do serviço pode atrasar a restauração do hosts.

O serviço limita pedidos simultâneos a 128 e reserva até 48 KiB para a resposta de estado. O limite que for atingido primeiro impede pedidos adicionais até expirarem os anteriores. O diário de restauração tem orçamento separado. Uma atualização do código ou da extensão não substitui automaticamente o serviço instalado em Program Files.

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

`painel/dados.json` contém regras, consumo e acompanhamento. Para combinar cotas sem duplicar minutos, a versão 8 do formato também registra os segundos por domínio/subdomínio acompanhado no dia atual, sem caminhos de páginas. Esses totais reiniciam à meia-noite local. `dados.anterior.json` mantém uma versão anterior. Backups adicionais são criados ao atualizar. O manifesto `painel/native-host.json` é gerado pela compilação e contém o caminho local do conector.

O aplicativo de hosts guarda backups em `backups/` e operações em `operacoes/`. Esses arquivos estão no `.gitignore` e não devem ser publicados. Não adicione capturas de tela com dados pessoais ao repositório.
