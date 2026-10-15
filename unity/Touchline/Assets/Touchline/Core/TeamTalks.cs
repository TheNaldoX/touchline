using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    public struct TalkSituation { public string moment;public int margin;public float gap,importance; }
    public sealed class TalkReaction { public string player,name,label;public float composure,drive,morale,trust; }

    // Causeries d'avant-match, de mi-temps et d'après-match. La réaction dépend
    // du ton, du joueur (sang-froid, âge, confiance envers le manager, moral)
    // et de la situation (score, rapport de force, enjeu). Les effets passent
    // dans TeamMindset (sang-froid, engagement) et dans le moral.
    public static class TeamTalks
    {
        public const string PreMatch="prematch",HalfTime="halftime",FullTime="fulltime";
        public const string Calm="calm",Encourage="encourage",Demand="demand",Praise="praise",Fire="fire",Free="free";
        public static readonly string[] Tones={Calm,Encourage,Demand,Praise,Fire,Free};
        // Nervosité d'un grand match pour un joueur sans aucun sang-froid (−0,6 au maximum).
        public const float BigMatchNervesScale=.6f;
        // Une causerie individuelle porte davantage qu'un message au groupe.
        public const float IndividualWeight=1.4f;
        public const int MaxIndividualTalks=3;
        public static string Label(string tone)=>tone switch{Calm=>"Rassurer",Encourage=>"Encourager",Demand=>"Exiger plus",Praise=>"Féliciter",Fire=>"Secouer le groupe",Free=>"Libérer de la pression",_=>tone};
        public static string Hint(string tone)=>tone switch{
            Calm=>"Apaise les nerfs, surtout dans un grand match ; peu d’intensité en plus.",
            Encourage=>"Soutien et engagement, plus utile quand on est mené ou outsider.",
            Demand=>"Plus d’intensité si le groupe vous fait confiance ; agace s’il mène déjà.",
            Praise=>"Confiance et sang-froid ; risque de relâchement avec une large avance.",
            Fire=>"Électrochoc quand on est mené ; peut tétaniser les jeunes et braquer les autres.",
            Free=>"Ôte la pression d’un outsider ; démobilise un favori.",_=>""};
        public static string MomentLabel(string moment)=>moment==PreMatch?"Avant-match":moment==HalfTime?"Mi-temps":"Après-match";
        public static string Describe(string record)
        {
            var parts=(record??"").Split('|');if(parts.Length<3)return record;
            return MomentLabel(parts[0])+" · "+(parts[1]=="group"?"groupe":"individuelle")+" · "+Label(parts[2]);
        }
        public static float Steadiness(PlayerData p)
        {
            float experience=Mathx.Clamp((p.age-19)/12f,0,1);float composure=Mathx.Clamp((p.Attribute("composure")-45)/45f,0,1);
            return composure*.6f+experience*.4f;
        }
        public static float BigMatchNerves(PlayerData p,float importance)=>-Mathx.Clamp(importance,0,1)*(1-Steadiness(p))*BigMatchNervesScale;
        public static TalkReaction React(PlayerData p,PlayerLife life,string tone,TalkSituation s,bool individual)
        {
            if(Array.IndexOf(Tones,tone)<0)throw new ArgumentException("Ton inconnu.");
            float steady=Steadiness(p),trust=life?.trust??60,pressure=Mathx.Clamp(s.importance,0,1)*(1-steady);
            float trustFactor=.5f+trust/100f;bool leading=s.margin>0,losing=s.margin<0,underdog=s.gap< -3,favourite=s.gap>3,final=s.moment==FullTime;
            float c=0,d=0,mo=0;string label;
            switch(tone){
                case Calm:c=.25f+.35f*pressure;d=-.05f;mo=losing?1:0;label=pressure>.3f?"Rassuré":"Serein";break;
                case Encourage:{float k=losing||underdog?1.3f:leading?.7f:1;c=.12f*k;d=.22f*k*trustFactor;mo=1;label="Encouragé";break;}
                case Demand:
                    if(trust<45){c=-.1f;d=-.1f;mo=-2;label="Agacé";}
                    else if(leading&&!final){c=-.12f;d=.05f;mo=-2;label="Incompris";}
                    else{d=.35f*trustFactor*(favourite&&losing?1.3f:1);c=-.05f;mo=losing?0:-1;label="Piqué au vif";}
                    break;
                case Fire:
                    if(s.margin>=0&&!final){c=-.2f;d=.1f;mo=-4;label="Choqué";}
                    else{d=.55f*trustFactor;c=-.25f*(1-steady);mo=-2;label="Remonté";if(trust<50||p.age<22){c-=.35f;mo-=2;label="Tétanisé";}}
                    break;
                case Praise:
                    if(losing){c=-.05f;d=-.1f;mo=-1;label="Incrédule";}
                    else if(s.margin>=2&&!final){c=.15f;d=-.25f;mo=3;label="Relâché";}
                    else{c=.25f;d=.05f;mo=leading||final?3:1;label="Fier";}
                    break;
                default:
                    if(favourite){c=.2f;d=-.25f;mo=0;label="Démobilisé";}
                    else{c=.45f*(pressure+.3f)*(underdog?1.3f:1);d=-.12f;mo=1;label="Libéré";}
                    break;
            }
            if(individual){c*=IndividualWeight;d*=IndividualWeight;mo*=1.5f;}
            if(Math.Abs(c)+Math.Abs(d)<.08f&&Math.Abs(mo)<1)label="Indifférent";
            // Après le match, seul le moral et la relation avec le manager évoluent.
            float trustChange=final?Mathx.Clamp(mo*.5f,-3,3):0;
            return new TalkReaction{player=p.id,name=p.name,label=label,composure=final?0:c,drive=final?0:d,morale=mo,trust=trustChange};
        }
    }

    public partial class Career
    {
        // Enjeu d'une rencontre (0–1) : amical, championnat, haut de tableau, élimination directe.
        public float MatchImportance(Database db)
        {
            var fixture=world?.fixtures.FirstOrDefault(f=>f.id==world.activeFixture);
            if(fixture==null)return .3f;if(fixture.league=="friendly")return .1f;if(fixture.knockout)return .8f;
            var table=Table(fixture.league);int Rank(string c)=>table.FindIndex(r=>r.club==c);
            bool started=table.Any(r=>r.played>0);if(started&&Rank(fixture.home)>=0&&Rank(fixture.home)<4&&Rank(fixture.away)>=0&&Rank(fixture.away)<4)return .7f;
            return .5f;
        }
        public TalkSituation CurrentTalkSituation(Database db,string moment)
        {
            if(match==null)throw new InvalidOperationException("Aucune rencontre en cours.");
            float Strength(int side)=>(float)match.actors.Where(a=>a.side==side&&!a.sentOff).Select(a=>db.Find(a.id)?.rating??60).DefaultIfEmpty(60).Average();
            return new TalkSituation{moment=moment,margin=moment==PreMatchMoment?0:match.score[0]-match.score[1],gap=Strength(0)-Strength(1),importance=MatchImportance(db)};
        }
        const string PreMatchMoment=TeamTalks.PreMatch;
        public string TalkMoment()
        {
            if(match==null)return null;if(match.finished)return TeamTalks.FullTime;if(match.halfTime)return TeamTalks.HalfTime;
            return match.period==1&&match.clock<=0?TeamTalks.PreMatch:null;
        }
        IEnumerable<string> TalkAudience(string moment)=>moment==TeamTalks.FullTime?match.used.Where(id=>life.players.Any(p=>p.id==id)):match.actors.Where(a=>a.side==0&&!a.sentOff).Select(a=>a.id);
        public List<TalkReaction> PreviewTeamTalk(Database db,string tone,string player=null)
        {
            string moment=TalkMoment();if(moment==null)throw new InvalidOperationException("Les causeries se tiennent avant le coup d’envoi, à la mi-temps ou après le match.");
            var situation=CurrentTalkSituation(db,moment);var audience=player!=null?new[]{player}:TalkAudience(moment).ToArray();
            if(player!=null&&!TalkAudience(moment).Contains(player))throw new InvalidOperationException("Ce joueur ne fait pas partie du groupe concerné.");
            return audience.Select(id=>TeamTalks.React(db.Find(id),life.players.FirstOrDefault(p=>p.id==id),tone,situation,player!=null)).ToList();
        }
        public List<TalkReaction> GiveTeamTalk(Database db,string tone,string player=null)
        {
            if(life==null)throw new InvalidOperationException("Aucun groupe à réunir.");RequireEmployment();
            string moment=TalkMoment();var reactions=PreviewTeamTalk(db,tone,player);
            match.mindset??=new[]{TeamMindset.Neutral(),TeamMindset.Neutral()};
            if(match.mindset.Length!=2)match.mindset=new[]{TeamMindset.Neutral(),TeamMindset.Neutral()};
            var mind=match.mindset[0]??=TeamMindset.Neutral();
            string key=moment+"|"+(player==null?"group":player)+"|";
            if(mind.talks.Any(t=>t.StartsWith(key,StringComparison.Ordinal)))throw new InvalidOperationException(player==null?"Vous avez déjà parlé au groupe à ce moment du match.":"Vous avez déjà parlé à ce joueur.");
            if(player!=null&&mind.talks.Count(t=>t.StartsWith(moment+"|",StringComparison.Ordinal)&&!t.StartsWith(moment+"|group|",StringComparison.Ordinal))>=TeamTalks.MaxIndividualTalks)throw new InvalidOperationException("Trois échanges individuels au plus à ce moment.");
            mind.talks.Add(key+tone);
            if(mind.composure==null||mind.composure.Length!=11)mind.composure=new float[11];if(mind.drive==null||mind.drive.Length!=11)mind.drive=new float[11];
            foreach(var r in reactions){
                var actor=match.actors.FirstOrDefault(a=>a.side==0&&a.id==r.player);
                if(actor!=null&&moment!=TeamTalks.FullTime){mind.composure[actor.slot]=Mathx.Clamp(mind.composure[actor.slot]+r.composure,-1,1);mind.drive[actor.slot]=Mathx.Clamp(mind.drive[actor.slot]+r.drive,-1,1);}
                var person=life.players.FirstOrDefault(p=>p.id==r.player);if(person==null)continue;
                person.morale=Mathx.Clamp(person.morale+r.morale,10,100);person.trust=Mathx.Clamp(person.trust+r.trust,0,100);
                var data=db.Find(r.player);if(data!=null)data.morale=person.morale;
            }
            return reactions;
        }
    }
}
