using System;
using System.Linq;
using System.Collections.Generic;
namespace Touchline.Core
{
    public partial class Career
    {
        // No retroactive appearances or absences are created for older saves.
        // The available fixture archive is the only evidence of opportunities.
        public int RoleMatchOpportunities(string player)
        {
            var contract=world?.contracts?.FirstOrDefault(c=>c.player==player&&c.club==club&&!c.IsLoan);
            if(contract==null)return 0;
            return (world.fixtures??new List<Fixture>()).Concat(world.history??new List<Fixture>())
                .Where(f=>f!=null&&f.played&&f.day>=contract.joined&&f.day<=life.day&&(f.home==club||f.away==club))
                .GroupBy(f=>f.id??f.league+"/"+f.day+"/"+f.home+"/"+f.away)
                .Select(g=>g.First()).Count(f=>!KnownMedicalAbsence(player,f.day));
        }
        bool KnownMedicalAbsence(string player,int day)
        {
            return life.medical.Any(m=>m.player==player&&m.opened<=day&&(m.closed<0||day<m.closed)&&
                (string.IsNullOrEmpty(m.responsibilityEndedReason)||day<m.responsibilityEndedDay));
        }
        int RoleAppearances(string player)
        {
            var contract=world?.contracts?.FirstOrDefault(c=>c.player==player&&c.club==club&&!c.IsLoan);
            var person=life.players.FirstOrDefault(p=>p.id==player);
            return contract==null||person==null?0:Math.Max(0,person.appearances-contract.appearancesAtSigning);
        }
    }
}
