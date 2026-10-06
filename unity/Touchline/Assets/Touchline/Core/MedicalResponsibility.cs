using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        public bool PlayerMedicalResponsibility(string id)=>!string.IsNullOrEmpty(id)&&life?.players?.Any(p=>p.id==id)==true&&!PlayerRetirementEffective(id);
        static void CloseMedicalResponsibility(ClubLife state,string id,string reason,int day)
        {
            if(state?.medical==null)return;
            foreach(var episode in state.medical.Where(m=>m.player==id&&m.closed<0&&string.IsNullOrEmpty(m.responsibilityEndedReason))){
                episode.responsibilityEndedReason=reason;episode.responsibilityEndedDay=day;
                // closed means an observed medical return. Leaving the club
                // does not establish recovery, alter diagnosis or refund care.
            }
        }
        void CloseMedicalResponsibilitiesForPlayer(PlayerData player)
        {
            if(player==null||life==null)return;
            bool retired=player.team=="retired"||PlayerRetirementEffective(player.id);
            if(retired||player.team!=club)CloseMedicalResponsibility(life,player.id,retired?"retirement":"left-club",life.day);
            if(previousClubs!=null)foreach(var managed in previousClubs)if(retired||player.team!=managed.club)CloseMedicalResponsibility(managed.life,player.id,retired?"retirement":"left-club",life.day);
        }
        void CloseMissingMedicalResponsibilities(Database db)
        {
            if(life?.medical==null)return;
            foreach(var episode in life.medical.Where(m=>m.closed<0&&string.IsNullOrEmpty(m.responsibilityEndedReason)).ToArray()){
                if(PlayerMedicalResponsibility(episode.player)&&db.Find(episode.player)?.team==club)continue;
                bool retired=db.Find(episode.player)?.team=="retired"||PlayerRetirementEffective(episode.player);
                CloseMedicalResponsibility(life,episode.player,retired?"retirement":"outside-current-follow-up",life.day);
            }
        }
    }
}
