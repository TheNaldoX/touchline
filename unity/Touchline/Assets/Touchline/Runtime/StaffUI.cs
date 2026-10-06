using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string staffSearch="",staffFilter="Tous";int staffPage;
        static string StaffRole(string role)=>role=="assistant"?"Adjoint":role=="fitness"?"Préparateur physique":role=="scout"?"Recruteur":"Formation";
        void StaffPage()
        {
            Career.EnsureStaffMarket(Database);var s=Scroll(content);Heading(s,"Votre équipe derrière l’équipe");Text(s,"STAFF ET DÉLÉGATION","eyebrow");
            Text(s,"Les identités réelles comportent une source ; les compléments sont fictifs. Les notes sur 20, salaires et échéances initiaux sont des estimations de simulation.","notice");
            var plan=Career.life.staff;var delegations=Card(s);delegations.name="staff-delegations";Text(delegations,"Qui prend les décisions ?","section-title");
            foreach(var item in new[]{("training","Charge et programme d’entraînement",plan.training),("lineup","Composition proposée avant les matchs",plan.lineup),("youth","Axes de formation et mentorat",plan.youth),("scouting","Observation de ma sélection de joueurs",plan.scouting),("press","Conférences d’avant-match",plan.press)}){var row=new VisualElement{name="staff-delegation-"+item.Item1};row.AddToClassList("staff-delegation-row");delegations.Add(row);var toggle=new Toggle(item.Item2){value=item.Item3,name="staff-delegation-toggle-"+item.Item1};row.Add(toggle);toggle.SetEnabled(PlayerManagementAvailable);toggle.RegisterValueChangedCallback(e=>RunDecision(()=>Career.SetDelegation(item.Item1,e.newValue)));Text(row,StaffDelegationStatus(item.Item1,item.Item3),"muted").name="staff-delegation-status-"+item.Item1;}
            Text(delegations,"Vos choix tactiques sont conservés. Le staff rend compte par message ; les contrats, promotions et interventions médicales restent vos décisions.","muted");
            var budget=new LongField("Plafond hebdomadaire d’observation (€)"){value=plan.scoutingWeeklyLimit};delegations.Add(budget);Button(delegations,"Enregistrer le plafond",()=>RunDecision(()=>{if(budget.value<0||budget.value>Career.life.revenue/52)throw new InvalidOperationException("Plafond invalide.");plan.scoutingWeeklyLimit=budget.value;}));
            StaffTrainingPanel(s);
            Heading(s,"Responsables actuels");foreach(var role in new[]{"assistant","fitness","scout","youth"}){var member=Career.Staff(role);var card=Card(s);Text(card,StaffRole(role)+" · "+member.name,"section-title");if(member.wage>0){StaffSummary(card,member);Button(card,"Contrat / agent",()=>StaffDialog(member.id));var train=PlayerManagementButton(card,"Formation · "+Money(Career.StaffTrainingCost),()=>Confirm("Former "+member.name+" ?","Programme de 45 jours, payé maintenant : "+Money(Career.StaffTrainingCost)+". La formation est nominative. Un départ ou une fin de contrat interrompt le programme ; les frais engagés restent à la charge du club.",()=>RunDecision(()=>Career.TrainStaff(role))));train.name="staff-training-start-"+role;
                bool qualified=member.tactics>=20&&member.coaching>=20&&member.judging>=20&&member.people>=20;
                train.SetEnabled(PlayerManagementAvailable&&!Career.StaffTrainingInProgress&&!qualified&&member.until>Career.life.day&&!string.IsNullOrEmpty(member.id)&&member.club==Career.club);
                if(Career.StaffTrainingInProgress)train.tooltip="Une seule formation à la fois. "+Career.StaffTrainingStatus();else if(qualified)train.tooltip="Les quatre compétences ont atteint la limite de 20.";else if(string.IsNullOrEmpty(member.id)||member.club!=Career.club)train.tooltip="Le responsable doit avoir une identité vérifiable dans votre staff.";else if(member.until<=Career.life.day)train.tooltip="Renouvelez son contrat avant d’engager une formation.";
                if(member.until>Career.life.day&&member.until<=Career.life.day+45)Text(card,"Contrat jusqu’au "+Core.Career.Epoch.AddDays(member.until).ToString("dd/MM/yyyy")+" : renouvelez-le pour éviter l’interruption du programme.","notice");Button(card,"Licencier",()=>Confirm("Rompre le contrat de "+member.name+" ?","Indemnité simulée : "+Money(Career.StaffReleaseCost(member))+". Sa délégation sera suspendue jusqu’au remplacement.",()=>RunDecision(()=>Career.FireStaff(Database,member.id))));}else Text(card,"Poste vacant : embauchez un responsable pour réactiver ses délégations.","notice");}
            TacticalAdvicePanel(s);
            Heading(s,"Marché du staff");var search=new TextField("Nom"){value=staffSearch};s.Add(search);search.RegisterValueChangedCallback(e=>staffSearch=e.newValue);Button(s,"Rechercher",()=>{staffPage=0;Build();});
            var filter=new DropdownField("Disponibilité",new List<string>{"Tous","Libres","Sous contrat","Réels"},new List<string>{"Tous","Libres","Sous contrat","Réels"}.IndexOf(staffFilter));s.Add(filter);filter.RegisterValueChangedCallback(e=>{staffFilter=e.newValue;staffPage=0;Build();});
            var candidates=Career.staffMarket.Where(m=>!plan.members.Any(x=>x.id==m.id)&&m.name.IndexOf(staffSearch,StringComparison.OrdinalIgnoreCase)>=0&&(staffFilter=="Tous"||staffFilter=="Libres"&&m.club==null||staffFilter=="Sous contrat"&&m.club!=null||staffFilter=="Réels"&&!m.fictional)).OrderBy(m=>m.fictional).ThenByDescending(m=>m.tactics).ToList();
            foreach(var m in candidates.Skip(staffPage*24).Take(24)){var card=Card(s);Text(card,m.name+" · "+StaffRole(m.role),"section-title");StaffSummary(card,m);Button(card,"Parler à l’agent",()=>StaffDialog(m.id));}
            var pages=Row(s);if(staffPage>0)Button(pages,"Précédents",()=>{staffPage--;Build();});if((staffPage+1)*24<candidates.Count)Button(pages,"Suivants",()=>{staffPage++;Build();});
            Heading(s,"Discussions en cours");foreach(var o in Career.staffOffers.Where(o=>o.club==Career.club).Reverse().Take(20)){var member=Career.staffMarket.First(m=>m.id==o.staff);var card=Card(s);Text(card,member.name+" · "+o.status,"section-title");Text(card,Money(Core.Career.MonthlySalary(o.wage))+" / mois · "+o.years+" ans · indemnité "+Money(o.compensation));if(o.status=="accepted")Button(card,"Signer",()=>RunDecision(()=>Career.SignStaffContract(Database,o.staff)));if(o.status=="counter")Button(card,"Reprendre la discussion",()=>StaffDialog(o.staff));}
        }
        string StaffDelegationStatus(string responsibility,bool requested)
        {
            if(!requested)return "À votre charge";
            string role=responsibility=="training"?"fitness":responsibility=="youth"?"youth":responsibility=="scouting"?"scout":"assistant";
            var member=Career.Staff(role);
            if(member.wage<=0)return "En attente · poste vacant ("+StaffRole(role)+"). Votre préférence est conservée ; vous gardez la main.";
            if(Career.world!=null&&Career.world.managerStatus!="employed")return "En attente · vous n’êtes plus en poste dans ce club.";
            if(Career.life.managerBanUntil>Career.life.day)return "Suspendue jusqu’à votre retour de suspension.";
            string rhythm=responsibility=="training"?"programme ajusté chaque jour":responsibility=="youth"?"axes et mentors réévalués chaque semaine":responsibility=="scouting"?"sélection examinée chaque semaine, selon le plafond et les places disponibles":responsibility=="lineup"?"onze proposé à l’approche du match":"conférence à la veille du match";
            return member.name+" · "+rhythm+".";
        }
        void StaffTrainingPanel(VisualElement parent)
        {
            var card=Card(parent);card.name="staff-training-progress";Text(card,"Formation professionnelle","section-title");
            var status=Text(card,Career.StaffTrainingStatus(),Career.StaffTrainingInProgress?"notice":"muted");status.name="staff-training-status";
            Text(card,"Un programme dure 45 jours et coûte "+Money(Career.StaffTrainingCost)+". Le programme travaille les compétences tactiques, pédagogiques, relationnelles et les capacités de jugement du responsable. Son bilan sera disponible dans le staff.","muted");
            if(Career.StaffTrainingInProgress)Text(card,"Le secrétariat vous préviendra de la fin du programme. Si le responsable quitte son poste avant son terme, les frais engagés restent à la charge du club.","footnote");
        }
        void StaffSummary(VisualElement panel,StaffMember m)
        {
            Text(panel,(m.fictional?"Fictif":"Identité réelle")+" · "+(m.club==null?"Libre":ClubName(m.club))+" · "+Money(Core.Career.MonthlySalary(m.wage))+" / mois","muted");Text(panel,"Tactique "+m.tactics+" · Entraînement "+m.coaching+" · Évaluation "+m.judging+" · Relationnel "+m.people+" / 20");Text(panel,m.biography??"Profil de simulation.","footnote");
            if(m.source!=null)Text(panel,"Source : "+m.source,"footnote");
        }
        void StaffDialog(string id)
        {
            var member=Career.staffMarket.First(m=>m.id==id);var accepted=Career.staffOffers.LastOrDefault(o=>o.staff==id&&o.club==Career.club&&o.status=="accepted"&&o.due+7>=Career.life.day);if(accepted!=null){StaffAgreementReview(accepted);return;}var panel=Modal("Agent · "+member.name);var s=Scroll(panel);var range=Career.StaffAgent(id);var bubble=Card(s);Text(bubble,"AGENT","eyebrow");Text(bubble,range.message);Text(bubble,"Indemnité : "+Money(member.club==Career.club?0:range.feeLow)+" · Salaire souhaité : "+Money(range.monthlyLow)+" à "+Money(range.monthlyHigh)+" / mois");
            var wage=new LongField("Votre proposition mensuelle (€)"){value=range.monthlyHigh};s.Add(wage);var years=new IntegerField("Durée (1 à 5 ans)"){value=2};s.Add(years);Button(s,"Envoyer à l’agent",()=>RunDecision(()=>Career.ProposeStaffContract(Database,id,wage.value,years.value))).AddToClassList("primary");
        }
        void TacticalAdvicePanel(VisualElement parent)
        {
            if(Career.world==null)return;Career.EnsureStaffMarket(Database);if(Career.Staff("assistant").wage<=0){Text(parent,"Le poste d’adjoint est vacant. Recrutez un adjoint pour disposer de son analyse.","notice");return;}var box=new Foldout{text="Réunion tactique · "+Career.Staff("assistant").name+" · "+Career.Staff("assistant").tactics+" / 20",value=false};parent.Add(box);
            Text(box,"Un adjoint plus qualifié produit un diagnostic plus détaillé. La confiance indique la qualité estimée de son analyse, jamais une probabilité de victoire.","footnote");var advice=Career.TacticalReport(Database);
            if(advice.Count==0)Text(box,"Aucune contradiction majeure détectée dans les consignes. Cela ne garantit pas le résultat : surveillez les espaces, les occasions et la fatigue pendant le match.");
            foreach(var a in advice){var c=Card(box);Text(c,a.title,"section-title");Text(c,a.evidence);Text(c,a.tradeoff,"muted");Text(c,"Confiance du rapport : "+a.confidence+" / 100","footnote");if(Career.match==null||Career.match.finished)Button(c,"Appliquer ce conseil",()=>RunDecision(()=>Career.ApplyStaffAdvice(Database,a.key)));}
        }
    }
}
