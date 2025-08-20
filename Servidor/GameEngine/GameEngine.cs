using System.Collections.Concurrent;
using System.Numerics;
using Servidor.Models;

namespace Servidor.GameEngine;

/// <summary>
/// Motor do jogo no servidor que processa a lógica do jogo e utiliza programação paralela
/// para otimizar cálculos computacionalmente pesados como detecção de colisões
/// </summary>
public class GameEngine
{
    private readonly GameState _gameState;
    private readonly Random _random;
    private readonly object _lockObject = new();
    
    // Configurações do jogo
    private const int LARGURA_TELA = 800;
    private const int ALTURA_TELA = 600;
    private const float VELOCIDADE_NAVE = 4f;
    private const float VELOCIDADE_TIRO = 8f;
    private const int SPAWN_ASTEROIDE_FRAMES = 40;

    public GameEngine()
    {
        _gameState = new GameState();
        _random = new Random();
        InicializarJogo();
    }

    /// <summary>
    /// Inicializa o estado do jogo
    /// </summary>
    private void InicializarJogo()
    {
        _gameState.Naves.Clear();
        _gameState.Pontuacao = 0;
        _gameState.GameOver = false;
        _gameState.FrameCount = 0;
    }

    /// <summary>
    /// Atualiza o estado do jogo baseado nas entradas dos jogadores
    /// Utiliza programação paralela para otimizar cálculos de colisão
    /// </summary>
    public GameState AtualizarJogo(Dictionary<string, PlayerInput> inputs)
    {
        lock (_lockObject)
        {
            // SEMPRE processar inputs se houver
            if (inputs.Any())
            {
                // Atualizar naves de todos os jogadores
                foreach (var kvp in inputs)
                {
                    var jogadorId = kvp.Key;
                    var input = kvp.Value;
                    
                    // Garantir que a nave existe para este jogador
                    if (!_gameState.Naves.ContainsKey(jogadorId))
                    {
                        AdicionarNovoJogador(jogadorId);
                    }
                    
                    // Só processar inputs se a nave estiver viva
                    if (_gameState.Naves[jogadorId].Viva)
                    {
                        AtualizarNave(jogadorId, input);

                        // Adicionar tiro se solicitado
                        if (input.Atirar)
                        {
                            AdicionarTiro(jogadorId);
                            // Resetar input de atirar para evitar tiro contínuo
                            input.Atirar = false;
                        }
                    }

                    // Reiniciar jogo se solicitado (apenas se estiver em GameOver)
                    if (input.Reiniciar && _gameState.GameOver)
                    {
                        ReiniciarJogo();
                        input.Reiniciar = false;
                        break; // Sair do loop pois o jogo foi reiniciado
                    }
                }
            }

            // SEMPRE atualizar o mundo do jogo, mesmo sem inputs
            if (!_gameState.GameOver)
            {
                // Atualizar tiros
                AtualizarTiros();

                // Atualizar asteroides
                AtualizarAsteroides();

                // PROGRAMAÇÃO PARALELA: Cálculo de colisões usando Parallel.ForEach
                // Justificativa: O cálculo de colisões é computacionalmente intensivo pois:
                // 1. Requer verificação de distância entre múltiplos objetos (O(n*m) complexidade)
                // 2. Envolve operações matemáticas (Vector2.Distance, raiz quadrada)
                // 3. É executado a cada frame (60+ vezes por segundo)
                // 4. Escala com o número de objetos na tela
                ProcessarColisoesParalelo();

                // Spawnar novos asteroides
                if (_gameState.FrameCount % SPAWN_ASTEROIDE_FRAMES == 0)
                {
                    SpawnarAsteroide();
                }
            }

            _gameState.FrameCount++;
            return _gameState;
        }
    }

