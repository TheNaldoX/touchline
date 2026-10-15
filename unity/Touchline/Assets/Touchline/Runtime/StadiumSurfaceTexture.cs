using UnityEngine;

namespace Touchline
{
    public static class StadiumSurfaceTexture
    {
        public const int Size=256;
        public const float TileMetres=2f; // shared texel density on horizontal and vertical concrete faces
        public static Texture2D Concrete()
        {
            var texture=new Texture2D(Size,Size,TextureFormat.RGB24,true){name="Stadium concrete",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color32[Size*Size];
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++){
                uint hash=unchecked((uint)x*73856093u^(uint)y*19349663u);hash^=hash>>13;hash=unchecked(hash*1274126177u);hash^=hash>>16;
                float grain=(hash&65535)/65535f;
                float aggregate=Mathf.Sin(x*Mathf.PI*2/Size)*Mathf.Sin(y*Mathf.PI*4/Size);
                float edge=Mathf.Min(Mathf.Min(x,Size-1-x),Mathf.Min(y,Size-1-y));
                float joint=1-Mathf.SmoothStep(0,1,edge/2f); // approximately 16mm panel joint, softened by mipmaps
                float shade=.90f+.07f*grain+.025f*aggregate-.12f*joint-(grain<.035f?.09f:0);
                pixels[y*Size+x]=new Color(shade,shade*.99f,shade*.96f,1);
            }
            texture.SetPixels32(pixels);texture.Apply(true,true);return texture;
        }
        public static void MapConcrete(Mesh mesh)
        {
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++){
                var p=vertices[i];var n=normals[i].normalized;
                var u=Vector3.ProjectOnPlane(Mathf.Abs(n.x)>.9f?Vector3.forward:Vector3.right,n).normalized;
                var v=Vector3.Cross(n,u).normalized;
                uv[i]=new Vector2(Vector3.Dot(p,u),Vector3.Dot(p,v))/TileMetres;
            }
            mesh.uv=uv;
        }
    }
}
