using System;
using System.Collections.Generic;
using System.Linq;
namespace Touchline.Core
{
    public static class PlayingTimeRoles
    {
        public static string[] All=>new[]{"key","starter","rotation","impact_sub","youth"};
        public static string Normalize(string role)=>role=="star"?"key":role=="prospect"?"youth":All.Contains(role)?role:"rotation";
        public static bool Valid(string role)=>All.Contains(role);
        public static string Label(string role){switch(Normalize(role)){case "key":return "Titulaire indiscutable";case "starter":return "Titulaire";case "impact_sub":return "Super sub";case "youth":return "Jeune en développement";default:return "Remplaçant";}}
        public static string Description(string role){switch(Normalize(role)){
            case "key":return "Débute au moins 70 % des rencontres où il est disponible.";
            case "starter":return "Débute au moins 50 % des rencontres où il est disponible.";
            case "impact_sub":return "Entre régulièrement pour peser sur le match : au moins 15 minutes dans 50 % des rencontres disponibles. Les titularisations comptent aussi.";
            case "youth":return "Intégration progressive, sans quota de matchs en équipe première. Le plan de développement reste à organiser.";
            default:return "Joue au moins 15 minutes dans 20 % des rencontres disponibles, comme titulaire ou en sortie de banc.";
        }}
        public static bool Concern(string role,PlayingTimeUsage usage)
        {
            if(usage?.games==null||usage.games.Count<4)return false;role=Normalize(role);
            int required=role=="key"?70:role=="starter"||role=="impact_sub"?50:role=="rotation"?20:0;
            int actual=usage.games.Count(g=>role=="key"||role=="starter"?g.started:g.minutes>=15);
            return actual*100<usage.games.Count*required;
        }
    }
    [Serializable] public sealed class PlayingTimeGame { public bool started;public int minutes; }
    [Serializable] public sealed class PlayingTimeUsage
    {
        public string club;
        public List<PlayingTimeGame> games=new List<PlayingTimeGame>();
        public void Add(bool started,float minutes){if(games==null)games=new List<PlayingTimeGame>();games.Add(new PlayingTimeGame{started=started,minutes=(int)Math.Floor(Mathx.Clamp(minutes,0,130)+.001f)});while(games.Count>12)games.RemoveAt(0);}
        public PlayingTimeUsage Copy()=>new PlayingTimeUsage{club=club,games=(games??new List<PlayingTimeGame>()).Select(g=>new PlayingTimeGame{started=g.started,minutes=g.minutes}).ToList()};
    }
    public partial class Career
    {
        // New contracts use only match records observed after signing. Legacy
        // contracts retain their appearance-based evaluation until renegotiated.
        void RecordPlayingTime(string id,bool started,float minutes)
        {
            var c=world?.contracts?.FirstOrDefault(x=>x.player==id&&x.club==club);
            if(c?.playingTime==null||world.managerStatus!="employed"||minutes<=0&&!Available(id))return;
            if(!string.IsNullOrEmpty(c.playingTime.club)&&c.playingTime.club!=club)c.playingTime=new PlayingTimeUsage();
            c.playingTime.club=club;
            var dismissal=match?.events?.FirstOrDefault(e=>e.kind=="red"&&e.player==id&&e.side==0);
            if(dismissal!=null){var arrival=match.events.FirstOrDefault(e=>e.kind=="substitution"&&e.player==id&&e.side==0);minutes=Math.Min(minutes,Math.Max(0,(dismissal.time-(arrival?.time??0))/match.SecondsPerMinute));}
            c.playingTime.Add(started,minutes);
        }
        public string PlayingTimeOfferIssue(Database db,string id,string role)=>PlayingTimeOfferIssueForClub(db,id,role,club);
        string PlayingTimeOfferIssueForClub(Database db,string id,string role,string assessingClub)
        {
            var p=db.Find(id);if(p==null)return "Joueur introuvable.";role=PlayingTimeRoles.Normalize(role);
            if(role=="key"||role=="starter")return null;
            float relative=p.rating+p.development-Strength(db,assessingClub);
            if(relative>=4)return "Le joueur attend un rôle de titulaire compte tenu de sa place estimée dans votre effectif. Une hausse salariale seule ne remplace pas ce projet sportif.";
            if(role=="youth"&&p.age>=20&&relative>=-3)return "Ce joueur s’estime déjà prêt pour des matchs seniors réguliers. Un simple statut de jeune sans quota de matchs ne lui convient pas.";
            var current=world?.contracts?.FirstOrDefault(x=>x.player==id&&x.club==assessingClub);
            if(current!=null&&relative>=-3&&(PlayingTimeRoles.Normalize(current.role)=="key"||PlayingTimeRoles.Normalize(current.role)=="starter"))return "Le joueur refuse de passer de son rôle de titulaire à un statut moins utilisé tant qu’il garde ce niveau dans l’effectif.";
            return null;
        }
        public bool PlayingTimeConcern(string id)
        {
            var c=world?.contracts?.FirstOrDefault(x=>x.player==id&&x.club==club);if(c==null)return false;
            if(c.playingTime!=null)return (string.IsNullOrEmpty(c.playingTime.club)||c.playingTime.club==club)&&PlayingTimeRoles.Concern(c.role,c.playingTime);
            var role=PlayingTimeRoles.Normalize(c.role);if(role=="rotation"||role=="youth"||c.parent!=null)return false;
            int opportunities=RoleMatchOpportunities(id);return opportunities>0&&RoleAppearances(id)<opportunities*(role=="key"?.7f:.5f);
        }
        public string PlayingTimeProgress(string id)
        {
            var c=world?.contracts?.FirstOrDefault(x=>x.player==id&&x.club==club);if(c==null)return "Aucun engagement de temps de jeu au club.";
            if(c.playingTime==null)return "Ancien contrat : apparitions historiques suivies, titularisations non documentées. Le suivi précis commence à la prochaine signature.";
            if(!string.IsNullOrEmpty(c.playingTime.club)&&c.playingTime.club!=club)return "Le suivi conservé concerne un ancien club. Un nouveau bilan commencera à la prochaine rencontre observée ici.";
            var g=c.playingTime.games??new List<PlayingTimeGame>();if(g.Count==0)return "Suivi à partir de la signature : aucune rencontre disponible encore observée.";
            return g.Count+" dernières rencontres disponibles · "+g.Count(x=>x.started)+" titularisations · "+g.Count(x=>!x.started&&x.minutes>0)+" entrées · "+g.Sum(x=>x.minutes)+" minutes. "+(g.Count<4?"Premier bilan après quatre rencontres.":PlayingTimeConcern(id)?"Utilisation inférieure à l’engagement.":"Engagement respecté sur cette période.");
        }
    }
}
