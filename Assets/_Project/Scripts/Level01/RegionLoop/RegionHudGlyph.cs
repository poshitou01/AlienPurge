using UnityEngine;
using UnityEngine.UI;

namespace AlienPurge.Level01.RegionLoop
{
    // Crisp UI geometry: right-facing arrow, dash chevrons, jump and pulse. No projectile texture.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RegionHudGlyph : MaskableGraphic
    {
        public enum Shape { Arrow, Dash, Jump, Pulse }
        public Shape shape;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (shape == Shape.Pulse)
            {
                for (int i=0;i<32;i++)
                {
                    float a=i*Mathf.PI/16, b=(i+1)*Mathf.PI/16;
                    Quad(vh, new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.85f, new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.85f,
                        new Vector2(Mathf.Cos(b),Mathf.Sin(b))*.65f, new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.65f);
                }
                Quad(vh,new Vector2(-.12f,-.34f),new Vector2(.12f,-.34f),new Vector2(.12f,.34f),new Vector2(-.12f,.34f));
                Quad(vh,new Vector2(-.34f,-.12f),new Vector2(.34f,-.12f),new Vector2(.34f,.12f),new Vector2(-.34f,.12f));
            }
            else if (shape == Shape.Arrow)
            {
                Quad(vh,new Vector2(-.9f,-.18f),new Vector2(.08f,-.18f),new Vector2(.08f,.18f),new Vector2(-.9f,.18f));
                Triangle(vh,new Vector2(-.02f,-.65f),new Vector2(.92f,0),new Vector2(-.02f,.65f));
            }
            else
            {
                for(int i=0;i<2;i++)
                {
                    float x=-.8f+i*.75f;
                    Quad(vh,Rotate(new Vector2(x,-.65f)),Rotate(new Vector2(x+.28f,-.65f)),Rotate(new Vector2(x+.8f,0)),Rotate(new Vector2(x+.48f,0)));
                    Quad(vh,Rotate(new Vector2(x+.48f,0)),Rotate(new Vector2(x+.8f,0)),Rotate(new Vector2(x+.28f,.65f)),Rotate(new Vector2(x,.65f)));
                }
            }
        }
        Vector2 Rotate(Vector2 p) => shape == Shape.Jump ? new Vector2(-p.y,p.x) : p;
        void Vertex(VertexHelper vh, Vector2 p)
        {
            var r=GetPixelAdjustedRect(); vh.AddVert(r.center + Vector2.Scale(p,r.size)*.5f, color, Vector2.zero);
        }
        void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        { int n=vh.currentVertCount;Vertex(vh,a);Vertex(vh,b);Vertex(vh,c);Vertex(vh,d);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3); }
        void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c)
        { int n=vh.currentVertCount;Vertex(vh,a);Vertex(vh,b);Vertex(vh,c);vh.AddTriangle(n,n+1,n+2); }
    }
}
