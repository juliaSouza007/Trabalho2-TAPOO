using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Cliente.Models;
using Cliente.Network;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cliente
{
    /// <summary>
    /// Jogo Asteroides com lógica original + suporte opcional à rede
    /// Mantém toda a jogabilidade original funcionando offline
    /// </summary>
    public class JogoAsteroidesCliente : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch = null!;
        private Texture2D _pixelTexture = null!;
        private SpriteFont _gameFont = null!; // Fonte para o texto

        // ===== LÓGICA ORIGINAL DO JOGO =====
        private Nave _nave = null!;
        private readonly List<Tiro> _tiros = new();
        private readonly List<Asteroide> _asteroides = new();
        private readonly Random _rnd = new();
        private int _pontuacao;
        private bool _gameOver;
        private int _frameCount;

        // ===== CONTROLES =====
        private bool _esquerda, _direita, _cima, _baixo;
        private KeyboardState _keyboardStateAnterior;

        // ===== REDE (OPCIONAL) =====
        private TcpGameClient? _networkClient;
        private bool _modoRede = false;
        private bool _conectado = false;
        private string _statusConexao = "Modo Offline";
        private string _mensagemErro = string.Empty;
        private GameState? _estadoJogoRede;
        private string _meuClienteId = string.Empty;

        public JogoAsteroidesCliente()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Configurar tamanho da janela
            _graphics.PreferredBackBufferWidth = 800;
            _graphics.PreferredBackBufferHeight = 600;
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Inicializar jogo original
            _nave = new Nave(new Vector2(_graphics.PreferredBackBufferWidth / 2f, _graphics.PreferredBackBufferHeight - 60));

            // Tentar conectar à rede (opcional)
            _ = Task.Run(TentarConectarRede);
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Criar uma textura de pixel para desenhar formas básicas
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            // Carregar a fonte do jogo (assumindo que 'GameFont.spritefont' existe no Content)
            try
            {
                _gameFont = Content.Load<SpriteFont>("GameFont");
            }
            catch (Exception ex)
            {
                // Se a fonte não puder ser carregada, o jogo ainda funcionará, mas o texto não será exibido
                System.Diagnostics.Debug.WriteLine("Erro ao carregar a fonte: " + ex.Message);
                _gameFont = null!; // Garantir que a fonte seja nula para evitar erros de referência
            }

        }

        /// <summary>
        /// Tenta conectar à rede de forma opcional (não bloqueia o jogo)
        /// </summary>
        private async Task TentarConectarRede()
        {
            try
            {
                _networkClient = new TcpGameClient("localhost", 8080);
                _networkClient.OnErrorOccurred += erro => _mensagemErro = erro;
                _networkClient.OnConnectionStatusChanged += (status) => _statusConexao = status;
                _networkClient.OnGameStateReceived += (gameState) => _estadoJogoRede = gameState;
                _networkClient.OnClientIdReceived += (clienteId) => _meuClienteId = clienteId;

                _conectado = await _networkClient.ConectarAsync();
                if (_conectado)
                {
                    _modoRede = true;
                    _statusConexao = "Conectado - Modo Online";
                }
            }
            catch
            {
                _statusConexao = "Modo Offline";
                _modoRede = false;
            }
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboardState = Keyboard.GetState();
            _frameCount++;

            // Verificar ESC para sair
            if (keyboardState.IsKeyDown(Keys.Escape))
                Exit();

            // Verificar GameOver (local ou do servidor)
            bool gameOverAtual = _gameOver || (_modoRede && _estadoJogoRede?.GameOver == true);

            // Verificar R para tentar reconectar (apenas se não estiver em modo rede e não estiver em GameOver)
            if (keyboardState.IsKeyDown(Keys.R) && !_keyboardStateAnterior.IsKeyDown(Keys.R))
            {
                if (!_modoRede && !gameOverAtual)
                {
                    _ = Task.Run(TentarConectarRede);
                }
            }

            if (!gameOverAtual)
            {
                // Processar input
                ProcessarTeclas(keyboardState);

                // Se estiver em modo rede, enviar input para o servidor
                if (_modoRede && _conectado && _networkClient != null)
                {
                    var input = new PlayerInput
                    {
                        Esquerda = _esquerda,
                        Direita = _direita,
                        Cima = _cima,
                        Baixo = _baixo,
                        Atirar = keyboardState.IsKeyDown(Keys.Space) && !_keyboardStateAnterior.IsKeyDown(Keys.Space),
                        Reiniciar = keyboardState.IsKeyDown(Keys.R) && !_keyboardStateAnterior.IsKeyDown(Keys.R) && gameOverAtual
                    };
                    _ = Task.Run(() => _networkClient.EnviarInputAsync(input));
                }
                else
                {
                    // Atualizar jogo local (lógica original)
                    AtualizarJogo();
                }
            }

            _keyboardStateAnterior = keyboardState;
            base.Update(gameTime);
        }

        /// <summary>
        /// Processa input do teclado (lógica original)
        /// </summary>
        private void ProcessarTeclas(KeyboardState keyboardState)
        {
            _esquerda = false;
            _direita = false;
            _cima = false;
            _baixo = false;

            // Teclas de movimento
            if (keyboardState.IsKeyDown(Keys.A) || keyboardState.IsKeyDown(Keys.Left)) _esquerda = true;
            if (keyboardState.IsKeyDown(Keys.D) || keyboardState.IsKeyDown(Keys.Right)) _direita = true;
            if (keyboardState.IsKeyDown(Keys.W) || keyboardState.IsKeyDown(Keys.Up)) _cima = true;
            if (keyboardState.IsKeyDown(Keys.S) || keyboardState.IsKeyDown(Keys.Down)) _baixo = true;

            // Atirar (detectar pressionamento único)
            if (keyboardState.IsKeyDown(Keys.Space) && !_keyboardStateAnterior.IsKeyDown(Keys.Space))
            {
                _tiros.Add(_nave.Atirar());
            }
        }

        /// <summary>
        /// Atualiza a lógica do jogo (versão original adaptada)
        /// </summary>
        private void AtualizarJogo()
        {
            // Atualizar nave
            _nave.Atualizar(_esquerda, _direita, _cima, _baixo, _graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight);

            // Atualizar tiros
            for (int i = _tiros.Count - 1; i >= 0; i--)
            {
                var tiro = _tiros[i];
                tiro.Atualizar();
                if (tiro.ForaDaTela(_graphics.PreferredBackBufferHeight))
                {
                    _tiros.RemoveAt(i);
                }
            }

            // Atualizar asteroides
            for (int i = _asteroides.Count - 1; i >= 0; i--)
            {
                var asteroide = _asteroides[i];
                asteroide.Atualizar();

                // Colisão tiro × asteroide
                for (int j = _tiros.Count - 1; j >= 0; j--)
                {
                    if (asteroide.Colide(_tiros[j]))
                    {
                        _pontuacao += 10;
                        _tiros.RemoveAt(j);
                        _asteroides.RemoveAt(i);
                        goto proximoAsteroide;
                    }
                }

                // Colisão nave × asteroide
                if (asteroide.Colide(_nave))
                {
                    _gameOver = true;
                }

            proximoAsteroide:;
            }

            // Spawnar novo asteroide a cada 40 quadros
            if (_frameCount % 40 == 0)
            {
                _asteroides.Add(NovoAsteroide());
            }
        }

        /// <summary>
        /// Cria um novo asteroide (lógica original)
        /// </summary>
        private Asteroide NovoAsteroide()
        {
            float x = _rnd.Next(_graphics.PreferredBackBufferWidth);
            float velY = 2f + (float)_rnd.NextDouble() * 2f; // 2–4 px/frame
            return new Asteroide(new Vector2(x, -30), new Vector2(0, velY), 25);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            _spriteBatch.Begin();

            // Verificar GameOver (local ou do servidor)
            bool gameOverAtual = _gameOver || (_modoRede && _estadoJogoRede?.GameOver == true);

            if (!gameOverAtual)
            {
                if (_modoRede && _estadoJogoRede != null)
                {
                    // Modo rede: desenhar apenas as naves vivas dos jogadores conectados
                    foreach (var nave in _estadoJogoRede.Naves.Values)
                    {
                        // Só desenhar se a nave estiver viva
                        if (nave.Viva)
                        {
                            // Verificar se é a nave do jogador atual
                            bool isMinhanave = nave.JogadorId == _meuClienteId;
                            DesenharNaveDetalhada(nave.Posicao, 0f, isMinhanave, nave.Viva);
                        }
                    }

                    // Desenhar tiros do servidor
                    foreach (var tiro in _estadoJogoRede.Tiros)
                    {
                        DesenharPonto(tiro.Posicao, Color.Yellow, 5);
                    }

                    // Desenhar asteroides do servidor
                    foreach (var asteroide in _estadoJogoRede.Asteroides)
                    {
                        DesenharCirculo(asteroide.Posicao, asteroide.Raio, Color.Brown);
                    }
                }
                else
                {
                    // Modo local: desenhar nave local
                    DesenharNave();

                    // Desenhar tiros locais
                    foreach (var tiro in _tiros)
                    {
                        DesenharTiro(tiro);
                    }

                    // Desenhar asteroides locais
                    foreach (var asteroide in _asteroides)
                    {
                        DesenharAsteroide(asteroide);
                    }
                }
            }
            else
            {
                // Game Over
                DesenharTexto("GAME OVER", new Vector2(400, 200), Color.Red, 2.0f);
                if (_modoRede)
                {
                    DesenharTexto("Pressione ESC para sair", new Vector2(400, 280), Color.White);
                }
                else
                {
                    DesenharTexto("Pressione ESC para sair", new Vector2(400, 250), Color.White);
                }
            }

            // Desenhar pontuação no topo centralizada
            int pontuacaoAtual = (_modoRede && _estadoJogoRede != null) ? _estadoJogoRede.Pontuacao : _pontuacao;
            string textoPontuacao = $"PONTUACAO: {pontuacaoAtual}";
            float larguraTela = _graphics.PreferredBackBufferWidth;
            // A nova função DesenharTexto já centraliza, então passamos o centro X
            DesenharTexto(textoPontuacao, new Vector2(larguraTela / 2, 20), Color.Yellow, 1.5f);

            // Desenhar status da conexão
            DesenharTexto($"Status: {_statusConexao}", new Vector2(150, _graphics.PreferredBackBufferHeight - 30), Color.LightGreen);

            // Mostrar ID do cliente (se conectado)
            if (!string.IsNullOrEmpty(_meuClienteId))
            {
                DesenharTexto($"ID: {_meuClienteId}", new Vector2(150, _graphics.PreferredBackBufferHeight - 70), Color.LightBlue, 0.8f);
            }

            // Mostrar erro, se houver
            if (!string.IsNullOrEmpty(_mensagemErro))
            {
                DesenharTexto($"Erro: {_mensagemErro}", new Vector2(150, _graphics.PreferredBackBufferHeight - 90), Color.OrangeRed, 0.8f);
            }

            // Instruções
            if (!_modoRede)
            {
                DesenharTexto("Pressione R para tentar conectar online", new Vector2(150, _graphics.PreferredBackBufferHeight - 50), Color.Yellow);
            }

            // Indicador de jogadores mortos no canto inferior direito
            if (_modoRede && _estadoJogoRede != null)
            {
                int jogadoresMortos = 0;
                foreach (var nave in _estadoJogoRede.Naves.Values)
                {
                    if (!nave.Viva)
                    {
                        jogadoresMortos++;
                    }
                }

                if (jogadoresMortos > 0)
                {
                    Vector2 posicaoIndicador = new Vector2(_graphics.PreferredBackBufferWidth - 120, _graphics.PreferredBackBufferHeight - 60);

                    // Desenhar X vermelho
                    DesenharLinha(posicaoIndicador + new Vector2(-8, -8), posicaoIndicador + new Vector2(8, 8), Color.Red, 3);
                    DesenharLinha(posicaoIndicador + new Vector2(-8, 8), posicaoIndicador + new Vector2(8, -8), Color.Red, 3);

                    // Texto indicando quantos jogadores morreram
                    string textoMortos = jogadoresMortos == 1 ? "1 JOGADOR MORTO" : $"{jogadoresMortos} JOGADORES MORTOS";
                    DesenharTexto(textoMortos, posicaoIndicador + new Vector2(0, 20), Color.Red, 0.8f);
                }
            }

            _spriteBatch.End();

            base.Draw(gameTime);
        }

        // ===== MÉTODOS DE DESENHO =====

        private void DesenharNave()
        {
            var pos = _nave.Posicao;
            // Desenhar nave melhorada com mais detalhes (sempre como "minha nave" no modo local)
            DesenharNaveDetalhada(pos, 0f, true, true);
        }

        private void DesenharTiro(Tiro tiro)
        {
            DesenharPonto(tiro.Pos, Color.Yellow, 5);
        }

        private void DesenharAsteroide(Asteroide asteroide)
        {
            DesenharCirculo(asteroide.Posicao, asteroide.Raio, Color.Brown);
        }

        private void DesenharNaveDetalhada(Vector2 centro, float rotacao = 0f, bool isMinhanave = false, bool viva = true)
        {
            // Definir cores baseadas se é a nave do jogador ou não e se está viva
            Color corPrincipal, corCockpit, corCentro;

            if (!viva)
            {
                // Nave morta: cores escuras/transparentes
                corPrincipal = Color.DarkRed;
                corCockpit = Color.Gray;
                corCentro = Color.DarkGray;
            }
            else
            {
                // Nave viva: cores normais
                corPrincipal = isMinhanave ? Color.Lime : Color.Cyan;
                corCockpit = isMinhanave ? Color.Yellow : Color.White;
                corCentro = isMinhanave ? Color.Green : Color.LightBlue;
            }

            // Corpo principal da nave (triângulo maior)
            var vertices = new Vector2[]
            {
                centro + new Vector2(0, -12),   // topo
                centro + new Vector2(-10, 12),  // esquerda
                centro + new Vector2(10, 12)    // direita
            };

            // Aplicar rotação se necessário (para futuras implementações)
            // Por enquanto mantemos simples sem rotação visual

            // Desenhar contorno da nave
            for (int i = 0; i < vertices.Length; i++)
            {
                var inicio = vertices[i];
                var fim = vertices[(i + 1) % vertices.Length];
                DesenharLinha(inicio, fim, corPrincipal, 2);
            }

            // Cockpit (parte superior)
            DesenharPonto(centro + new Vector2(0, -8), corCockpit, 4);
            DesenharPonto(centro + new Vector2(0, -4), corCockpit, 3);

            // Motores (parte traseira)
            DesenharPonto(centro + new Vector2(-6, 10), Color.Orange, 3);
            DesenharPonto(centro + new Vector2(6, 10), Color.Orange, 3);
            DesenharPonto(centro + new Vector2(-6, 12), Color.Red, 2);
            DesenharPonto(centro + new Vector2(6, 12), Color.Red, 2);

            // Centro da nave
            DesenharPonto(centro, corCentro, 2);
        }

        private void DesenharLinha(Vector2 inicio, Vector2 fim, Color cor, int espessura)
        {
            // Desenhar linha usando pontos
            var direcao = fim - inicio;
            float distancia = direcao.Length();
            if (distancia > 0)
            {
                direcao.Normalize();
                for (float i = 0; i <= distancia; i += 1.5f)
                {
                    var ponto = inicio + direcao * i;
                    DesenharPonto(ponto, cor, espessura);
                }
            }
        }

        private void DesenharTriangulo(Vector2 centro, Color cor)
        {
            // Desenhar triângulo simples usando retângulos pequenos
            var vertices = new Vector2[]
            {
                centro + new Vector2(0, -10),   // topo
                centro + new Vector2(-8, 10),   // esquerda
                centro + new Vector2(8, 10)     // direita
            };

            foreach (var vertex in vertices)
            {
                DesenharPonto(vertex, cor, 3);
            }
        }

        private void DesenharCirculo(Vector2 centro, float raio, Color cor)
        {
            // Desenhar círculo usando pontos
            int pontos = 16;
            for (int i = 0; i < pontos; i++)
            {
                float angulo = (float)(2 * Math.PI * i / pontos);
                var ponto = centro + new Vector2(
                    (float)(Math.Cos(angulo) * raio),
                    (float)(Math.Sin(angulo) * raio)
                );
                DesenharPonto(ponto, cor, 2);
            }
        }

        private void DesenharPonto(Vector2 posicao, Color cor, int tamanho)
        {
            var destino = new Rectangle((int)posicao.X - tamanho / 2, (int)posicao.Y - tamanho / 2, tamanho, tamanho);
            _spriteBatch.Draw(_pixelTexture, destino, cor);
        }

        private void DesenharTexto(string texto, Vector2 posicao, Color cor, float escala = 1.0f)
        {
            if (_gameFont == null) return; // Não desenha se a fonte não foi carregada

            // Medir o texto para centralizar corretamente
            Vector2 tamanhoTexto = _gameFont.MeasureString(texto) * escala;
            Vector2 posicaoCentralizada = new Vector2(posicao.X - tamanhoTexto.X / 2, posicao.Y);

            // Desenhar o texto usando a fonte carregada
            _spriteBatch.DrawString(_gameFont, texto, posicaoCentralizada, cor, 0, Vector2.Zero, escala, SpriteEffects.None, 0);
        }
    }

    // ===== CLASSES DO JOGO ORIGINAL (ADAPTADAS) =====

    public class Nave
    {
        public Vector2 Posicao;
        const float Vel = 4f;
        const float HalfW = 10, HalfH = 10;

        public Nave(Vector2 start) => Posicao = start;

        public void Atualizar(bool left, bool right, bool up, bool down, int w, int h)
        {
            Vector2 dir = Vector2.Zero;
            if (left) dir.X -= 2;
            if (right) dir.X += 2;
            if (up) dir.Y -= 2;
            if (down) dir.Y += 2;

            if (dir != Vector2.Zero) dir.Normalize();
            Posicao += dir * Vel;

            // Mantém dentro da tela
            Posicao.X = Math.Clamp(Posicao.X, HalfW, w - HalfW);
            Posicao.Y = Math.Clamp(Posicao.Y, HalfH, h - HalfH);
        }

        public Tiro Atirar() => new(Posicao + new Vector2(0, -12), new Vector2(0, -8));
    }

    public class Tiro
    {
        Vector2 _pos, _vel;
        public Tiro(Vector2 p, Vector2 v) { _pos = p; _vel = v; }

        public void Atualizar() => _pos += _vel;
        public bool ForaDaTela(int h) => _pos.Y < -5;
        public Vector2 Pos => _pos;
    }

    public class Asteroide
    {
        Vector2 _pos, _vel;
        public float Raio { get; }
        public Vector2 Posicao => _pos;

        public Asteroide(Vector2 p, Vector2 v, float r)
        { _pos = p; _vel = v; Raio = r; }

        public void Atualizar() => _pos += _vel;
        public bool Colide(Tiro t) => Vector2.Distance(t.Pos, _pos) < Raio;
        public bool Colide(Nave n) => Vector2.Distance(n.Posicao, _pos) < Raio + 8;
    }
}