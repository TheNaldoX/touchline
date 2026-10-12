using System;
using System.Linq;

namespace Touchline.Core
{
    public sealed class PressContext
    {
        public Fixture fixture;public string phase,question,unavailable;public int result;
        public bool Available=>fixture!=null&&unavailable==null;
    }
    public partial class Career
    {
        public PressContext Conference(string phase)
        {
            var c=new PressContext{phase=phase};
            if(world==null||life==null||phase!="before"&&phase!="after"){c.unavailable="Aucune conférence disponible.";return c;}
            c.fixture=phase=="before"?NextFixture():world.fixtures.Where(f=>f.played&&(f.home==club||f.away==club)).OrderByDescending(f=>f.day).FirstOrDefault();
            if(c.fixture==null){c.unavailable="Aucune rencontre concernée.";return c;}
            var f=c.fixture;int own=f.home==club?f.hg:f.ag,other=f.home==club?f.ag:f.hg;c.result=Math.Sign(own-other);
            c.question=phase=="before"?"Quel message avant cette rencontre ?":c.result>0?"Après cette victoire, comment garder votre groupe mobilisé ?":c.result<0?"Après cette défaite, assumez-vous la responsabilité du résultat ?":"Ce match nul vous satisfait-il ?";
            if(world.managerStatus!="employed")c.unavailable="Vous n’êtes plus en poste.";
            else if(world.press.Any(p=>p.fixture==f.id&&p.phase==phase))c.unavailable="Votre déclaration est déjà enregistrée.";
            else if(phase=="before"&&(f.day<life.day||f.day-life.day>2)||phase=="after"&&(life.day<f.day||life.day-f.day>2))c.unavailable="Conférence disponible dans les deux jours autour de la rencontre.";
            return c;
        }
        public static string PressQuestion(PressContext context,int index)=>index switch {
            0=>context.question,
            1=>context.phase=="before"?"Allez-vous imposer votre jeu ou vous adapter à cet adversaire ?":"Que retenez-vous de votre organisation tactique sur cette rencontre ?",
            2=>context.phase=="before"?"Comment gérez-vous la pression et la concurrence dans votre groupe ?":"Quel message adressez-vous aux joueurs avant la prochaine séance ?",
            _=>throw new ArgumentOutOfRangeException(nameof(index))};
        public static string PressReply(string phase,int index,string tone)
        {
            if(index<0||index>2||!new[]{"calm","ambition","protect"}.Contains(tone))throw new ArgumentException("Réponse invalide.");
            string[][] before={
                new[]{"Nous préparons ce match avec sérieux, sans faire de promesses sur le résultat.","Nous voulons prendre l’initiative et jouer pour gagner.","La responsabilité de la préparation m’appartient. Je fais confiance au groupe."},
                new[]{"Nous garderons nos principes tout en surveillant les espaces laissés par l’adversaire.","Nous chercherons à imposer notre rythme et à jouer dans leur camp.","Je choisirai une organisation qui mette les joueurs dans de bonnes conditions."},
                new[]{"La concurrence se règle sur le terrain et à l’entraînement. Chacun connaît son rôle.","J’attends de chacun qu’il saisisse sa chance et tire le groupe vers le haut.","Personne ne sera jugé sur une seule action. Nous devons rester solidaires."}};
            string[][] after={
                new[]{"Le score ne résume pas tout. Nous analyserons ce qui a fonctionné et ce qui doit progresser.","Nous devons être exigeants et viser plus haut lors du prochain match.","J’assume mes décisions. Je ne désignerai pas un joueur comme responsable du résultat."},
                new[]{"Nous allons revoir les distances entre les lignes et nos choix avec le ballon.","Je veux davantage d’initiative et de précision dans les zones décisives.","Les joueurs ont appliqué un cadre dont je suis responsable. Nous le corrigerons ensemble."},
                new[]{"Nous allons récupérer, analyser calmement et préparer la prochaine rencontre.","J’attends une réponse forte à l’entraînement et de l’engagement de tout le groupe.","Mon message est simple : nous restons unis et je soutiens mes joueurs."}};
            return (phase=="before"?before:after)[index][Array.IndexOf(new[]{"calm","ambition","protect"},tone)];
        }
        float PressMorale(PressContext c,string answer,PlayerLife p)
        {
            if(answer=="calm")return c.phase=="after"&&c.result<0?0:.25f;
            if(answer=="protect")return c.phase=="after"&&c.result<0?2:1;
            // Ambition after a defeat can sound detached from the result to a
            // player who already distrusts the manager; no universal best answer.
            return p.trust>60?1:c.phase=="after"&&c.result<0?-2:-1;
        }
    }
}
