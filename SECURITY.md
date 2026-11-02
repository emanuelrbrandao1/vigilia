# Segurança e privacidade

Vigília é um protótipo de autocontrole para um computador administrado pelo próprio usuário. Não é uma fronteira de segurança contra o dono da máquina, malware ou outro administrador. Desativar a extensão, usar um navegador sem ela ou editar dados locais pode contornar as regras.

## Dados e permissões

- A extensão consulta abas, atividade e navegação para aplicar regras. Não injeta scripts para ler o conteúdo das páginas.
- O conector responde com as regras ativas e o dia de referência. Histórico de acompanhamento, regras arquivadas e atividade dos outros navegadores ficam fora dessa resposta.
- Não há telemetria nem API de IA. A conversa opcional copia texto para a área de transferência após um clique e abre o ChatGPT. O envio depende do usuário.
- Os dados e backups são arquivos locais sem criptografia na pasta da instalação. Uma pasta sincronizada pode enviá-los ao provedor de sincronização. O `.gitignore` evita inclusão acidental no Git, não restringe acesso a arquivos.
- O painel mostra 14 dias, mas pode reter até 400 datas com uso registrado. Backups adicionais e registros de operações ainda não têm limpeza automática.

## Serviço privilegiado opcional

O serviço `FocoLocalGuard` executa como LocalSystem para alterar o hosts. As cotas da extensão não exigem esse serviço. Ele permanece experimental pelos seguintes pontos conhecidos:

1. O cliente não autentica a identidade do processo servidor do named pipe. Um processo local que consiga ocupar o endpoint pode fornecer respostas falsas. A validação dos prazos recebidos também precisa ser fortalecida.
2. O servidor autoriza o SID da conta, mas ainda não impõe rejeição explícita de clientes de rede. A acessibilidade depende de SMB, firewall e autenticação. Não foi demonstrada exposição pela internet.
3. Há um único atendimento IPC. A leitura tem timeout por fragmento e a escrita não tem prazo total, permitindo que um cliente autorizado prejudique a disponibilidade do canal. A restauração periódica roda separadamente.
4. A escrita do hosts preserva codificação e tenta restaurar os bytes em caso de exceção, mas é feita no próprio arquivo. Encerramento abrupto ou queda de energia durante a escrita não têm recuperação transacional completa.
5. A instalação requer administrador e os binários não são assinados. O serviço e o instalador ainda precisam de validação específica de endurecimento e recuperação em uma máquina de teste.

A revisão de novembro separou o diário de restauração do limite das mensagens, limitou novos pedidos e passou a persistir uma cópia antes de ativar um pedido em memória. Isso corrige o caminho de esgotamento reproduzido nos testes, mas não resolve os pontos acima. Não houve pentest completo, teste entre contas Windows ou simulação de queda de energia.

## Relatar problemas

Um relato deve incluir versão, componente, resultado esperado, resultado observado e passos mínimos com domínios fictícios. Não publique `dados.json`, backups do hosts, capturas com hábitos pessoais ou credenciais. Este repositório ainda não tem um canal privado dedicado para receber detalhes sensíveis.
