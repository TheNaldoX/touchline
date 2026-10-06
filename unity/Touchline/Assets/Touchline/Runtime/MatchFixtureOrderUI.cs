using Touchline.Core;
using UnityEngine.UIElements;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        MatchState displayedFixtureMatch;
        MatchFixtureOrder displayedFixtureOrder;
        MatchFixtureOrder FixtureDisplayOrder(MatchState match)
        {
            // Retain the confirmed order when RecordWorldMatch clears activeFixture.
            if(!ReferenceEquals(displayedFixtureMatch,match)){displayedFixtureMatch=match;displayedFixtureOrder=MatchFixtureOrder.From(Career,match);}
            return displayedFixtureOrder;
        }
        void BuildMatchScoreboard()
        {
            var order=FixtureDisplayOrder(Career.match);var score=Row(content,"scoreboard");score.name="match-scoreboard";
            score.Add(new Label(ClubName(order.HomeClub)){name="match-home-club"});
            clock=new Label{name="match-score"};clock.AddToClassList("score");score.Add(clock);
            score.Add(new Label(ClubName(order.AwayClub)){name="match-away-club"});
        }
    }
}
