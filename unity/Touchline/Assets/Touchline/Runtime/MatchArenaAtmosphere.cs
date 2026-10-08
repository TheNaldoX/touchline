using UnityEngine;

namespace Touchline
{
    // Vie des tribunes pendant le match : réactions du public (CrowdReaction) et
    // drapeaux qui flottent (StadiumFlags). Avancé à chaque image, y compris pendant
    // le ralenti d'un but (la tribune continue de célébrer).
    public sealed partial class MatchArena
    {
        CrowdReaction crowdReaction;StadiumFlags flags;
        public CrowdReaction CrowdLife=>crowdReaction;
        void BuildAtmosphere(Mesh crowd,CrowdRig rig,Material[] crowdMaterials)
        {
            crowdReaction=new CrowdReaction(crowd,rig);crowdReaction.Observe(Simulation,false);
            flags=new StadiumFlags();stadiumMeshes.Add(flags.Mesh);
            var go=new GameObject(flags.Mesh.name);go.transform.SetParent(world,false);go.AddComponent<MeshFilter>().sharedMesh=flags.Mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{crowdMaterials[0],crowdMaterials[1],crowdMaterials[2]};
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
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
