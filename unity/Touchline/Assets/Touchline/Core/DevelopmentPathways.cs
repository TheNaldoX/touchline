using System;
using System.Linq;

namespace Touchline.Core
{
    public partial class Career
    {
        // Retained only for old saves. Coaching now uses the contracted youth staff member.
        public int youthCoach=1;
        public void LoanYouth(Database db,string player,string destination)
        {
            OffPitch();var p=db.Find(player);var path=world.youth.FirstOrDefault(y=>y.player==player);if(path==null||p.team!=club||p.age<18||p.age>23||!WindowOpen||db.Squad(club).Count<19||!LoanRecipientAllowed(db,destination))throw new InvalidOperationException("Prêt de développement impossible dans cette situation.");
            if(p.rating<Strength(db,destination)-8)throw new InvalidOperationException("Ce club ne prévoit pas un temps de jeu suffisant pour ce jeune.");
            var c=Contract(db,player);int end=Math.Min(c.until,Math.Max(life.day+30,world.seasonEnd-20));if(c.IsLoan||end<life.day+28)throw new InvalidOperationException("Le contrat parent ne permet pas ce prêt de développement.");BeforeFinancialTermsChange(db,club,destination);CaptureLoanParent(c,p);c.parentUntil=c.until;c.terms=new MarketTerms{loanWagePercent=50};c.parent=club;c.club=destination;c.originalWage=p.wage;c.loanUntil=end;PrepareLoanPurchase(c,3,true);p.team=destination;AfterFinancialTermsChange(db,club,destination);path.group="loan";StartOutgoingLoanTracking(c,path);life.players.RemoveAll(x=>x.id==player);SavePlayer(p);lineup=Select(db,club,tactic);
            Mail("Formation","Prêt de développement conclu",p.name+" rejoint "+db.clubs.First(x=>x.id==destination).name+". Votre club conserve la moitié du salaire et recevra un bilan mensuel.",player,"academy");
        }
        void DevelopmentAndRoles(Database db)
        {
            
            // First contract of each player, as FirstOrDefault returned, built once
            // instead of rescanning every contract for each youth path.
            var firstContract=new System.Collections.Generic.Dictionary<string,Employment>();foreach(var c in world.contracts)if(c.player!=null&&!firstContract.ContainsKey(c.player))firstContract[c.player]=c;
            foreach(var path in world.youth){var p=db.Find(path.player);if(p==null)continue;
                firstContract.TryGetValue(p.id,out var contract);if(path.group=="loan"&&contract?.parent==club&&life.day%28==0)Mail("Responsable des prêts","Suivi mensuel",p.name+" : "+Math.Max(0,path.loanAppearances-contract.appearancesAtSigning)+" apparitions de prêt simulées, "+path.trackedLoanMinutes+" minutes estimées en prêt depuis le début du suivi distinct. Les minutes sont estimées selon le poste, la concurrence et les rencontres réellement jouées du club. "+(contract.loanAppearanceHistoryEstimated?"Ancienne partie : le suivi des apparitions reprend à la migration, sans historique antérieur vérifié.":"Ces chiffres ne sont pas une composition IA réellement simulée."),p.id,"academy");
            }
            if(life.day%30!=0||world.managerStatus!="employed")return;
            foreach(var contract in world.contracts.Where(c=>c.club==club&&life.day-c.joined>=60&&(!c.IsLoan||c.playingTime!=null))){
                var p=life.players.FirstOrDefault(x=>x.id==contract.player);if(p==null||!Available(p.id))continue;
                bool concern=PlayingTimeConcern(contract.player);
                if(concern){p.trust=Math.Max(0,p.trust-3);p.morale=Math.Max(10,p.morale-4);Mail(db.Find(p.id).name,"Mon rôle dans votre projet","Le statut de "+PlayingTimeRoles.Label(contract.role)+" annoncé à ma signature ne correspond pas à ma situation. "+PlayingTimeProgress(p.id)+" J’attends des actes et une discussion.",p.id,"talk");}
            }
        }
    }
}

