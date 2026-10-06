namespace Touchline.Core
{
    public partial class Career
    {
        void SettlePermanentDepartureGrowth(PlayerData player)
        {
            if(player==null)return;
            var state=KnownClubLife(player.team);
            var person=state?.players?.Find(x=>x.id==player.id);
            // Use exactly the same bounded, one-time settlement as a loan
            // departure. No history is reconstructed for unobserved players.
            SettleLoanDepartureGrowth(player,person);
        }
    }
}
