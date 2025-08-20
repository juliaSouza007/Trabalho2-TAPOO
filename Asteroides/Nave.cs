using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;   // só para comparar com Keys.*
using Monogame.Processing;

namespace Asteroides;

class Nave
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

        /* mantém dentro da tela */
        Posicao.X = Math.Clamp(Posicao.X, HalfW, w - HalfW);
        Posicao.Y = Math.Clamp(Posicao.Y, HalfH, h - HalfH);
    }

    public void Desenhar(Processing g)
    {
        g.stroke(0);
        g.fill(100, 100, 255);
        
        g.triangle(Posicao.X - 8, Posicao.Y + 10,
                   Posicao.X, Posicao.Y - 10,
                   Posicao.X + 8, Posicao.Y + 10);
    }

    public Tiro Atirar() => new(Posicao + new Vector2(0, -12), new Vector2(0, -8));
}
