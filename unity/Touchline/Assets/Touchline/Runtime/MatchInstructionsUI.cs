using System;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        void MatchInstructionPanel()
        {
            if(arena==null||Career.match==null)return;
            var panel=Modal("Consignes");panel.name="match-instructions";
            Button(panel,"Composition et rôles",()=>{CloseModal();Navigate("Tactique");}).name="quick-tactic-full-board";
            var body=Scroll(panel);Text(body,"Choisissez un niveau : il s’applique immédiatement, puis vous revenez au match.","muted");
            QuickInstruction(body,MatchInstruction.Pressing,"Pressing",new[]{"Mesuré","Normal","Intense"},"Plus de pression sur le porteur, mais davantage d’efforts et d’espaces à couvrir.");
            QuickInstruction(body,MatchInstruction.Line,"Ligne défensive",new[]{"Basse","Médiane","Haute"},"Un bloc haut réduit les distances ; les appels dans son dos deviennent plus dangereux.");
            QuickInstruction(body,MatchInstruction.Width,"Largeur offensive",new[]{"Étroite","Normale","Large"},"Écarter le jeu ouvre les couloirs et éloigne les soutiens.");
            QuickInstruction(body,MatchInstruction.Tempo,"Rythme",new[]{"Patient","Normal","Rapide"},"Décider plus vite exige davantage de précision technique.");
            QuickInstruction(body,MatchInstruction.Directness,"Passes",new[]{"Courtes","Mixtes","Directes"},"Jouer plus long accélère la progression mais expose aux interceptions.");
            TouchlineShoutsCard(body);
            // The unfolded layout scales one UI unit to about .97 dp: 50 units
            // retain the 48 dp touch target in both native Fold layouts.
            panel.Query<UnityEngine.UIElements.Button>().ForEach(button=>button.style.minHeight=50);
        }
        void QuickInstruction(VisualElement body,MatchInstruction instruction,string title,string[] labels,string tradeoff)
        {
            var card=Card(body,"instruction-card");Text(card,title,"section-title");var row=Row(card,"instruction-options");
            float value=MatchInstructionShortcuts.Read(Career.tactic,instruction);
            const float HalfPresetStep=.15f; // Half the 0.3 interval on the normalized 0–1 scale.
            for(int i=0;i<labels.Length;i++){
                int level=i;var button=Button(row,labels[i],()=>{MatchInstructionShortcuts.Apply(Career,instruction,level);QueuePreferenceSave();CloseModal();});
                button.name="quick-tactic-"+instruction+"-"+i;
                // Android touch target, in UI units (approximately dp on Fold).
                button.style.minHeight=50;button.style.fontSize=13;button.style.flexGrow=1;
                button.EnableInClassList("active",Mathf.Abs(value-MatchInstructionShortcuts.Value(i))<HalfPresetStep);
            }
            Text(card,tradeoff,"muted");
        }
    }
}
