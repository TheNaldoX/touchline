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
        string comparisonPlayer,comparisonClub;
        void ComparePlayer(string id)
        {
            if(comparisonClub!=Career.club){comparisonPlayer=null;comparisonClub=Career.club;}
            var player=Database.Find(id);if(player==null)return;
            string other=comparisonPlayer!=id?comparisonPlayer:null;
            if(Database.Find(other)==null||Database.Find(other).position!=player.position)other=Database.Squad(Career.club).Where(p=>p.id!=id).OrderBy(p=>p.position==player.position?0:1).ThenBy(p=>p.name).FirstOrDefault()?.id;
            comparisonPlayer=id;if(other==null){Message("Deux joueurs sont nécessaires pour comparer les profils.");return;}ComparePlayers(id,other);
        }
        void ComparePlayers(string leftId,string rightId)
        {
            if(arena!=null)arena.Paused=true;
            var panel=Modal("Comparer les joueurs");panel.name="player-comparison";panel.AddToClassList("comparison-panel");
            var candidates=Database.Squad(Career.club).Concat(Career.shortlist.Select(Database.Find).Where(p=>p!=null)).Concat(new[]{Database.Find(leftId),Database.Find(rightId)}).Where(p=>p!=null).GroupBy(p=>p.id).Select(g=>g.First()).OrderBy(p=>p.name).ToList();
            var pickers=Row(panel,"comparison-pickers");
            var left=new DropdownField(candidates.Select(p=>p.name).ToList(),Math.Max(0,candidates.FindIndex(p=>p.id==leftId))){name="comparison-left"};pickers.Add(left);
            var right=new DropdownField(candidates.Select(p=>p.name).ToList(),Math.Max(0,candidates.FindIndex(p=>p.id==rightId))){name="comparison-right"};pickers.Add(right);
            Text(panel,"Choix : votre effectif et votre sélection de recrutement. Ouvrez une autre fiche pour comparer un nouveau joueur.","comparison-hint");
            var body=Scroll(panel);body.name="comparison-body";body.AddToClassList("comparison-body");
            void Populate()
            {
                body.Clear();var a=candidates[left.index];var b=candidates[right.index];comparisonPlayer=a.id;
                int ka=Career.Knowledge(a.id),kb=Career.Knowledge(b.id);
                var identities=Row(body,"comparison-identities");Identity(identities,a,ka);Identity(identities,b,kb);
                if(a.id==b.id){Text(body,"Choisissez deux joueurs différents pour comparer leurs profils.","notice");return;}
                Fact("Âge",a.age+" ans",b.age+" ans");Fact("Postes",FrenchFootballPositions.List(a.positions??new[]{a.position}),FrenchFootballPositions.List(b.positions??new[]{b.position}));
                Fact("Valeur estimée",Money(a.value),Money(b.value));Fact("Salaire / mois",Money(Core.Career.MonthlySalary(a.wage)),Money(Core.Career.MonthlySalary(b.wage)));
                var ra=Career.ScoutReportCard(Database,a.id,CachedRecruitmentOverview());var rb=Career.ScoutReportCard(Database,b.id,CachedRecruitmentOverview());
                Fact("Note recrutement",ra.grade,rb.grade);Fact("Niveau estimé",ra.ability.ToString(),rb.ability.ToString());Fact("Potentiel estimé",ra.potential.ToString(),rb.potential.ToString());
                Text(body,"ATTRIBUTS CLÉS / SUR 20","eyebrow");
                Text(body,"Les différences ne sont colorées que si les deux joueurs sont suffisamment connus. Une fourchette reste incertaine.","comparison-hint");
                var specs=a.Goalkeeper||b.Goalkeeper?new[]{"gkDiving|Plongeon","gkHandling|Prise de balle","gkKicking|Relance au pied","gkPositioning|Placement gardien","gkReflexes|Réflexes","shortPassing|Passes courtes","reactions|Réactivité","jumping|Détente","strength|Puissance"}:new[]{"ballControl|Contrôle","dribbling|Dribble","shortPassing|Passes courtes","longPassing|Passes longues","finishing|Finition","shotPower|Puissance de frappe","longShots|Tirs de loin","crossing|Centres","headingAccuracy|Jeu de tête","standingTackle|Tacle debout","slidingTackle|Tacle glissé","vision|Vision","composure|Sang-froid","reactions|Réactivité","positioning|Placement offensif","defensiveAwareness|Placement défensif","interceptions|Anticipation","acceleration|Accélération","sprintSpeed|Vitesse","agility|Agilité","balance|Équilibre","stamina|Endurance","strength|Puissance","jumping|Détente"};
                foreach(var spec in specs){var pair=spec.Split('|');bool hasA=a.attributes?.Any(v=>v.key==pair[0])==true,hasB=b.attributes?.Any(v=>v.key==pair[0])==true;if(!hasA&&!hasB)continue;
                    var va=Career.AssessedAttribute(Database,a.id,pair[0]);var vb=Career.AssessedAttribute(Database,b.id,pair[0]);
                    var row=Row(body,"comparison-stat");row.name="comparison-stat-"+pair[0];Text(row,pair[1],"comparison-stat-label");
                    var av=Text(row,va.ToString(),"comparison-stat-value");var bv=Text(row,vb.ToString(),"comparison-stat-value");
                    if(ka>=90&&kb>=90&&va.known&&vb.known&&hasA&&hasB){av.EnableInClassList("comparison-better",va.low>vb.high);bv.EnableInClassList("comparison-better",vb.low>va.high);}
                }
                var actions=Row(body,"comparison-actions");Button(actions,"Fiche · "+a.name,()=>PlayerProfile(a.id,()=>ComparePlayers(a.id,b.id),"Retour à la comparaison"));Button(actions,"Fiche · "+b.name,()=>PlayerProfile(b.id,()=>ComparePlayers(a.id,b.id),"Retour à la comparaison"));
                void Fact(string label,string av,string bv){var row=Row(body,"comparison-stat");Text(row,label,"comparison-stat-label");Text(row,av,"comparison-stat-value");Text(row,bv,"comparison-stat-value");}
            }
            void Identity(VisualElement parent,PlayerData p,int knowledge){var card=Card(parent,"comparison-identity");Text(card,p.name,"section-title");Text(card,ClubName(p.team),"muted");Text(card,"Connaissance : "+knowledge+" %","comparison-hint");}
            left.RegisterValueChangedCallback(_=>Populate());right.RegisterValueChangedCallback(_=>Populate());Populate();
        }
        internal static string ComparisonValue(int value,int knowledge,bool available)=>!available||knowledge<40?"—":knowledge<90?Math.Max(1,value-3)+"–"+Math.Min(20,value+3):value.ToString();
    }
}
