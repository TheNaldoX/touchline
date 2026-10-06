using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        void PreseasonPanel(VisualElement parent)=>CalendarPreparation(parent);
        void ActiveLoansPanel(VisualElement parent)
        {
            foreach(var o in Career.outgoingLoans.Where(o=>o.owner==Career.club&&(o.status=="pending"||o.status=="accepted"||o.status=="declined"))){var card=Card(parent);Text(card,"Prêt de "+Database.Find(o.player).name+" · "+ClubName(o.borrower)+" · "+o.status);if(o.status=="accepted"&&o.due+7>=Career.life.day&&!Career.PlayerRetirementEffective(o.player)&&Database.Find(o.player)?.team!="retired")Button(card,"Signer le prêt sortant",()=>RunDecision(()=>Career.SignOutgoingLoan(Database,o.player)));}
            var loans=Career.world.contracts.Where(c=>c.parent!=null&&(c.parent==Career.club||c.club==Career.club)).ToArray();if(loans.Length==0)return;var s=new Foldout{text="Prêts en cours",value=true};parent.Add(s);
            foreach(var loan in loans){var card=Card(s);Text(card,Database.Find(loan.player).name,"section-title");Text(card,ClubName(loan.parent)+" → "+ClubName(loan.club)+" · jusqu’au "+Core.Career.Epoch.AddDays(loan.loanUntil).ToString("dd MMM yyyy",French));Text(card,"Part du salaire pour le club d’accueil : "+(loan.terms?.loanWagePercent??50)+" % · salaire intégral "+Money(Core.Career.MonthlySalary(loan.wage))+" / mois");if(loan.terms?.obligationFee>0)Text(card,"Obligation : "+Money(loan.terms.obligationFee)+" · après "+loan.terms.obligationAppearances+" apparitions (0 = automatique)");
                if(loan.club==Career.club&&loan.terms?.optionFee>0)Button(card,"Lever l’option · "+Money(loan.terms.optionFee),()=>Confirm("Lever l’option ?","Le joueur rejoint définitivement votre club, avec prise en charge intégrale du salaire.",()=>RunDecision(()=>Career.ExerciseLoanOption(Database,loan.player))));
                if(loan.parent==Career.club&&loan.terms?.recall==true)Button(card,"Rappeler le joueur",()=>RunDecision(()=>Career.RecallLoan(Database,loan.player)));
            }
        }
        void ExtendedIntegrityCards(VisualElement parent)
        {
            if(!Career.life.corruptionEnabled||Career.world==null)return;
            foreach(var spec in new[]{("leak","Un intermédiaire fictif propose des documents obtenus irrégulièrement. Accélération limitée des observations, frais perdus en cas de refus et enquête possible."),("shadow","Un financement occulte temporaire. La totalité doit être remboursée à la clôture du dossier ; une amende peut s’y ajouter."),("pressure","Un réseau fictif promet un répit auprès du conseil. Gain de confiance faible, méfiance du vestiaire et risque de suspension.")}){var card=Card(parent);Text(card,Core.Career.SchemeName(spec.Item1),"section-title");Text(card,spec.Item2);Button(card,"Ouvrir · "+Money(Career.SchemeCost(spec.Item1)),()=>Confirm(Core.Career.SchemeName(spec.Item1),spec.Item2+" Confirmer ce choix fictif ?",()=>RunDecision(()=>Career.StartScheme(spec.Item1))));}
        }
    }
}
