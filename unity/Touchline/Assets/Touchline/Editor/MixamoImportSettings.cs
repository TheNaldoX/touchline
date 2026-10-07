using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Touchline.Editor
{
    // Réglages d'import des animations Mixamo déposées dans Animations/Mixamo :
    // Humanoid (avatar créé depuis le squelette Mixamo du fichier), pas de
    // matériaux ni de caméras, boucle pour les déplacements, pose de la racine
    // figée (le moteur de match décide où est le joueur).
    public sealed class MixamoImportSettings : AssetPostprocessor
    {
        const string Folder="Assets/Touchline/Resources/Animations/Mixamo/";
        static readonly string[] LoopKeywords={"idle","walk","jog","run","strafe","backward","sidestep","dribble","sprint"};
        // Clips doublés en version miroir (gauche/droite) : « <nom> (miroir) ».
        static readonly string[] MirroredClips={"Goalkeeper Diving Save","Goalkeeper Diving Save (1)"};
        static readonly string[] OneShotKeywords={"stop","turn","strike","kick","pass","shot","chip","header","tackle","trip","catch","save","throw","scoop","place","receive","victory","celebrat","defeat","pump"};

        // À incrémenter quand les réglages changent : Unity réimporte alors les clips.
        public override uint GetVersion()=>4;

        static bool Mixamo(string path)=>path.Replace('\\','/').StartsWith(Folder)&&path.EndsWith(".fbx",System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if(!Mixamo(assetPath))return;
            var importer=(ModelImporter)assetImporter;
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.importCameras=false;importer.importLights=false;importer.importBlendShapes=false;
            importer.animationCompression=ModelImporterAnimationCompression.KeyframeReduction;
            importer.resampleCurves=true;
        }

        void OnPreprocessAnimation()
        {
            if(!Mixamo(assetPath))return;
            var importer=(ModelImporter)assetImporter;
            var name=System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool loop=LoopKeywords.Any(name.Contains)&&!OneShotKeywords.Any(name.Contains);
            var clips=importer.defaultClipAnimations;
            foreach(var clip in clips){
                clip.name=System.IO.Path.GetFileNameWithoutExtension(assetPath);
                clip.loopTime=loop;clip.loopPose=loop;
                // Racine : rotation et hauteur figées sur la pose, déplacement horizontal
                // ignoré (animations « In Place » ou non) ; le moteur pilote la position.
                // Orientation figée sur celle du corps (une course arrière regarde vers l'avant),
                // hauteur gardée dans la pose (sauts, plongeons), déplacement horizontal laissé
                // en mouvement de racine, ignoré à la lecture (applyRootMotion=false) : un clip
                // qui avance (Standard Run, sprint) reste sur place au lieu de dériver puis revenir.
                clip.lockRootRotation=true;clip.keepOriginalOrientation=false;
                clip.lockRootHeightY=true;clip.keepOriginalPositionY=true;
                clip.lockRootPositionXZ=false;clip.keepOriginalPositionXZ=false;
            }
            var all=clips.ToList();
            if(MirroredClips.Contains(System.IO.Path.GetFileNameWithoutExtension(assetPath)))
                foreach(var clip in clips){
                    var mirror=new ModelImporterClipAnimation{name=clip.name+" (miroir)",takeName=clip.takeName,firstFrame=clip.firstFrame,lastFrame=clip.lastFrame,mirror=true,
                        loopTime=clip.loopTime,loopPose=clip.loopPose,lockRootRotation=clip.lockRootRotation,keepOriginalOrientation=clip.keepOriginalOrientation,
                        lockRootHeightY=clip.lockRootHeightY,keepOriginalPositionY=clip.keepOriginalPositionY,lockRootPositionXZ=clip.lockRootPositionXZ,keepOriginalPositionXZ=clip.keepOriginalPositionXZ};
                    all.Add(mirror);
                }
            importer.clipAnimations=all.ToArray();
        }
    }
}
