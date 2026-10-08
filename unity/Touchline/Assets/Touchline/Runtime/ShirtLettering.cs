using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Touchline
{
    // Police à traits générée en code (aucun fichier de police) : chaque caractère
    // est une suite de lignes brisées dans une boîte de hauteur 1 (y vers le haut),
    // dessinées avec des extrémités arrondies. Sert au flocage des maillots.
    public static class ShirtLettering
    {
        public const float Spacing=.2f; // espace entre les bords de deux caractères, en hauteurs de glyphe
        static Dictionary<char,(float width,float[][] strokes)> glyphs;

        public static string Surname(string name)
        {
            if(string.IsNullOrWhiteSpace(name))return "";
            var parts=name.Trim().Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            var raw=parts.Length==1?parts[0]:string.Join(" ",parts,1,parts.Length-1);
            return Printable(raw);
        }
        // Majuscules sans accents, limitées aux caractères de la police.
        public static string Printable(string text)
        {
            var builder=new StringBuilder();
            foreach(char c in (text??"").Normalize(NormalizationForm.FormD)){
                if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;
                var upper=char.ToUpperInvariant(c);
                switch(upper){case 'Ø':upper='O';break;case 'Ł':upper='L';break;case 'Đ':case 'Ð':upper='D';break;
                    case 'Æ':builder.Append("AE");continue;case 'Œ':builder.Append("OE");continue;case 'ß':builder.Append("SS");continue;case 'Þ':builder.Append("TH");continue;}
                if(Glyph(upper).strokes!=null)builder.Append(upper);
            }
            return builder.ToString().Trim();
        }

        public static (float width,float[][] strokes) Glyph(char c){Build();return glyphs.TryGetValue(c,out var g)?g:(0f,null);}
        public static float Measure(string text,float spacing){float w=0;foreach(char c in text){var g=Glyph(c);if(g.strokes!=null)w+=g.width+spacing;}return Math.Max(0,w-spacing);}

        // Dessine le texte dans une zone (px) d'un tampon de distances : distance[i]
        // reçoit la distance minimale (px) au tracé le plus proche.
        public static void Trace(float[] distance,int stride,int rows,string text,float left,float bottom,float height,float scaleX,float spacing,float reach)
        {
            float x=left;
            foreach(char c in text){var g=Glyph(c);if(g.strokes==null)continue;
                foreach(var stroke in g.strokes)for(int i=0;i+3<stroke.Length;i+=2)
                    Segment(distance,stride,rows,x+stroke[i]*height*scaleX,bottom+stroke[i+1]*height,x+stroke[i+2]*height*scaleX,bottom+stroke[i+3]*height,reach);
                x+=(g.width+spacing)*height*scaleX;}
        }
        static void Segment(float[] distance,int stride,int rows,float ax,float ay,float bx,float by,float reach)
        {
            int x0=Math.Max(0,(int)Math.Floor(Math.Min(ax,bx)-reach)),x1=Math.Min(stride-1,(int)Math.Ceiling(Math.Max(ax,bx)+reach));
            int y0=Math.Max(0,(int)Math.Floor(Math.Min(ay,by)-reach)),y1=Math.Min(rows-1,(int)Math.Ceiling(Math.Max(ay,by)+reach));
            float dx=bx-ax,dy=by-ay,length=dx*dx+dy*dy;
            for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++){
                float px=x+.5f-ax,py=y+.5f-ay;float t=length<1e-6f?0:Math.Max(0,Math.Min(1,(px*dx+py*dy)/length));
                float ex=px-t*dx,ey=py-t*dy;float d=(float)Math.Sqrt(ex*ex+ey*ey);int i=y*stride+x;if(d<distance[i])distance[i]=d;}
        }

        // Arc d'ellipse (degrés, sens trigonométrique si a1 > a0), pas de 10°.
        static float[] Arc(float cx,float cy,float rx,float ry,float a0,float a1,params float[] tail)=>Arc(null,cx,cy,rx,ry,a0,a1,tail);
        static float[] Arc(float[] head,float cx,float cy,float rx,float ry,float a0,float a1,params float[] tail)
        {
            var list=new List<float>();if(head!=null)list.AddRange(head);
            int steps=Math.Max(2,(int)Math.Ceiling(Math.Abs(a1-a0)/10));
            for(int i=0;i<=steps;i++){double a=(a0+(a1-a0)*i/steps)*Math.PI/180;list.Add(cx+rx*(float)Math.Cos(a));list.Add(cy+ry*(float)Math.Sin(a));}
            list.AddRange(tail);return list.ToArray();
        }
        static float[] L(params float[] points)=>points;
        static void Add(char c,float width,params float[][] strokes)=>glyphs[c]=(width,strokes);

        static void Build()
        {
            if(glyphs!=null)return;glyphs=new Dictionary<char,(float,float[][])>();
            // Chiffres (largeur 0,6).
            Add('0',.6f,Arc(.3f,.5f,.3f,.5f,90,450));
            Add('1',.6f,L(.1f,.78f,.36f,1,.36f,0));
            Add('2',.6f,Arc(.3f,.72f,.29f,.28f,165,-38,.02f,0,.6f,0));
            Add('3',.6f,Arc(.3f,.76f,.27f,.24f,155,-90),Arc(.3f,.27f,.3f,.27f,90,-155));
            Add('4',.62f,L(.46f,0,.46f,1,0,.3f,.62f,.3f));
            Add('5',.6f,Arc(L(.56f,1,.08f,1,.05f,.56f),.3f,.32f,.3f,.32f,128,-150));
            Add('6',.6f,Arc(.3f,.31f,.3f,.31f,0,360),Arc(.66f,.36f,.66f,.64f,95,180));
            Add('7',.6f,L(0,1,.6f,1,.18f,0));
            Add('8',.6f,Arc(.3f,.76f,.25f,.24f,-90,270),Arc(.3f,.27f,.3f,.27f,90,450));
            Add('9',.6f,Arc(.3f,.69f,.3f,.31f,0,360),Arc(-.06f,.64f,.66f,.64f,-85,0));
            // Lettres capitales.
            Add('A',.64f,L(0,0,.32f,1,.64f,0),L(.11f,.33f,.53f,.33f));
            Add('B',.56f,Arc(L(0,0,0,1,.3f,1),.3f,.76f,.22f,.24f,90,-90,0,.52f),Arc(L(.3f,.52f),.32f,.26f,.24f,.26f,90,-90,0,0));
            Add('C',.6f,Arc(.33f,.5f,.31f,.5f,50,310));
            Add('D',.6f,Arc(L(0,0,0,1,.18f,1),.18f,.5f,.4f,.5f,90,-90,0,0));
            Add('E',.54f,L(.54f,1,0,1,0,0,.54f,0),L(0,.52f,.44f,.52f));
            Add('F',.52f,L(.52f,1,0,1,0,0),L(0,.52f,.42f,.52f));
            Add('G',.64f,Arc(.33f,.5f,.31f,.5f,45,345,.64f,.45f,.38f,.45f));
            Add('H',.6f,L(0,0,0,1),L(.6f,0,.6f,1),L(0,.52f,.6f,.52f));
            Add('I',.04f,L(.02f,0,.02f,1));
            Add('J',.5f,Arc(L(.48f,1),.25f,.3f,.23f,.3f,0,-180));
            Add('K',.58f,L(0,0,0,1),L(.58f,1,0,.38f),L(.2f,.58f,.6f,0));
            Add('L',.52f,L(0,1,0,0,.52f,0));
            Add('M',.72f,L(0,0,0,1,.36f,.32f,.72f,1,.72f,0));
            Add('N',.6f,L(0,0,0,1,.6f,0,.6f,1));
            Add('O',.66f,Arc(.33f,.5f,.33f,.5f,90,450));
            Add('P',.56f,Arc(L(0,0,0,1,.3f,1),.3f,.74f,.26f,.26f,90,-90,0,.48f));
            Add('Q',.68f,Arc(.33f,.5f,.33f,.5f,90,450),L(.4f,.24f,.7f,-.04f));
            Add('R',.58f,Arc(L(0,0,0,1,.3f,1),.3f,.74f,.26f,.26f,90,-90,0,.48f),L(.28f,.48f,.6f,0));
            Add('S',.58f,Arc(.29f,.75f,.27f,.25f,15,270),Arc(.29f,.25f,.29f,.25f,90,-195));
            Add('T',.6f,L(0,1,.6f,1),L(.3f,1,.3f,0));
            Add('U',.6f,Arc(L(0,1),.3f,.32f,.3f,.32f,180,360,.6f,1));
            Add('V',.62f,L(0,1,.31f,0,.62f,1));
            Add('W',.84f,L(0,1,.2f,0,.42f,.7f,.64f,0,.84f,1));
            Add('X',.6f,L(0,1,.6f,0),L(0,0,.6f,1));
            Add('Y',.62f,L(0,1,.31f,.5f,.62f,1),L(.31f,.5f,.31f,0));
            Add('Z',.58f,L(0,1,.58f,1,0,0,.58f,0));
            Add('-',.34f,L(.04f,.48f,.3f,.48f));
            Add('\'',.04f,L(.02f,1,.02f,.74f));
            Add('.',.04f,L(.02f,0,.02f,.02f));
            Add(' ',.3f);
        }
    }
}
