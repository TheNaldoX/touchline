using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        public string[] TacticalEleven=>match==null?lineup:match.actors.Where(a=>a.side==0).OrderBy(a=>a.slot).Select(a=>a.id).ToArray();
        public string LineupRestriction(Database db,int slot,string incoming)
        {
            if(world!=null&&world.managerStatus!="employed")return "Vous n’êtes plus en poste dans ce club.";
            if(slot<0||slot>10)return "Poste invalide.";
            var p=db.Find(incoming);if(p==null||p.team!=club)return "Ce joueur n’appartient pas à votre effectif.";
            if(!Available(incoming))return "Joueur indisponible.";
            if(match==null)return null;
            if(match.finished)return "Le match est terminé.";
            var target=match.actors.First(a=>a.side==0&&a.slot==slot);if(target.sentOff)return "Un joueur exclu ne peut pas être remplacé.";
            var active=match.actors.FirstOrDefault(a=>a.side==0&&a.id==incoming);if(active!=null)return active.sentOff?"Ce joueur a été exclu.":null;
            if(match.used.Contains(incoming))return "Ce joueur est déjà sorti.";
            var pending=MatchSimulation.PendingSubstitutionRestriction(match,0,target.id,incoming);if(pending!=null)return pending;
            if(match.substitutions[0]>=5)return "Les cinq remplacements ont été utilisés.";
            if(!match.halfTime&&!match.homeWindows.Contains(match.clock)&&match.homeWindows.Count>=3)return "Les trois fenêtres de remplacement ont été utilisées.";
            return null;
        }
        public void AssignTacticalPlayer(Database db,int slot,string incoming)
        {
            var error=LineupRestriction(db,slot,incoming);if(error!=null)throw new InvalidOperationException(error);
            var ids=TacticalEleven;int from=Array.IndexOf(ids,incoming);if(from==slot)return;
            if(match==null){if(from>=0)lineup[from]=lineup[slot];lineup[slot]=incoming;return;}
            if(from<0){new MatchSimulation(db,match).RequestSubstitution(0,slot,incoming);return;}
            // Move the actors, retaining individual fitness, cautions, possession
            // and identity. Array order must continue to match side/slot indices.
            var first=match.actors[from];var second=match.actors[slot];first.slot=slot;second.slot=from;match.actors[slot]=first;match.actors[from]=second;
        }
    }
}
