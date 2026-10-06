using System;
using System.Collections.Generic;
using System.Linq;
using Touchline.Core;

namespace Touchline
{
    // UI projection only. Actor sides, metrics arrays and manager actions keep simulation indices.
    public readonly struct MatchFixtureOrder
    {
        public readonly int HomeSide;
        public int AwaySide=>1-HomeSide;
        public readonly string HomeClub,AwayClub;
        MatchFixtureOrder(MatchState match,int homeSide){HomeSide=homeSide;HomeClub=homeSide==0?match.home:match.away;AwayClub=homeSide==0?match.away:match.home;}
        public int SideAt(int column)=>column==0?HomeSide:AwaySide;
        public string ClubAt(int column)=>column==0?HomeClub:AwayClub;
        public string Score(MatchState match)=>match.score[HomeSide]+" – "+match.score[AwaySide];
        public string MomentClass(int simulationSide)=>simulationSide==HomeSide?"moment-home":"moment-away";
        static bool PairMatches(MatchState match,Fixture fixture)=>fixture!=null&&(fixture.home==match.home&&fixture.away==match.away||fixture.home==match.away&&fixture.away==match.home);
        public static MatchFixtureOrder From(Career career,MatchState match)
        {
            if(match==null)throw new ArgumentNullException(nameof(match));
            var world=career?.world;Fixture fixture=null;
            if(world!=null&&!string.IsNullOrEmpty(world.activeFixture))fixture=world.fixtures?.FirstOrDefault(f=>f!=null&&f.id==world.activeFixture);
            // Recorded matches clear activeFixture. A freshly loaded finished match can
            // use one exact same-day, same-pair, same-score record; ambiguous data falls back.
            if(world!=null&&string.IsNullOrEmpty(world.activeFixture)&&match.finished&&career.life!=null){
                var records=(world.fixtures??new List<Fixture>()).Concat(world.history??new List<Fixture>())
                    .Where(f=>f!=null&&f.played&&f.day==career.life.day&&PairMatches(match,f))
                    .Where(f=>f.hg==match.score[f.home==match.home?0:1]&&f.ag==match.score[f.home==match.home?1:0])
                    .GroupBy(f=>f.id).Select(g=>g.First()).Take(2).ToArray();
                if(records.Length==1)fixture=records[0];
            }
            return new MatchFixtureOrder(match,PairMatches(match,fixture)&&fixture.home==match.away?1:0);
        }
    }
}
