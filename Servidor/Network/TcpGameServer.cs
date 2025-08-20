using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Servidor.GameEngine;
using Servidor.Models;

namespace Servidor.Network;

/// <summary>
/// Servidor TCP que gerencia múltiplos clientes simultaneamente usando paralelismo
/// Implementa comunicação robusta e tratamento de falhas
/// </summary>
public class TcpGameServer
{
    private TcpListener? _tcpListener;
    private readonly GameEngine.GameEngine _gameEngine;
    private readonly ConcurrentDictionary<string, ClientConnection> _clientes;
    private bool _isRunning;
    private readonly int _porta;
    private CancellationTokenSource _cancellationTokenSource;

    public TcpGameServer(int porta = 8888)
    {
        _porta = porta;
        _gameEngine = new GameEngine.GameEngine();
        _clientes = new ConcurrentDictionary<string, ClientConnection>();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Inicia o servidor e começa a aceitar conexões de clientes
    /// </summary>
    public async Task IniciarServidorAsync()
    {
        try
        {
            _tcpListener = new TcpListener(IPAddress.Any, _porta);
            _tcpListener.Start();
            _isRunning = true;

            Console.WriteLine($"🚀 Servidor iniciado na porta {_porta}");
            Console.WriteLine("📡 Aguardando conexões de clientes...");

            // Task para aceitar conexões de clientes (paralelismo)
            var aceitarConexoesTask = Task.Run(async () => await AceitarConexoesAsync(_cancellationTokenSource.Token));

            // Task para loop principal do jogo (paralelismo)
            var gameLoopTask = Task.Run(async () => await GameLoopAsync(_cancellationTokenSource.Token));

            // Aguardar ambas as tasks
            await Task.WhenAll(aceitarConexoesTask, gameLoopTask);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro ao iniciar servidor: {ex.Message}");
        }
    }

    /// <summary>
    /// PARALELISMO: Aceita conexões de clientes de forma assíncrona
    /// Cada cliente é tratado em uma Task separada
    /// </summary>
    private async Task AceitarConexoesAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_tcpListener?.Pending() == true)
                {
                    var tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    var clienteId = Guid.NewGuid().ToString();

                    Console.WriteLine($"✅ Cliente conectado: {clienteId} ({tcpClient.Client.RemoteEndPoint})");

                    var clientConnection = new ClientConnection(clienteId, tcpClient);
                    _clientes.TryAdd(clienteId, clientConnection);

                    // Enviar ID do cliente para ele se identificar
                    await EnviarMensagemAsync(clientConnection, "CLIENT_ID", clienteId);

                    // PARALELISMO: Cada cliente é tratado em uma Task separada
                    _ = Task.Run(async () => await TratarClienteAsync(clientConnection, cancellationToken));
                }

