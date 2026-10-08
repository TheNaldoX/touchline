using UnityEngine;

namespace Touchline
{
    // Vie des tribunes pendant le match : réactions du public (CrowdReaction) et
    // drapeaux qui flottent (StadiumFlags). Avancé à chaque image, y compris pendant
    // le ralenti d'un but (la tribune continue de célébrer).
    public sealed partial class MatchArena
    {
        CrowdReaction crowdReaction;StadiumFlags flags;ContactShadows contactShadows;Texture2D contactTexture;
        const int ContactCasters=23; // 22 joueurs + ballon
        public CrowdReaction CrowdLife=>crowdReaction;
        void BuildAtmosphere(Mesh crowd,CrowdRig rig,Material[] crowdMaterials)
        {
            crowdReaction=new CrowdReaction(crowd,rig);crowdReaction.Observe(Simulation,false);
            flags=new StadiumFlags();stadiumMeshes.Add(flags.Mesh);
            var go=new GameObject(flags.Mesh.name);go.transform.SetParent(world,false);go.AddComponent<MeshFilter>().sharedMesh=flags.Mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{crowdMaterials[0],crowdMaterials[1],crowdMaterials[2]};
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        // Taches sombres sous les joueurs et le ballon (ContactShadows), un appel de rendu.
        void BuildContactShadows()
        {
            var template=Resources.Load<Material>(PitchTurf.WearMaterialPath);if(template==null)return;
            contactShadows=new ContactShadows(ContactCasters);stadiumMeshes.Add(contactShadows.Mesh);contactTexture=ContactShadows.Texture();
            var material=new Material(template){name="Contact shadows"};material.mainTexture=contactTexture;material.color=Color.white;
            var go=new GameObject(contactShadows.Mesh.name);go.transform.SetParent(world,false);go.AddComponent<MeshFilter>().sharedMesh=contactShadows.Mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void UpdateContactShadows()
        {
            if(contactShadows==null)return;
            for(int i=0;i<players.Length&&i<ContactCasters-1;i++){var p=players[i].transform.position;contactShadows.Set(i,p,ContactShadows.PlayerRadius,players[i].gameObject.activeInHierarchy?ContactShadows.Strength(p.y):0);}
            if(ball.gameObject.activeInHierarchy)contactShadows.SetBall(ContactCasters-1,ball.position);else contactShadows.Set(ContactCasters-1,ball.position,0,0);
            contactShadows.Apply();
        }
        void AdvanceAtmosphere(float frameDelta)
        {
            if(crowdReaction==null)return;
            crowdReaction.Observe(Simulation,!QuietPresentation);
            if(QuietPresentation)return;
            crowdReaction.Advance(frameDelta);flags.Advance(frameDelta,crowdReaction.Excitement(0));
        }
    }
}
