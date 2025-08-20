using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Cliente.Models;

namespace Cliente.Network;

/// <summary>
/// Cliente TCP com programação assíncrona para manter responsividade da aplicação
/// Evita bloqueios durante comunicação de rede usando async/await
/// </summary>
public class TcpGameClient
{
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private bool _isConnected;
    private readonly string _serverHost;
    private readonly int _serverPort;
    private CancellationTokenSource _cancellationTokenSource;

    // Eventos para comunicação com a interface do jogo
    public event Action<GameState>? OnGameStateReceived;
    public event Action<string>? OnConnectionStatusChanged;
    public event Action<string>? OnErrorOccurred;
    public event Action<string>? OnClientIdReceived;

    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;

    public TcpGameClient(string host = "localhost", int port = 8888)
    {
        _serverHost = host;
        _serverPort = port;
        _cancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// ASYNC/AWAIT: Conecta ao servidor de forma assíncrona para manter responsividade
    /// </summary>
    public async Task<bool> ConectarAsync()
    {
        try
        {
            OnConnectionStatusChanged?.Invoke("Conectando ao servidor...");

            _tcpClient = new TcpClient();

            // Conexão assíncrona para evitar bloqueio da UI
            await _tcpClient.ConnectAsync(_serverHost, _serverPort);

            _stream = _tcpClient.GetStream();
            _isConnected = true;

            OnConnectionStatusChanged?.Invoke($"Conectado ao servidor {_serverHost}:{_serverPort}");

            // Iniciar loop de recepção de mensagens em background
            _ = Task.Run(async () => await ReceberMensagensAsync(_cancellationTokenSource.Token));

            // Iniciar ping periódico para manter conexão viva
            _ = Task.Run(async () => await PingPeriodicoAsync(_cancellationTokenSource.Token));

            return true;
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke($"Erro ao conectar: {ex.Message}");
            OnConnectionStatusChanged?.Invoke("Desconectado");
            return false;
        }
    }

    /// <summary>
    /// ASYNC/AWAIT: Envia input do jogador de forma assíncrona
    /// Mantém responsividade evitando bloqueios na thread principal
    /// </summary>
    public async Task EnviarInputAsync(PlayerInput input)
    {
        if (!IsConnected || _stream == null) return;

        try
        {
            var mensagem = new NetworkMessage
            {
                Tipo = "INPUT",
                Dados = JsonConvert.SerializeObject(input)
            };

            var mensagemJson = JsonConvert.SerializeObject(mensagem) + "\n";
            var dados = Encoding.UTF8.GetBytes(mensagemJson);

            await _stream.WriteAsync(dados.AsMemory());
            await _stream.FlushAsync();
        }
        catch (IOException ioEx)
        {
            OnErrorOccurred?.Invoke($"⚠️ Falha ao enviar input (conexão perdida): {ioEx.Message}");
            await DesconectarAsync();
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke($"Erro inesperado ao enviar input: {ex.Message}");
        }
    }

    /// <summary>
    /// ASYNC/AWAIT: Loop de recepção de mensagens do servidor
    /// Roda em background para manter responsividade da aplicação
    /// </summary>
    private async Task ReceberMensagensAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var mensagemBuffer = string.Empty; // Buffer para mensagens incompletas

        try
        {
            while (_isConnected && _stream != null && !cancellationToken.IsCancellationRequested)
            {
                // Leitura assíncrona para não bloquear
                var bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (bytesRead == 0)
                {
                    // Servidor desconectou
                    OnConnectionStatusChanged?.Invoke("Servidor desconectou");
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
                        await ProcessarMensagemAsync(linhas[i].Trim());
                    }
                }

                // Manter a última linha (pode estar incompleta)
                mensagemBuffer = linhas[linhas.Length - 1];
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelamento normal
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke($"Erro na recepção: {ex.Message}");
        }
        finally
        {
            await DesconectarAsync();
        }
    }

    /// <summary>
    /// ASYNC/AWAIT: Processa mensagens recebidas do servidor de forma assíncrona
    /// </summary>
    private async Task ProcessarMensagemAsync(string mensagemJson)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(mensagemJson))
                return;

            var mensagem = JsonConvert.DeserializeObject<NetworkMessage>(mensagemJson);
            if (mensagem == null)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Mensagem JSON nula: {mensagemJson}");
                return;
            }

            switch (mensagem.Tipo)
            {
                case "GAME_STATE":
                    if (!string.IsNullOrWhiteSpace(mensagem.Dados))
                    {
                        var gameState = JsonConvert.DeserializeObject<GameState>(mensagem.Dados);
                        if (gameState != null)
                        {
                            // Notificar interface do jogo sobre novo estado (thread-safe)
                            OnGameStateReceived?.Invoke(gameState);
                        }
                    }
                    break;

                case "CLIENT_ID":
                    // Receber ID único do cliente
                    if (!string.IsNullOrWhiteSpace(mensagem.Dados))
                    {
                        OnClientIdReceived?.Invoke(mensagem.Dados);
                        System.Diagnostics.Debug.WriteLine($"🆔 Cliente ID recebido: {mensagem.Dados}");
                    }
                    break;

                case "PONG":
                    // Resposta ao ping - conexão está viva
                    System.Diagnostics.Debug.WriteLine("🏓 PONG recebido");
                    break;

                default:
                    System.Diagnostics.Debug.WriteLine($"⚠️ Tipo de mensagem desconhecido: {mensagem.Tipo}");
                    break;
            }
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke($"Erro ao processar mensagem: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"📄 Mensagem JSON com erro: {mensagemJson}");
        }

        await Task.CompletedTask; // Para manter assinatura async
    }

    /// <summary>
    /// ASYNC/AWAIT: Envia ping periódico para manter conexão viva
    /// Roda em background sem bloquear a aplicação
    /// </summary>
    private async Task PingPeriodicoAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (_isConnected && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(5000, cancellationToken); // Ping a cada 5 segundos

                if (_stream != null && IsConnected)
                {
                    var pingMessage = new NetworkMessage
                    {
                        Tipo = "PING",
                        Dados = "PING"
                    };

                    var mensagemJson = JsonConvert.SerializeObject(pingMessage) + "\n";
                    var dados = Encoding.UTF8.GetBytes(mensagemJson);

                    await _stream.WriteAsync(dados.AsMemory(), cancellationToken);
                    await _stream.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelamento normal
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke($"Erro no ping: {ex.Message}");
        }
    }

    /// <summary>
    /// ASYNC/AWAIT: Desconecta do servidor de forma assíncrona
    /// </summary>
    public async Task DesconectarAsync()
    {
        try
        {
            _isConnected = false;
            _cancellationTokenSource.Cancel();

            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }

            _tcpClient?.Close();
            _tcpClient = null;

            OnConnectionStatusChanged?.Invoke("Desconectado");
        }
        catch (Exception ex)
        {
            OnErrorOccurred?.Invoke($"Erro ao desconectar: {ex.Message}");
        }
    }

    /// <summary>
    /// Libera recursos
    /// </summary>
    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _stream?.Dispose();
        _tcpClient?.Close();
    }
}