using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;   // só para comparar com Keys.*
using Monogame.Processing;

namespace Asteroides;

class Asteroide
{
    Vector2 pos, vel;
    public float Raio { get; }

    public Asteroide(Vector2 p, Vector2 v, float r)
    { pos = p; vel = v; Raio = r; }

    public void Atualizar() => pos += vel;

    public void Desenhar(Processing g)
    {
        g.fill(150, 100, 100);
        g.stroke(200);
        g.ellipse(pos.X, pos.Y, Raio * 2, Raio * 2);
    }

    public bool Colide(Tiro t) => Vector2.Distance(t.Pos, pos) < Raio;
    public bool Colide(Nave n) => Vector2.Distance(n.Posicao, pos) < Raio + 8;
}