    /// <summary>
    /// Adiciona um novo jogador ao jogo
    /// </summary>
    private void AdicionarNovoJogador(string jogadorId)
    {
        // Se o jogo estiver em Game Over e um novo jogador se conectar, reiniciar automaticamente
        if (_gameState.GameOver)
        {
            Console.WriteLine($"🔄 Novo jogador {jogadorId} conectou após Game Over. Reiniciando jogo automaticamente...");
            ReiniciarJogo();
        }
        
        // Posicionar naves lado a lado
        var numJogadores = _gameState.Naves.Count;
        var espacamento = LARGURA_TELA / (_gameState.Naves.Count + 2);
        var posicaoX = espacamento * (numJogadores + 1);
        
        var novaNave = new NaveData
        {
            JogadorId = jogadorId,
            Posicao = new Vector2(posicaoX, ALTURA_TELA - 60)
        };
        
        _gameState.Naves[jogadorId] = novaNave;
        Console.WriteLine($"🚀 Novo jogador adicionado: {jogadorId} na posição {novaNave.Posicao}");
    }

    /// <summary>
    /// Atualiza a posição da nave de um jogador específico baseada na entrada
    /// </summary>
    private void AtualizarNave(string jogadorId, PlayerInput input)
    {
        if (!_gameState.Naves.ContainsKey(jogadorId)) return;

        Vector2 direcao = Vector2.Zero;
        
        if (input.Esquerda) direcao.X -= 2;
        if (input.Direita) direcao.X += 2;
        if (input.Cima) direcao.Y -= 2;
        if (input.Baixo) direcao.Y += 2;

        if (direcao != Vector2.Zero)
        {
            direcao = Vector2.Normalize(direcao);
        }

        _gameState.Naves[jogadorId].Posicao += direcao * VELOCIDADE_NAVE;

        // Manter nave dentro da tela
        _gameState.Naves[jogadorId].Posicao = new Vector2(
            Math.Clamp(_gameState.Naves[jogadorId].Posicao.X, 10, LARGURA_TELA - 10),
            Math.Clamp(_gameState.Naves[jogadorId].Posicao.Y, 10, ALTURA_TELA - 10)
        );
    }

    /// <summary>
    /// Adiciona um novo tiro na posição da nave do jogador específico
    /// </summary>
    private void AdicionarTiro(string jogadorId)
    {
        if (!_gameState.Naves.ContainsKey(jogadorId)) return;

        var novoTiro = new TiroData
        {
            Posicao = _gameState.Naves[jogadorId].Posicao + new Vector2(0, -12),
            Velocidade = new Vector2(0, -VELOCIDADE_TIRO)
        };
        _gameState.Tiros.Add(novoTiro);
    }

