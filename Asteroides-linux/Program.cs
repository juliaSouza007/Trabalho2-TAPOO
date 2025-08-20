using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;   // só para comparar com Keys.*
using Monogame.Processing;
using Asteroides;

public class JogoAsteroides : Processing
{
    /* --------------------- estado de jogo --------------------- */
    // estado do jogo: nave, tiros, asteroides, pontuação
    Nave nave;
    readonly List<Tiro> tiros = new();
    readonly List<Asteroide> asteroides = new();
    readonly Random rnd = new();

    int pontuacao;

    /* --------------------- teclado (flags) -------------------- */
    bool esquerda, direita, cima, baixo;

    /* ===================== ciclo de vida ====================== */
    public override void Setup()
    {

        size(800, 600);
        nave = new Nave(new Vector2(width / 2f, height - 60));
    }

    public override void Draw()
    {
        background(0);

        /* ----- nave ----- */
        Teclas();
        nave.Atualizar(esquerda, direita, cima, baixo, width, height);
        nave.Desenhar(this);

        /* ----- tiros ----- */
        for (int i = tiros.Count - 1; i >= 0; i--)
        {
            var t = tiros[i];
            t.Atualizar();
            t.Desenhar(this);
            if (t.ForaDaTela(height)) tiros.RemoveAt(i);
        }

        /* ----- asteroides ----- */
        for (int i = asteroides.Count - 1; i >= 0; i--)
        {
            var a = asteroides[i];
            a.Atualizar();
            a.Desenhar(this);

            /* colisão tiro × asteroide */
            for (int j = tiros.Count - 1; j >= 0; j--)
            {
                if (!a.Colide(tiros[j])) continue;
                pontuacao += 10;
                tiros.RemoveAt(j);
                asteroides.RemoveAt(i);
                goto proximoAst;        // sai dos dois loops
            }

            /* colisão nave × asteroide */
            if (a.Colide(nave))
            {
                fill(255, 0, 0);
                textSize(48);
                //textAlign(CENTER, CENTER);
                text("GAME OVER", width / 2f + -4*48, height / 2f);
                noLoop();
            }

        proximoAst:;
        }

        /* spawna novo asteroide a cada 40 quadros */
        if (frameCount % 40 == 0) asteroides.Add(NovoAsteroide());

        /* ----- placar ----- */
        fill(255);
        textSize(20);
        text($"Pontuacao: {pontuacao}", 10, 10);
    }

    /* ====================== input ============================= */
    public void Teclas() 
    {
        esquerda = false;
        direita = false;
        cima = false;
        baixo = false;

        if (!keyPressed) return;  // nada pressionado

        /* tecla “única” (letras) */
        switch (char.ToUpperInvariant(key))
        {
            case 'A': esquerda = true; break;
            case 'D': direita = true; break;
            case 'W': cima = true; break;
            case 'S': baixo = true; break;
        }

        /* teclas especiais (setas, espaço, esc) */
        switch (keyCode)
        {
            case Keys.Left: esquerda = true; break;
            case Keys.Right: direita = true; break;
            case Keys.Up: cima = true; break;
            case Keys.Down: baixo = true; break;

            case Keys.Space: tiros.Add(nave.Atirar()); break;
            case Keys.Escape: Exit(); break;
        }
    }

    public override void KeyReleased(Keys pkey)
    {
        switch (char.ToUpperInvariant(key))
        {
            case 'A': esquerda = false; break;
            case 'D': direita = false; break;
            case 'W': cima = false; break;
            case 'S': baixo = false; break;
        }

        switch (keyCode)
        {
            case Keys.Left: esquerda = false; break;
            case Keys.Right: direita = false; break;
            case Keys.Up: cima = false; break;
            case Keys.Down: baixo = false; break;
        }
    }

    /* ====================== fábrica de asteroides ============= */
    Asteroide NovoAsteroide()
    {
        float x = rnd.Next(width);
        float velY = 2f + (float)rnd.NextDouble() * 2f;   // 2–4 px/frame
        return new Asteroide(new Vector2(x, -30), new Vector2(0, velY), 25);
    }

    /* ====================== entry-point ======================= */
    [STAThread]
    static void Main()
    {
        using var jogo = new JogoAsteroides();
        jogo.Run();
    }
}