                await Task.Delay(10, cancellationToken); // Pequeno delay para evitar uso excessivo de CPU
            }
            catch (ObjectDisposedException)
            {
                // Servidor foi parado
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao aceitar conexão: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// PARALELISMO: Trata um cliente específico de forma assíncrona
    /// Cada cliente roda em sua própria Task
    /// </summary>
    private async Task TratarClienteAsync(ClientConnection cliente, CancellationToken cancellationToken)
    {
        var mensagemBuffer = string.Empty; // Buffer para mensagens incompletas

        try
        {
            var buffer = new byte[4096];
            var stream = cliente.TcpClient.GetStream();

            // Enviar ID do cliente assim que conectar
            await EnviarMensagemAsync(cliente, "CLIENT_ID", cliente.Id);

            while (_isRunning && cliente.TcpClient.Connected && !cancellationToken.IsCancellationRequested)
            {
                // Leitura assíncrona para manter responsividade
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (bytesRead == 0)
                {
                    // Cliente desconectou
                    break;
                }

                var dadosRecebidos = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                mensagemBuffer += dadosRecebidos;

                // Processar mensagens completas (terminadas com \n)
                var linhas = mensagemBuffer.Split('\n');

                // Processar todas as mensagens completas
                for (int i = 0; i < linhas.Length - 1; i++)
                {
                    if (!string.IsNullOrWhiteSpace(linhas[i]))
                    {
                        await ProcessarMensagemClienteAsync(cliente, linhas[i].Trim());
                    }
                }

                // Manter a última linha (pode estar incompleta)
                mensagemBuffer = linhas[linhas.Length - 1];
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao tratar cliente {cliente.Id}: {ex.Message}");
        }
        finally
        {
            // Remover cliente da lista e fechar conexão
            HandleClientDisconnect(cliente.Id);
        }
    }

    /// <summary>
    /// Processa mensagens recebidas do cliente de forma assíncrona
    /// </summary>
    private async Task ProcessarMensagemClienteAsync(ClientConnection cliente, string mensagemJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mensagemJson))
                return;

            var mensagem = JsonConvert.DeserializeObject<NetworkMessage>(mensagemJson);
            if (mensagem == null)
            {
                Console.WriteLine($"⚠️ Mensagem JSON nula do cliente {cliente.Id}: {mensagemJson}");
                return;
            }

            switch (mensagem.Tipo)
            {
                case "INPUT":
                    if (!string.IsNullOrWhiteSpace(mensagem.Dados))
                    {
                        var input = JsonConvert.DeserializeObject<PlayerInput>(mensagem.Dados);
                        if (input != null)
                        {
                            input.ClienteId = cliente.Id;
                            cliente.UltimoInput = input;
                        }
                    }
                    break;

                case "PING":
                    // Responder ao ping para manter conexão viva
                    await EnviarMensagemAsync(cliente, "PONG", "OK");
                    break;

                default:
                    Console.WriteLine($"⚠️ Tipo de mensagem desconhecido do cliente {cliente.Id}: {mensagem.Tipo}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Erro ao processar mensagem do cliente {cliente.Id}: {ex.Message}");
            Console.WriteLine($"📄 Mensagem JSON: {mensagemJson}");
        }
    }

    /// <summary>
    /// Loop principal do jogo que atualiza o estado e envia para todos os clientes
    /// Roda em paralelo com o tratamento de clientes
    /// </summary>
    private async Task GameLoopAsync(CancellationToken cancellationToken)
    {
        const int targetFPS = 60;
        const int frameTime = 1000 / targetFPS;

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            var frameStart = DateTime.UtcNow;

            try
            {
                // SEMPRE processar o jogo, mesmo sem inputs
                if (_clientes.Any())
                {
                    // Processar inputs de todos os clientes conectados
                    var inputs = new Dictionary<string, PlayerInput>();

                    foreach (var cliente in _clientes.Values)
                    {
                        if (cliente.UltimoInput != null)
                        {
                            inputs[cliente.Id] = cliente.UltimoInput;
                        }
                    }

                    // Atualizar estado do jogo (inclui processamento paralelo de colisões)
                    var estadoJogo = _gameEngine.AtualizarJogo(inputs);

                    // SEMPRE enviar estado atualizado para todos os clientes conectados
                    await BroadcastEstadoJogoAsync(estadoJogo);
                }

                // Controle de FPS
                var frameEnd = DateTime.UtcNow;
                var frameElapsed = (int)(frameEnd - frameStart).TotalMilliseconds;
                var sleepTime = Math.Max(0, frameTime - frameElapsed);

                if (sleepTime > 0)
                {
                    await Task.Delay(sleepTime, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro no game loop: {ex.Message}");
                await Task.Delay(frameTime, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Envia o estado do jogo para todos os clientes conectados usando paralelismo
    /// </summary>
    private async Task BroadcastEstadoJogoAsync(GameState estadoJogo)
    {
        if (!_clientes.Any()) return;

        var mensagemJson = JsonConvert.SerializeObject(new NetworkMessage
        {
            Tipo = "GAME_STATE",
            Dados = JsonConvert.SerializeObject(estadoJogo)
        }) + "\n";

        // PARALELISMO: Enviar para todos os clientes simultaneamente
        var tasks = _clientes.Values.Select(async cliente =>
        {
            try
            {
                await EnviarMensagemAsync(cliente, mensagemJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Erro ao enviar para cliente {cliente.Id}: {ex.Message}");
                HandleClientDisconnect(cliente.Id);
            }
        });

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Envia uma mensagem para um cliente específico
    /// </summary>
    private async Task EnviarMensagemAsync(ClientConnection cliente, string tipo, string dados)
    {
        var mensagem = new NetworkMessage { Tipo = tipo, Dados = dados };
        var mensagemJson = JsonConvert.SerializeObject(mensagem) + "\n";
        await EnviarMensagemAsync(cliente, mensagemJson);
    }

    /// <summary>
    /// Envia dados brutos para um cliente
    /// </summary>
    private async Task EnviarMensagemAsync(ClientConnection cliente, string mensagemJson)
    {
        try
        {
            if (cliente.TcpClient.Connected)
            {
                var dados = Encoding.UTF8.GetBytes(mensagemJson);
                var stream = cliente.TcpClient.GetStream();
                await stream.WriteAsync(dados.AsMemory());
                await stream.FlushAsync();
            }
        }
        catch (Exception)
        {
            // Cliente desconectou, será removido no próximo ciclo
            throw;
        }
    }

    /// <summary>
    /// Para o servidor e desconecta todos os clientes
    /// </summary>
    public void PararServidor()
    {
        Console.WriteLine("🛑 Parando servidor...");
        _isRunning = false;
        _cancellationTokenSource.Cancel();

        // Desconectar todos os clientes
        foreach (var cliente in _clientes.Values)
        {
            cliente.TcpClient.Close();
        }
        _clientes.Clear();

        _tcpListener?.Stop();
        Console.WriteLine("✅ Servidor parado");
    }

    /// <summary>
    /// Remove cliente da lista e remove sua nave do estado do jogo
    /// </summary>
    private void HandleClientDisconnect(string clientId)
    {
        if (_clientes.TryRemove(clientId, out var client))
        {
            client.TcpClient.Close();
            Console.WriteLine($"❌ Cliente removido: {clientId}");

            // Remover nave associada ao cliente
            var estadoJogo = _gameEngine.ObterEstadoJogo();
            if (estadoJogo.Naves.Remove(clientId))
            {
                Console.WriteLine($"🚫 Nave do jogador {clientId} removida do jogo");
            }
        }
    }
}

/// <summary>
/// Representa uma conexão de cliente
/// </summary>
public class ClientConnection
{
    public string Id { get; }
    public TcpClient TcpClient { get; }
    public PlayerInput? UltimoInput { get; set; }
    public DateTime UltimaAtividade { get; set; }

    public ClientConnection(string id, TcpClient tcpClient)
    {
        Id = id;
        TcpClient = tcpClient;
        UltimaAtividade = DateTime.UtcNow;
    }
}