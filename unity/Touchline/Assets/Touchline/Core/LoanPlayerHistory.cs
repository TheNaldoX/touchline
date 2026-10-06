using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    // Owned by the parent club. Borrower appearances and estimated loan minutes
    // never enter this record of observations made by the manager.
    [Serializable] public class LoanPlayerHistory
    {
        public PlayerLife player;
        public int departed;
    }

    public partial class Career
    {
        ClubLife KnownClubLife(string owner)=>owner==club?life:previousClubs?.FirstOrDefault(x=>x.club==owner)?.life;

        static void SettleLoanDepartureGrowth(PlayerData player,PlayerLife person)
        {
            if(player==null||person==null)return;
            float gain=Math.Min(Math.Max(0,person.growth),Math.Max(0,player.potential-player.rating));
            player.rating+=gain;
            foreach(var attribute in player.attributes??Array.Empty<AttributeValue>())attribute.value=Math.Min(99,attribute.value+gain);
            person.growth=0;player.development=0;
        }

        void CaptureLoanPlayerHistory(PlayerData player,string owner)
        {
            var state=KnownClubLife(owner);var known=state?.players?.FirstOrDefault(x=>x.id==player.id);
            if(known==null)return; // Old saves and unobserved NPC histories stay unknown.
            SettleLoanDepartureGrowth(player,known);
            var saved=known.Copy();
            // These are instructions from the departing club, not an ongoing
            // medical follow-up at the borrower. Absolute bans remain dated.
            saved.boostUntil=-1;saved.restUntil=-1;saved.rehabUntil=0;
            state.loanedPlayers??=new List<LoanPlayerHistory>();
            state.loanedPlayers.RemoveAll(x=>x.player?.id==player.id);
            state.loanedPlayers.Add(new LoanPlayerHistory{player=saved,departed=life.day});
            state.players.RemoveAll(x=>x.id==player.id);
        }

        void RestoreLoanPlayerHistory(PlayerData player,string owner,string borrower)
        {
            var borrowingState=KnownClubLife(borrower);
            var observed=borrowingState?.players?.FirstOrDefault(x=>x.id==player.id);
            SettleLoanDepartureGrowth(player,observed);
            borrowingState?.players?.RemoveAll(x=>x.id==player.id);
            var parentState=KnownClubLife(owner);if(parentState==null)return;
            var history=parentState.loanedPlayers?.FirstOrDefault(x=>x.player?.id==player.id);
            var restored=history?.player?.Copy()??new PlayerLife{id=player.id};
            // Fitness/morale reflect the current player, while trust and real
            // appearances belong to this manager/club's recorded history.
            restored.fitness=player.fitness;restored.morale=player.morale;
            if(history!=null&&restored.promiseUntil>=history.departed)
                restored.promiseUntil+=Math.Max(0,life.day-history.departed);
            parentState.loanedPlayers?.RemoveAll(x=>x.player?.id==player.id);
            parentState.players??=new List<PlayerLife>();
            parentState.players.RemoveAll(x=>x.id==player.id);parentState.players.Add(restored);
        }

        void ForgetLoanPlayerHistory(string owner,string id)=>KnownClubLife(owner)?.loanedPlayers?.RemoveAll(x=>x.player?.id==id);
    }
}
