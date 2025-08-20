# Comunicação TCP e Paralelismo — Resumo Técnico do Projeto

Este documento descreve exclusivamente quatro partes do trabalho: (1) a comunicação robusta Cliente⇄Servidor via TCP em C#, (2) o uso de async/await no cliente para manter responsividade, (3) o paralelismo no servidor para gerenciar múltiplos clientes simultaneamente, e (4) a aplicação de técnicas de programação paralela para otimizar uma tarefa computacionalmente pesada no servidor.


## 1) Comunicação robusta Cliente⇄Servidor via TCP (C#)

Arquivos principais:
- Servidor: `Servidor/Network/TcpGameServer.cs`
- Cliente: `Cliente/Network/TcpGameClient.cs`

Como foi feito:
- Protocolo de mensagens textual usando JSON (Newtonsoft.Json) para serialização/deserialização.
- Framing de mensagens por linha: cada mensagem JSON termina com `\n`. O lado receptor usa um buffer acumulador e faz `Split('\n')` para processar apenas mensagens completas; a última linha incompleta permanece no buffer até chegar o restante. Isso torna a comunicação robusta a fragmentação de pacotes.
- Identidade do cliente: ao conectar, o servidor envia uma mensagem `CLIENT_ID` com um GUID único, permitindo que o cliente se identifique e que o servidor associe inputs por cliente.
- Heartbeat/Keep-alive: o cliente envia periodicamente `PING`; o servidor responde com `PONG`, ajudando a detectar quedas de conexão e manter o socket ativo.
- Tratamento de erros e desconexões: toda a E/S é protegida com try/catch. Quando há falha, o servidor remove o cliente do `ConcurrentDictionary` e fecha a conexão; no cliente, eventos informam o status e a desconexão é feita de forma segura.
- Broadcast de estado: a cada frame do servidor, é enviada a mensagem `GAME_STATE` (JSON) para todos os clientes conectados, garantindo sincronização do jogo em rede.


## 2) Cliente responsivo com async/await (sem bloqueios)

Arquivo principal: `Cliente/Network/TcpGameClient.cs`

Como foi feito:
- Conexão assíncrona: `ConectarAsync()` usa `TcpClient.ConnectAsync` e, após conectar, dispara duas rotinas em background via `Task.Run`: (a) recepção contínua e (b) ping periódico.
- Envio de dados sem bloquear: `EnviarInputAsync()` serializa o input para JSON e usa `_stream.WriteAsync`/`FlushAsync`, evitando travar a thread principal do jogo.
- Recepção em background: `ReceberMensagensAsync()` faz `ReadAsync` com `CancellationToken`, acumula dados no buffer e processa mensagens completas. Os resultados são propagados via eventos (ex.: `OnGameStateReceived`) para a camada de UI, sem bloquear o loop de renderização do cliente.
- Ping assíncrono: `PingPeriodicoAsync()` roda em uma `Task` separada, aguardando com `Task.Delay` e enviando mensagens de keep-alive sem interferir na jogabilidade.
- Tratamento robusto: em exceções ou cancelamentos, o cliente notifica via eventos (`OnErrorOccurred`, `OnConnectionStatusChanged`) e encerra a conexão de forma limpa com `DesconectarAsync`.


## 3) Paralelismo no servidor para múltiplos clientes simultâneos

Arquivo principal: `Servidor/Network/TcpGameServer.cs`

Como foi feito:
- Aceitação de conexões em paralelo: `IniciarServidorAsync()` inicia uma `Task` dedicada para aceitar novas conexões (`AceitarConexoesAsync`). Cada cliente aceito é tratado em sua própria `Task` (`TratarClienteAsync`).
- Loop do jogo em paralelo: o loop principal (`GameLoopAsync`) roda em uma `Task` separada, em paralelo tanto com a aceitação de conexões quanto com o tratamento de cada cliente.
- Estruturas thread-safe: os clientes conectados são mantidos em `ConcurrentDictionary<string, ClientConnection>`, permitindo acesso concorrente seguro entre múltiplas tasks.
- Processamento de inputs: a cada frame, o servidor coleta o último input de cada cliente e atualiza o estado do jogo, depois faz broadcast do `GAME_STATE` a todos, de forma assíncrona.
- Cancelamento e encerramento: o servidor utiliza `CancellationTokenSource` para encerrar tarefas de forma coordenada quando necessário.


## 4) Programação paralela para tarefa pesada no servidor

Arquivo principal: `Servidor/GameEngine/GameEngine.cs`

Tarefa escolhida: processamento de colisões (tiros × asteroides) e verificação de destruição de naves.

Como foi feito:
- Método dedicado: `ProcessarColisoesParalelo()` usa `Parallel.ForEach` para distribuir a verificação de colisões por asteroide em múltiplos threads.
- Coleções concorrentes: usa `ConcurrentBag<int>` para acumular índices de tiros/asteroides a remover, garantindo segurança entre threads.
- Operações atômicas: usa `Interlocked.Add` para acumular pontuação adicional decorrente de colisões, evitando condições de corrida.
- Aplicação sequencial de remoções: após o processamento paralelo, aplica as remoções de forma ordenada/sequencial para manter a integridade das listas.

Justificativa técnica:
- Intensidade computacional: a verificação de colisões envolve cálculos de distância e ocorre a cada frame (alvo ~60 FPS), multiplicando o custo por segundo.
- Complexidade: o problema é tipicamente O(n×m) (n tiros × m asteroides), crescendo rapidamente conforme a cena fica mais carregada.
- Independência das iterações: cada checagem de colisão por asteroide é independente, tornando-se um candidato ideal à paralelização.
- Impacto direto em performance: mover essa carga para paralelismo reduz o tempo por frame e melhora a escalabilidade do servidor com mais objetos em jogo.


— Fim —