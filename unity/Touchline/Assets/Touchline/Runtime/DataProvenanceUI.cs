using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        DataProvenance provenance;
        Dictionary<string,PlayerProvenance> playerProvenance;
        Dictionary<string,ClubProvenance> clubProvenance;
        void LoadProvenance()
        {
            if(provenance!=null)return;
            var asset=Resources.Load<TextAsset>("Data/data-provenance");
            provenance=asset==null?new DataProvenance():JsonUtility.FromJson<DataProvenance>(asset.text)??new DataProvenance();
            playerProvenance=(provenance.players??Array.Empty<PlayerProvenance>()).GroupBy(x=>x.id).ToDictionary(x=>x.Key,x=>x.First());
            clubProvenance=(provenance.clubs??Array.Empty<ClubProvenance>()).GroupBy(x=>x.id).ToDictionary(x=>x.Key,x=>x.First());
        }
        void ProfileProvenance(VisualElement body,PlayerData player,int knowledge)
        {
            LoadProvenance();body.name="profile-provenance";
            playerProvenance.TryGetValue(player.id,out var source);
            var contract=Career.world?.contracts.FirstOrDefault(c=>c.player==player.id);
            Text(body,"Sources et fiabilité","section-title");
            Text(body,"Les observations du staff mesurent votre connaissance en jeu. Elles ne rendent pas une estimation publique ou contractuelle plus certaine.","muted");
            var roster=ProvenanceCard(body,"IDENTITÉ ET EFFECTIF",source==null&&!string.IsNullOrEmpty(player.rosterSource)?"Identité issue d’un catalogue sourcé. Le relevé externe décrit une date donnée ; votre carrière fait ensuite évoluer le statut du joueur.":ProvenanceLabels.Roster(source,player.team));
            if(source!=null){
                ProfileFact(roster,"Effectif de départ",ClubName(source.baselineClub));
                ProfileFact(roster,"Date du relevé",ProvenanceLabels.Date(source.rosterAsOf));
                ProfileFact(roster,"Import du fichier",ProvenanceLabels.Date(provenance.importedAt));
                Text(roster,"La date d’import ne certifie pas que chaque affiliation a été revue ce jour-là.","footnote");
                ProvenanceLink(roster,source.rosterUrl,"Voir l’effectif source");ProvenanceLink(roster,source.identityUrl,"Voir l’identité source");
            }
            else if(!string.IsNullOrEmpty(player.rosterSource)){ProfileFact(roster,"Date du relevé",ProvenanceLabels.Date(player.rosterAsOf));ProvenanceLink(roster,player.rosterSource,"Consulter le catalogue source");}
            var assessment=ProvenanceCard(body,"ATTRIBUTS ET POTENTIEL",ProvenanceLabels.Assessment(source,knowledge));
            ProfileFact(assessment,"Relevé de la source",ProvenanceLabels.Date(source?.ratingDate));
            Text(assessment,"Potentiel, adaptation et développement sont simulés. Une note publiée par EA est une note de jeu ; elle ne vérifie ni salaire, ni contrat.","footnote");
            // A source date and publisher remain visible, never the hidden numeric rating or potential.
            ProvenanceLink(assessment,source?.ratingUrl,"Consulter l’éditeur des attributs");
            if(source==null&&!string.IsNullOrEmpty(player.assessment))Text(assessment,player.assessment,"footnote");
            foreach(var evidence in source?.performanceEvidence??Array.Empty<PlayerPerformanceEvidence>()){
                if(evidence==null||evidence.metric!="topSpeed"||evidence.unit!="km/h"||double.IsNaN(evidence.value)||double.IsInfinity(evidence.value)||evidence.value<10||evidence.value>45)continue;
                var performance=ProvenanceCard(body,"MESURE PUBLIÉE · "+evidence.publisher,"Une observation réelle et une note du jeu sont deux informations différentes.");
                performance.name="player-performance-evidence";
                ProfileFact(performance,"Vitesse de pointe observée",evidence.value.ToString("0.00",French)+" km/h");
                ProfileFact(performance,"Saison de la mesure",evidence.season);ProfileFact(performance,"Source consultée le",ProvenanceLabels.Date(evidence.observedAt));
                Text(performance,evidence.scope,"footnote");
                Text(performance,"La vitesse de jeu reste une estimation. Les attributs masqués ne sont pas révélés par cette mesure publique.","footnote");
                ProvenanceLink(performance,evidence.url,"Consulter les mesures officielles");
            }
            var value=ProvenanceCard(body,"VALEUR DE TRANSFERT","La valeur affichée dans la carrière est une estimation de négociation, pas le prix d’une transaction réelle.");
            if(source?.referenceValue>0&&!string.IsNullOrEmpty(source.valueUrl)){
                ProfileFact(value,"Référence Transfermarkt importée",Money(source.referenceValue));ProfileFact(value,"Date de cette estimation",ProvenanceLabels.Date(source.valueDate));
                Text(value,"Référence historique du jeu de données importé ; pas une nouvelle vérification de la cote actuelle. L’ancienneté peut rendre cette estimation peu représentative.","footnote");ProvenanceLink(value,source.valueUrl,"Voir la référence de valorisation");
            }else Text(value,player.valueSource??"Aucune cote externe datée n’est documentée pour ce joueur. Estimation de simulation.","footnote");
            var salary=ProvenanceCard(body,"SALAIRE",ProvenanceLabels.Salary(contract?.estimated,source));if(contract?.estimated!=false&&!string.IsNullOrEmpty(player.salarySource))Text(salary,player.salarySource,"footnote");
            if(!string.IsNullOrEmpty(source?.salaryUrl)){
                ProfileFact(salary,"Référence",source.salaryProvider);ProfileFact(salary,"Date du relevé",ProvenanceLabels.Date(source.salaryDate));
                if(source.annualSalaryBase>0)ProfileFact(salary,"Base brute annuelle de référence",source.annualSalaryBase.ToString("N0",French)+" "+source.salaryCurrency);
                if(source.annualGuaranteedCompensation>0){ProfileFact(salary,"Compensation garantie de référence",source.annualGuaranteedCompensation.ToString("N0",French)+" "+source.salaryCurrency);Text(salary,"Ces deux montants ne sont pas additionnés pour calculer le salaire de la carrière.","footnote");}
                ProvenanceLink(salary,source.salaryUrl,"Consulter la référence salariale");
            }
            ProvenanceCard(body,"CONTRAT",ProvenanceLabels.Contract(contract?.estimated));
            Text(body,"Les sources sont consultables en ligne ; les indications de provenance restent disponibles hors connexion.","footnote");
        }
        VisualElement ProvenanceCard(VisualElement parent,string heading,string description)
        {
            var card=Card(parent,"provenance-card");Text(card,heading,"eyebrow");Text(card,description);return card;
        }
        void ProvenanceLink(VisualElement parent,string url,string label)
        {
            if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")return;
            var button=Button(parent,label+" ↗",()=>Application.OpenURL(uri.AbsoluteUri));button.AddToClassList("provenance-link");button.tooltip=uri.Host;
        }
        void ClubProvenancePanel(string id)
        {
            LoadProvenance();var club=Database.clubs.FirstOrDefault(c=>c.id==id);if(club==null)return;
            var panel=Modal("Sources · "+ClubName(id));panel.name="club-provenance";var body=Scroll(panel);
            clubProvenance.TryGetValue(id,out var source);
            var roster=ProvenanceCard(body,"EFFECTIF","L’effectif affiché suit les transferts de votre carrière. La source ci-dessous concerne la base de départ.");
            ProfileFact(roster,"Date du relevé",ProvenanceLabels.Date(source?.rosterAsOf??club.rosterAsOf));ProfileFact(roster,"Import du fichier",ProvenanceLabels.Date(provenance.importedAt));
            Text(roster,"Import et vérification individuelle sont deux dates différentes. Sans date de relevé documentée, l’actualité de l’effectif reste à confirmer.","footnote");ProvenanceLink(roster,source?.rosterUrl??club.rosterSource,"Voir l’effectif source");
            var model=ProvenanceCard(body,"FINANCES DE VOTRE PARTIE","Ces ressources évoluent dans la simulation. Les comptes publiés plus bas sont des références historiques séparées.");
            ProfileFact(model,"Base annuelle du modèle",Money(club.annualRevenue));Text(model,club.financeSource??"Modèle Touchline · estimation","footnote");
            if(id==Career.club&&Career.world!=null&&Career.world.managerStatus=="employed"){
                ProfileFact(model,"Trésorerie dans votre carrière",Money(Career.life.cash));ProfileFact(model,"Enveloppe de transferts en jeu",Money(Career.TransferBudget));
            }
            Text(body,"Comptes publiés","section-title");
            var references=(provenance.financialReferences??Array.Empty<FinancialReference>()).Where(r=>r.club==id).ToArray();
            if(references.Length==0)Text(body,"Aucune référence primaire vérifiée n’est jointe pour ce club dans cette version. Ses revenus de jeu restent une estimation ; ils ne doivent pas être présentés comme son budget réel.","notice");
            foreach(var reference in references){
                string heading=reference.metric=="cash"?"TRÉSORERIE PUBLIÉE À UNE DATE":reference.metric=="personnel"?"COÛT HISTORIQUE DU PERSONNEL":reference.metric=="operating-cost"?"AUTRES CHARGES PUBLIÉES":"REVENUS ANNUELS PUBLIÉS";
                var card=ProvenanceCard(body,heading,reference.label+" : "+reference.amount.ToString("N0",French)+" €");
                ProfileFact(card,"Exercice",reference.period);if(!string.IsNullOrEmpty(reference.asOf))ProfileFact(card,"Clôture",ProvenanceLabels.Date(reference.asOf));
                Text(card,reference.scope,"muted");ProfileFact(card,"Source primaire",reference.publisher);ProfileFact(card,"Publication",ProvenanceLabels.Date(reference.publishedAt));ProfileFact(card,"Vérification de la source",ProvenanceLabels.Date(reference.checkedAt));ProvenanceLink(card,reference.url,"Lire la publication officielle");
            }
            Text(body,"Les périmètres comptables diffèrent selon les clubs. Ces montants ne sont ni additionnés entre eux ni injectés dans votre sauvegarde.","footnote");
            Button(panel,"Retour au club",()=>ClubProfile(id));
        }
    }
}
