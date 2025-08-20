using Servidor.Network;

namespace Servidor;

/// <summary>
/// Programa principal do servidor
/// Demonstra comunicação TCP robusta com paralelismo para múltiplos clientes
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🎮 SERVIDOR DE JOGO ASTEROIDES");
        Console.WriteLine("================================");
        Console.WriteLine();
        Console.WriteLine("📋 FUNCIONALIDADES IMPLEMENTADAS:");
        Console.WriteLine("✅ Comunicação TCP robusta cliente-servidor");
        Console.WriteLine("✅ Paralelismo para múltiplos clientes simultâneos");
        Console.WriteLine("✅ Programação paralela (Parallel.ForEach) para cálculo de colisões");
        Console.WriteLine("✅ Tratamento de falhas e desconexões");
        Console.WriteLine();

        var servidor = new TcpGameServer(8080);

        // Configurar tratamento de Ctrl+C para parada limpa
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n🛑 Recebido sinal de parada...");
            servidor.PararServidor();
            Environment.Exit(0);
        };

        try
        {
            // Iniciar servidor de forma assíncrona
            await servidor.IniciarServidorAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro fatal no servidor: {ex.Message}");
            Console.WriteLine("Pressione qualquer tecla para sair...");
            Console.ReadKey();
        }
    }
}