    /// <summary>
    /// Atualiza posições dos tiros e remove os que saíram da tela
    /// </summary>
    private void AtualizarTiros()
    {
        for (int i = _gameState.Tiros.Count - 1; i >= 0; i--)
        {
            _gameState.Tiros[i].Posicao += _gameState.Tiros[i].Velocidade;
            
            // Remove tiros que saíram da tela
            if (_gameState.Tiros[i].Posicao.Y < -5)
            {
                _gameState.Tiros.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Atualiza posições dos asteroides
    /// </summary>
    private void AtualizarAsteroides()
    {
        foreach (var asteroide in _gameState.Asteroides)
        {
            asteroide.Posicao += asteroide.Velocidade;
        }

        // Remove asteroides que saíram da tela
        _gameState.Asteroides.RemoveAll(a => a.Posicao.Y > ALTURA_TELA + 50);
    }

    /// <summary>
    /// PROGRAMAÇÃO PARALELA: Processa colisões usando Parallel.ForEach para otimização
    /// 
    /// JUSTIFICATIVA PARA ESCOLHA DESTA TAREFA:
    /// 1. INTENSIDADE COMPUTACIONAL: Cálculo de distância entre objetos envolve operações matemáticas pesadas
    /// 2. FREQUÊNCIA: Executado a cada frame (60+ FPS), resultando em milhares de cálculos por segundo
    /// 3. ESCALABILIDADE: Complexidade O(n*m) - cresce exponencialmente com número de objetos
    /// 4. PARALELIZÁVEL: Cada verificação de colisão é independente, ideal para processamento paralelo
    /// 5. IMPACTO NA PERFORMANCE: Gargalo principal em jogos com muitos objetos
    /// </summary>
    private void ProcessarColisoesParalelo()
    {
        // Usar ConcurrentBag para thread-safety ao coletar resultados
        var tirosParaRemover = new ConcurrentBag<int>();
        var asteroidesParaRemover = new ConcurrentBag<int>();
        var pontuacaoAdicional = 0;

        // PARALLEL.FOREACH para colisões tiro × asteroide
        // Cada asteroide é processado em paralelo para verificar colisões com todos os tiros
        Parallel.ForEach(_gameState.Asteroides.Select((asteroide, index) => new { asteroide, index }), 
            asteroidePair =>
            {
                var asteroide = asteroidePair.asteroide;
                var asteroidIndex = asteroidePair.index;

                // Verificar colisão com todos os tiros
                for (int tiroIndex = 0; tiroIndex < _gameState.Tiros.Count; tiroIndex++)
                {
                    var tiro = _gameState.Tiros[tiroIndex];
                    
                    // Cálculo de distância (operação computacionalmente custosa)
                    float distancia = Vector2.Distance(tiro.Posicao, asteroide.Posicao);
                    
                    if (distancia < asteroide.Raio)
                    {
                        // Colisão detectada - marcar para remoção
                        tirosParaRemover.Add(tiroIndex);
                        asteroidesParaRemover.Add(asteroidIndex);
                        Interlocked.Add(ref pontuacaoAdicional, 10);
                        break; // Sair do loop de tiros para este asteroide
                    }
                }
            });

        // Aplicar remoções (deve ser feito sequencialmente para evitar problemas de índice)
        var tirosRemover = tirosParaRemover.Distinct().OrderByDescending(x => x).ToList();
        var asteroidesRemover = asteroidesParaRemover.Distinct().OrderByDescending(x => x).ToList();

        foreach (var index in tirosRemover)
        {
            if (index < _gameState.Tiros.Count)
                _gameState.Tiros.RemoveAt(index);
        }

        foreach (var index in asteroidesRemover)
        {
            if (index < _gameState.Asteroides.Count)
                _gameState.Asteroides.RemoveAt(index);
        }

        _gameState.Pontuacao += pontuacaoAdicional;

        // Verificar colisão nave × asteroide para todas as naves vivas
        foreach (var nave in _gameState.Naves.Values.Where(n => n.Viva))
        {
            foreach (var asteroide in _gameState.Asteroides)
            {
                float distancia = Vector2.Distance(nave.Posicao, asteroide.Posicao);
                if (distancia < asteroide.Raio + 8)
                {
                    // Marcar nave como morta ao invés de terminar o jogo
                    nave.Viva = false;
                    Console.WriteLine($"💀 Jogador {nave.JogadorId} foi destruído!");
                    break;
                }
            }
        }

        // Verificar se todas as naves estão mortas para terminar o jogo
        if (_gameState.Naves.Values.Any() && _gameState.Naves.Values.All(n => !n.Viva))
        {
            _gameState.GameOver = true;
            Console.WriteLine("🎮 Game Over - Todos os jogadores foram destruídos!");
        }
    }

    /// <summary>
    /// Spawna um novo asteroide em posição aleatória
    /// </summary>
    private void SpawnarAsteroide()
    {
        float x = _random.Next(LARGURA_TELA);
        float velY = 2f + (float)_random.NextDouble() * 2f; // 2-4 px/frame
        
        var novoAsteroide = new AsteroideData
        {
            Posicao = new Vector2(x, -30),
            Velocidade = new Vector2(0, velY),
            Raio = 25
        };
        
        _gameState.Asteroides.Add(novoAsteroide);
    }

    /// <summary>
    /// Retorna o estado atual do jogo
    /// </summary>
    public GameState ObterEstadoJogo()
    {
        lock (_lockObject)
        {
            return _gameState;
        }
    }

    /// <summary>
    /// Reinicia o jogo
    /// </summary>
    public void ReiniciarJogo()
    {
        lock (_lockObject)
        {
            _gameState.Tiros.Clear();
            _gameState.Asteroides.Clear();
            _gameState.Pontuacao = 0;
            _gameState.GameOver = false;
            _gameState.FrameCount = 0;
            
            // Ressuscitar e reposicionar todas as naves
            var jogadores = _gameState.Naves.Keys.ToList();
            _gameState.Naves.Clear();
            
            foreach (var jogadorId in jogadores)
            {
                AdicionarNovoJogador(jogadorId);
            }
            
            Console.WriteLine("🔄 Jogo reiniciado - Todas as naves ressuscitadas!");
        }
    }
}