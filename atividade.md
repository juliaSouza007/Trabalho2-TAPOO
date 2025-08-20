Trabalho 2: Asteroides Multiplayer
Neste trabalho você e seu grupo vão transformar o jogo clássico de Asteroides single-player em um jogo multiplayer cooperativo para dois jogadores. Vocês deverão projetar e implementar uma arquitetura cliente-servidor robusta. Cada jogador, usando um computador diferente, irá controlar sua nave dentro do mesmo ambiente virtual, interagindo com asteroides e outros elementos compartilhados.
Objetivos Específicos:
Seu grupo deverá:
Projetar e implementar uma comunicação robusta entre cliente e servidor usando o protocolo TCP em C#.
Garantir que o cliente utilize programação assíncrona (async/await) para manter a responsividade da aplicação, evitando bloqueios durante a comunicação em rede.
Implementar paralelismo no servidor para gerenciar múltiplos clientes simultaneamente, usando recursos como Task ou Thread.
Aplicar técnicas de programação paralela (Parallel.ForEach ou PLINQ) para otimizar uma tarefa computacionalmente pesada no servidor (por exemplo, cálculo de colisões ou atualização de objetos). Explique claramente na apresentação qual tarefa foi escolhida e justifique essa escolha.
Desenvolver um sistema robusto que lide corretamente com situações como desconexões abruptas dos jogadores.
Implementar um menu inicial no jogo, permitindo opções básicas como iniciar partida, sair do jogo e configurar conexão ao servidor.
Entregáveis:
Código Completo:
Solução no Visual Studio com dois projetos separados: Servidor e Cliente.
Código limpo, organizado e bem comentado, seguindo boas práticas.
Vídeo Demonstrativo:
Vídeo curto (máximo 5 minutos) mostrando claramente:
Inicialização do servidor.
Conexão bem-sucedida de dois clientes ao servidor.
Demonstração prática da jogabilidade cooperativa, incluindo a sincronização entre jogadores.
Exemplo claro do sistema lidando com a desconexão abrupta de um jogador.
Slides da Apresentação:
Diagramas detalhados da arquitetura cliente-servidor e protocolo de comunicação.
Explicação técnica detalhada sobre:
Implementação da comunicação TCP.
Uso de programação assíncrona (async/await).
Aplicação do paralelismo para gerenciar múltiplos clientes.
Otimização paralela de uma tarefa específica, com justificativa.
Destaque claro dos recursos adicionais implementados (se houver), incluindo sprites, efeitos visuais e sonoros ou otimização com SIMD.
Reflexão sobre os principais desafios enfrentados durante o desenvolvimento e o aprendizado obtido.
Avaliação:
Apresentação (5 pontos): Avalia-se a clareza, organização e qualidade dos slides, o domínio do conteúdo técnico durante a apresentação e a qualidade do vídeo demonstrativo.
Entregáveis (10 pontos): Avalia-se a funcionalidade correta do jogo multiplayer, a implementação adequada dos requisitos técnicos (TCP, programação assíncrona, paralelismo), robustez frente a desconexões e qualidade geral do código (organização, comentários e práticas recomendadas).
Pontos Extras: Implementação bem executada dos seguintes recursos adicionais:
Sprites e animações para substituir formas simples.
Efeitos visuais e sonoros como explosões, partículas e sons ambiente.
Otimização avançada com instruções SIMD para melhorar ainda mais o desempenho.
A pontuação extra considerará a qualidade, complexidade e polimento desses recursos, valendo até 2 pontos.
