using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        void MatchBench()
        {
            arena.Paused=true;var m=Career.match;var panel=Modal("Le banc de touche");panel.name="match-bench";panel.AddToClassList("match-bench");
            var summary=Text(panel,"","bench-summary");var available=m.actors.Where(a=>a.side==0&&!a.sentOff).OrderBy(a=>a.slot).ToArray();
            if(m.finished||available.Length==0){Text(panel,"Aucun changement possible dans cette rencontre.");return;}
            // Joueur le plus fatigué parmi ceux qui n’ont pas déjà un remplaçant préparé : un deuxième changement ne repasse pas par la liste.
            var replaced=m.pendingSubstitutions.Where(p=>p.side==0).Select(p=>p.outgoing).ToArray();int selection=System.Array.IndexOf(available,available.OrderBy(a=>replaced.Contains(a.id)?1:0).ThenBy(a=>a.fitness).First());
            var labels=available.Select(a=>Database.Find(a.id).name+" · "+Mathf.RoundToInt(a.fitness)+" % de condition"+(a.injured?" · blessé":"")).ToList();
            var outgoing=new DropdownField("Joueur à remplacer",labels,selection){name="bench-outgoing"};panel.Add(outgoing);
            Text(panel,"Les joueurs sont triés selon leur aisance au poste. Un changement préparé attend le prochain arrêt de jeu.","muted");
            var list=Scroll(panel);list.name="bench-candidates";
            void Populate(){
                list.Clear();var target=available[outgoing.index];var role=m.homeTactic.withoutBall[target.slot].role;
                summary.text=(5-m.substitutions[0])+" remplacement(s) restant(s) · "+(3-m.homeWindows.Count)+" fenêtre(s) hors mi-temps";
                foreach(var pending in m.pendingSubstitutions.Where(p=>p.side==0).ToArray()){
                    var row=Row(list,"bench-pending");Text(row,Database.Find(pending.incoming).name+" pour "+Database.Find(pending.outgoing).name,"muted");Button(row,"Annuler",()=>{arena.Simulation.CancelSubstitution(pending.outgoing);Save();Populate();UpdatePendingBanner();}).name="bench-cancel";
                }
                var candidates=Database.Squad(Career.club).Where(p=>!m.used.Contains(p.id)).OrderByDescending(p=>p.Fit(role)).ThenByDescending(p=>p.fitness).ToArray();
                foreach(var player in candidates){
                    var row=Card(list,"bench-candidate");var copy=new VisualElement();copy.AddToClassList("bench-copy");row.Add(copy);Text(copy,player.name,"section-title");float fit=player.Fit(role);
                    Text(copy,FrenchFootballPositions.Label(player.position)+" · "+Mathf.RoundToInt(player.fitness)+" % · "+(fit>=.94f?"Poste habituel":fit>=.8f?"Adaptation possible":"Poste inhabituel"),"muted");
                    string restriction=Career.LineupRestriction(Database,target.slot,player.id);if(Career.life.managerBanUntil>Career.life.day)restriction="Vous êtes suspendu : la gestion revient à votre staff.";
                    if(restriction!=null)Text(copy,restriction,"bench-unavailable");
                    var choose=Button(row,"Faire entrer",()=>{try{Career.AssignTacticalPlayer(Database,target.slot,player.id);Save();CloseModal();MatchBench();UpdatePendingBanner();}catch(System.Exception e){Message(e.Message);}});choose.name="bench-enter-"+player.id;choose.SetEnabled(restriction==null);
                }
                if(candidates.Length==0)Text(list,"Aucun autre joueur disponible sur le banc.","empty-state");
            }
            outgoing.RegisterValueChangedCallback(_=>Populate());Populate();
            var actions=Row(panel,"bench-actions");Button(actions,"Revoir la tactique",()=>Navigate("Tactique"));Button(actions,m.halfTime?"Deuxième mi-temps":"Reprendre le match",()=>{CloseModal();if(m.halfTime)arena.Simulation.ResumeHalf();arena.Paused=false;}).AddToClassList("primary");
        }
    }
}
