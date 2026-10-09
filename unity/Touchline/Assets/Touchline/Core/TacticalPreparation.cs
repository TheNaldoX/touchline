using System;
using System.Collections.Generic;
using System.Linq;

namespace Touchline.Core
{
    [Serializable] public class FormationFamiliarity { public string formation;public float value; }
    // Ce que le groupe a travaillé entre les matchs. Familiarité 0–100,
    // préparation physique et coups de pied arrêtés 0–100 (50 = neutre).
    [Serializable] public class TacticalPreparation
    {
        public List<FormationFamiliarity> formations=new List<FormationFamiliarity>();
        // Consignes collectives réellement travaillées : ligne, pressing, largeur, rythme, jeu direct, mentalité.
        public float[] style=new float[0];
        public float conditioning=50,setPieces=50;
        public string focus="balanced";
        // Premier jour de suivi : −1 = carrière créée avant cette fonction (tout l'effectif est installé).
        public int trackingFrom=-1;
    }

    public partial class Career
    {
        public static readonly string[] TrainingFocuses={"balanced","tactical","physical","setpieces","recovery"};
        public static string TrainingFocusLabel(string focus)=>focus switch{"tactical"=>"Tactique","physical"=>"Physique","setpieces"=>"Coups de pied arrêtés","recovery"=>"Récupération active",_=>"Équilibré"};
        public static string TrainingFocusHint(string focus)=>focus switch{
            "tactical"=>"Automatismes du système et des consignes plus vite ; peu de travail physique.",
            "physical"=>"Meilleure endurance en match, mais fraîcheur entamée et risque de blessure accru.",
            "setpieces"=>"Corners et coups francs mieux livrés ; les automatismes progressent peu.",
            "recovery"=>"Fraîcheur et moins de blessures ; endurance et automatismes progressent peu.",
            _=>"Un peu de tout, sans compromis marqué."};
        // Familiarité d'une équipe installée dans son système (référence neutre du moteur).
        public const float EstablishedFamiliarity=75,NewFormationFamiliarity=35,FamiliarityFloor=30;
        public const float FamiliarityReference=85,CohesionReference=95;
        // Pondération de la compréhension : familiarité 60 %, cohésion 40 %, écart de 40 points = effet maximal.
        public const float UnderstandingSpan=40,FamiliarityWeight=.6f,CohesionWeight=.4f;
        // Un nouveau venu est intégré après 90 jours ou 12 matchs de 30 minutes.
        public const float CohesionSettleDays=90,CohesionSettleMatches=12;
        // Écart moyen des consignes (0–1) ×2,5 = perte de familiarité des consignes.
        public const float StyleDistancePenalty=2.5f;
        // Récupération et risque selon le thème d'entraînement (points de condition par jour, multiplicateur de risque).
        public const float RecoveryFocusFitness=1.5f,PhysicalFocusFitness=-.8f,PhysicalInjuryFactor=1.35f,RecoveryInjuryFactor=.6f;
        public const float MatchFamiliarityGain=3;
        // joinedDay : −1 = joueur pas encore repéré ; les joueurs présents au début du suivi
        // reçoivent une ancienneté fictive très large (jours) pour compter comme installés.
        public const int UnseenPlayer=-1,FounderSeniorityDays=100000;
        TacticalPreparation Preparation=>life.preparation??=new TacticalPreparation();
        public string TrainingFocus=>world?.trainingFocus??Preparation.focus??"balanced";
        public void SetTrainingFocus(string focus)
        {
            if(Array.IndexOf(TrainingFocuses,focus)<0)throw new ArgumentException("Thème d’entraînement inconnu.");
            OffPitch();Preparation.focus=focus;if(world!=null)world.trainingFocus=focus;
        }
        static float[] StyleOf(Tactic t)=>new[]{t.line,t.pressing,t.width,t.tempo,t.directness,t.mentality};
        FormationFamiliarity FormationEntry(string formation)
        {
            var prep=Preparation;var entry=prep.formations.FirstOrDefault(f=>f.formation==formation);
            if(entry==null){entry=new FormationFamiliarity{formation=formation,value=NewFormationFamiliarity};prep.formations.Add(entry);}
            return entry;
        }
        // Repère les arrivées sans toucher aux modules de recrutement : un joueur
        // inconnu après le début du suivi est une recrue du jour.
        public void TrackPreparation()
        {
            if(life==null)return;var prep=Preparation;
            if(prep.trackingFrom<0){
                prep.trackingFrom=life.day;
                foreach(var p in life.players)if(p.joinedDay==UnseenPlayer)p.joinedDay=life.day-FounderSeniorityDays;
                if(!prep.formations.Any(f=>f.formation==tactic.formation))prep.formations.Add(new FormationFamiliarity{formation=tactic.formation,value=EstablishedFamiliarity});
                if(prep.style==null||prep.style.Length!=6)prep.style=StyleOf(tactic);
            }
            foreach(var p in life.players)if(p.joinedDay==UnseenPlayer)p.joinedDay=life.day;
            if(prep.style==null||prep.style.Length!=6)prep.style=StyleOf(tactic);
        }
        public float FormationFamiliarityValue(string formation)=>Preparation.formations.FirstOrDefault(f=>f.formation==formation)?.value??NewFormationFamiliarity;
        public float StyleFamiliarity()
        {
            var style=Preparation.style;if(style==null||style.Length!=6)return 100;var current=StyleOf(tactic);
            float distance=0;for(int i=0;i<6;i++)distance+=Math.Abs(current[i]-style[i]);distance/=6;
            return 100*(1-Mathx.Clamp(distance*StyleDistancePenalty,0,1));
        }
        public float TacticalFamiliarity()=>FormationFamiliarityValue(tactic.formation)*.6f+StyleFamiliarity()*.4f;
        public float Settledness(string id)
        {
            var p=life.players.FirstOrDefault(x=>x.id==id);if(p==null)return 0;if(p.joinedDay==UnseenPlayer)return 1;
            return Mathx.Clamp(Math.Max((life.day-p.joinedDay)/CohesionSettleDays,p.appearances/CohesionSettleMatches),0,1);
        }
        public float Cohesion(IEnumerable<string> lineupIds)
        {
            var ids=lineupIds?.ToArray()??Array.Empty<string>();return ids.Length==0?100:100*ids.Average(Settledness);
        }
        public static float UnderstandingFrom(float familiarity,float cohesion)=>Mathx.Clamp((familiarity-FamiliarityReference)/UnderstandingSpan*FamiliarityWeight+(cohesion-CohesionReference)/UnderstandingSpan*CohesionWeight,-1,1);
        public float FocusRecoveryBonus()=>TrainingFocus=="recovery"?RecoveryFocusFitness:TrainingFocus=="physical"?PhysicalFocusFitness:0;
        public float FocusInjuryFactor()=>TrainingFocus=="physical"?PhysicalInjuryFactor:TrainingFocus=="recovery"?RecoveryInjuryFactor:1;
        // Une journée d'entraînement (appelée par AdvanceDay).
        public void PreparationDay()
        {
            TrackPreparation();var prep=Preparation;string focus=TrainingFocus;bool resting=life.training=="rest";
            float gain=(focus=="tactical"?2.5f:focus=="physical"?.4f:focus=="setpieces"?.6f:focus=="recovery"?.3f:1f)*(resting?.5f:1);
            float styleRate=focus=="tactical"?.15f:focus=="balanced"?.06f:.03f;
            foreach(var f in prep.formations)if(f.formation!=tactic.formation)f.value=Math.Max(FamiliarityFloor,f.value-.25f);
            var current=FormationEntry(tactic.formation);current.value=Math.Min(100,current.value+gain);
            var target=StyleOf(tactic);for(int i=0;i<6;i++)prep.style[i]+=(target[i]-prep.style[i])*styleRate;
            prep.conditioning=Mathx.Clamp(prep.conditioning+(focus=="physical"?1.2f:focus=="recovery"?-.4f:(50-prep.conditioning)*.02f),0,100);
            prep.setPieces=Mathx.Clamp(prep.setPieces+(focus=="setpieces"?2f:(50-prep.setPieces)*.02f),0,100);
        }
        public void PreparationAfterMatch()
        {
            if(life==null||match==null)return;TrackPreparation();var entry=FormationEntry(tactic.formation);entry.value=Math.Min(100,entry.value+MatchFamiliarityGain);
            var target=StyleOf(tactic);var style=Preparation.style;for(int i=0;i<6;i++)style[i]+=(target[i]-style[i])*.1f;
        }
        // Automatismes, cohésion et nerfs injectés dans le match de carrière.
        public void PrepareMatchMindset(Database db,MatchSimulation simulation)
        {
            if(life==null)return;TrackPreparation();var m=simulation.State;float importance=MatchImportance(db);
            var ours=TeamMindset.Neutral();var theirs=TeamMindset.Neutral();
            var ids=m.actors.Where(a=>a.side==0).OrderBy(a=>a.slot).Select(a=>a.id).ToArray();
            ours.familiarity=TacticalFamiliarity();ours.cohesion=Cohesion(ids);ours.understanding=UnderstandingFrom(ours.familiarity,ours.cohesion);
            ours.conditioning=(Preparation.conditioning-50)/50;ours.setPieces=(Preparation.setPieces-50)/50;
            foreach(var a in m.actors){var p=db?.Find(a.id)??simulation.Player(a.id);if(p==null)continue;(a.side==0?ours:theirs).composure[a.slot]=TeamTalks.BigMatchNerves(p,importance);}
            m.mindset=new[]{ours,theirs};
        }
    }
}
