using Cliente;

namespace Cliente;

/// <summary>
/// Programa principal do cliente
/// Demonstra comunicação TCP assíncrona para manter responsividade da aplicação
/// </summary>
class Program
{
    [STAThread]
    static void Main()
    {
        Console.WriteLine("🎮 CLIENTE DE JOGO ASTEROIDES");
        Console.WriteLine("==============================");
        Console.WriteLine();
        Console.WriteLine("📋 FUNCIONALIDADES IMPLEMENTADAS:");
        Console.WriteLine("✅ Comunicação TCP assíncrona (async/await)");
        Console.WriteLine("✅ Interface responsiva sem bloqueios");
        Console.WriteLine("✅ Reconexão automática em caso de falha");
        Console.WriteLine("✅ Tratamento robusto de erros de rede");
        Console.WriteLine();
        Console.WriteLine("🎯 CONTROLES:");
        Console.WriteLine("• WASD ou Setas: Movimento");
        Console.WriteLine("• Espaço: Atirar");
        Console.WriteLine("• ESC: Sair");
        Console.WriteLine();
        Console.WriteLine("🚀 Iniciando cliente...");

        try
        {
            using var jogo = new JogoAsteroidesCliente();
            jogo.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro fatal no cliente: {ex.Message}");
            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }
    }
}