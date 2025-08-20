using System.Numerics;

namespace Cliente.Models;

/// <summary>
/// Representa o estado completo do jogo que será sincronizado entre cliente e servidor
/// </summary>
public class GameState
{
    public Dictionary<string, NaveData> Naves { get; set; } = new Dictionary<string, NaveData>();
    public List<TiroData> Tiros { get; set; } = new List<TiroData>();
    public List<AsteroideData> Asteroides { get; set; } = new List<AsteroideData>();
    public int Pontuacao { get; set; }
    public bool GameOver { get; set; }
    public int FrameCount { get; set; }
}

/// <summary>
/// Dados da nave para sincronização
/// </summary>
public class NaveData
{
    public string JogadorId { get; set; } = string.Empty;
    public Vector2 Posicao { get; set; }
    public bool Viva { get; set; } = true;
}

/// <summary>
/// Dados do tiro para sincronização
/// </summary>
public class TiroData
{
    public Vector2 Posicao { get; set; }
    public Vector2 Velocidade { get; set; }
}

/// <summary>
/// Dados do asteroide para sincronização
/// </summary>
public class AsteroideData
{
    public Vector2 Posicao { get; set; }
    public Vector2 Velocidade { get; set; }
    public float Raio { get; set; }
}

/// <summary>
/// Comandos de entrada do jogador
/// </summary>
public class PlayerInput
{
    public bool Esquerda { get; set; }
    public bool Direita { get; set; }
    public bool Cima { get; set; }
    public bool Baixo { get; set; }
    public bool Atirar { get; set; }
    public bool Reiniciar { get; set; }
    public string ClienteId { get; set; } = string.Empty;
}

/// <summary>
/// Mensagens trocadas entre cliente e servidor
/// </summary>
public class NetworkMessage
{
    public string Tipo { get; set; } = string.Empty;
    public string Dados { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
}