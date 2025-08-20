using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;   // só para comparar com Keys.*
using Monogame.Processing;

namespace Asteroides;

class Tiro
{
    Vector2 pos, vel;
    public Tiro(Vector2 p, Vector2 v) { pos = p; vel = v; }

    public void Atualizar() => pos += vel;

    public void Desenhar(Processing g)
    {
        g.strokeWeight(5);
        g.stroke(255, 255, 0);
        g.point(pos.X, pos.Y);
        g.strokeWeight(1);
    }

    public bool ForaDaTela(int h) => pos.Y < -5;
    public Vector2 Pos => pos;
}

