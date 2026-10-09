using System;
using Touchline.Core;

namespace Touchline
{
    public enum MatchViewingMode { Full=0, KeyMoments=1, Highlights=2, Extended=3 }
    // Selects presentation and pacing only. All decisions, injuries and ball
    // contacts still run through the same fixed-step simulation and RNG.
    public sealed class MatchBroadcast
    {
        readonly MatchSimulation simulation;
        readonly ExitBroadcastWindow exitWindow=new ExitBroadcastWindow();
        int observedEvents;
        float visibleUntil;
        int presentationSpeed=1;
        string protectedReason;float protectedUntil;
        string threatOwner,flightSource,flightKind;int threatPeriod,flightSequence=-1;Point threatPosition;float previousFlightElapsed;
        public MatchViewingMode Mode {get;private set;}=MatchViewingMode.Full;
        // Compatibility for old callers: the previous highlights switch means
        // the standard selection, not the new minimal selection.
        public bool Enabled {get=>Mode!=MatchViewingMode.Full;set{if(!value)Mode=MatchViewingMode.Full;else if(!Enabled)Mode=MatchViewingMode.Highlights;}}
        public static MatchViewingMode ResolveMode(int savedMode,int legacyHighlights)=>savedMode>=0&&savedMode<=3?(MatchViewingMode)savedMode:legacyHighlights==0?MatchViewingMode.Full:MatchViewingMode.Highlights;
        public static string ModeName(MatchViewingMode mode)=>mode switch{MatchViewingMode.Full=>"Match complet",MatchViewingMode.KeyMoments=>"Moments clés",MatchViewingMode.Extended=>"Temps forts étendus",_=>"Temps forts"};
        public static string ModeDescription(MatchViewingMode mode)=>mode switch{
            MatchViewingMode.Full=>"Toute la rencontre en 3D, à la vitesse choisie.",
            MatchViewingMode.KeyMoments=>"Occasions franches, frappes, buts et incidents majeurs. Moins de construction montrée.",
            MatchViewingMode.Extended=>"Davantage de construction, de transitions et d’actions sur les ailes pour analyser votre tactique.",
            _=>"Actions dangereuses et approches de la surface, avec un suivi statistique entre les séquences."};
        public void SetMode(MatchViewingMode mode)
        {
            Mode=ResolveMode((int)mode,1);threatOwner=flightSource=flightKind=null;flightSequence=-1;
            ShowLive(4,"Changement de visionnage");Refresh();
        }
        public int QuietSpeed=12;
        public bool Quiet {get;private set;}
        public bool PauseRequested {get;private set;}
        public string Reason {get;private set;}="Début de rencontre";
        public string QuietSituation
        {
            get{
                var m=simulation.State;
                if(m.halfTime)return "Mi-temps · préparez vos ajustements";
                if(m.finished)return "Match terminé";
                if(m.restart>0){string phase=m.phase switch{"throw-in"=>"Touche","goal-kick"=>"Six mètres","corner"=>"Corner","free-kick"=>"Coup franc","penalty"=>"Penalty","goal"=>"Après le but","kickoff"=>"Engagement",_=>"Arrêt de jeu"};return phase+" · préparation, reprise dans "+Math.Max(1,(int)Math.Ceiling(m.restart))+" s";}
                if(m.ball.held)return "Gardien en possession · préparation de la relance";
                int side=MatchSimulation.PossessionSide(m);if(side<0)side=m.ball.side;
                float depth=m.ball.position.x*simulation.Direction(side);
                string action=m.ball.elapsed>=0?m.ball.kind switch{"cross"=>"Centre en cours","through"=>"Passe dans la profondeur","clearance"=>"Dégagement","keeper-roll"=>"Relance au sol","keeper-throw"=>"Relance à la main",_=>"Circulation du ballon"}:m.clock-m.turnoverAt<4?"Transition après récupération":depth>18?"Construction dans le camp adverse":depth< -18?"Relance depuis la défense":"Construction au milieu";
                float flank=m.ball.position.z*simulation.Direction(side);return action+" · "+(flank>9?"côté gauche":flank< -9?"côté droit":"axe central");
            }
        }
        public static string EventHeadline(string kind)=>kind switch{"goal"=>"BUT !","shot"=>"FRAPPE","save"=>"ARRÊT DU GARDIEN","woodwork"=>"SUR LE MONTANT","penalty"=>"PENALTY","offside"=>"HORS-JEU","red"=>"EXCLUSION","assistant"=>"L’ADJOINT","shout"=>"DEPUIS LA TOUCHE",_=>null};
        public int LastFrameSteps {get;private set;}
        public bool BudgetLimited {get;private set;}
        public float EffectiveSpeed {get;private set;}
        public MatchBroadcast(MatchSimulation simulation)
        {
            this.simulation=simulation??throw new ArgumentNullException(nameof(simulation));
            QuietSpeed=simulation.State.HalfDuration==2700?60:12;
            observedEvents=simulation.State.events.Count;visibleUntil=simulation.State.clock+4;
        }
        public void ShowLive(float seconds=5,string reason="Retour au direct")
        {visibleUntil=Math.Max(visibleUntil,simulation.State.clock+seconds);Reason=simulation.State.clock<protectedUntil?protectedReason:reason;Quiet=false;}
        void ShowEvent(string kind,string reason)
        {
            // Scale visible windows by the selected live pace, preserving every
            // simulation step. Even at x10 a goal remains readable for five seconds.
            float window=kind=="goal"?Math.Max(7,5*presentationSpeed):kind=="shot"?Math.Max(5,2.1f*presentationSpeed):Math.Max(5,1.5f*presentationSpeed);
            if(kind=="goal"||simulation.State.clock>=protectedUntil){protectedReason=reason;protectedUntil=simulation.State.clock+window;}
            ShowLive(window,reason);
        }
        public void Refresh()
        {
            var m=simulation.State;
            visibleUntil=exitWindow.Extend(simulation.ExitContact,m,Quiet,visibleUntil);
            if(observedEvents>m.events.Count)observedEvents=m.events.Count;
            while(observedEvents<m.events.Count){
                var e=m.events[observedEvents++];
                string reason=e.kind switch{"goal"=>"But !","shot"=>"Occasion de but","save"=>"Intervention du gardien","woodwork"=>"Sur le montant","penalty"=>"Penalty","red"=>"Exclusion","yellow"=>"Avertissement","injury"=>"Intervention médicale","substitution"=>"Changement de joueur",_=>null};
                bool major=e.kind=="goal"||e.kind=="shot"||e.kind=="save"||e.kind=="woodwork"||e.kind=="penalty"||e.kind=="red"||e.kind=="injury";
                if(reason!=null&&(Mode!=MatchViewingMode.KeyMoments||major))ShowEvent(e.kind,reason);
                if(Mode==MatchViewingMode.Extended&&(e.kind=="offside"||e.kind=="foul"))ShowEvent(e.kind,e.kind=="offside"?"Hors-jeu":"Faute sifflée");
                if(Enabled&&e.side==0&&(e.kind=="injury"||e.kind=="red"))PauseRequested=true;
            }
            if(m.halfTime||m.finished){Quiet=false;Reason=m.finished?"Fin de la rencontre":"Mi-temps";return;}
            if(!Enabled){Quiet=false;return;}
            string threat=Threat(m);
            if(threat!=null){ShowLive(Mode==MatchViewingMode.Extended?8:Mode==MatchViewingMode.KeyMoments?3:4,threat);}
            Quiet=Enabled&&m.clock>=visibleUntil;
        }
        string Threat(MatchState m)
        {
            var b=m.ball;
            if(m.restart>0){
                // Full-duration stoppages include retrieval and preparation.
                // Show the award through the event window, then return for
                // the actual delivery instead of filming 30 seconds of waiting.
                if(m.HalfDuration==2700&&(m.phase=="goal"||m.restart>(Mode==MatchViewingMode.Extended?12:6))&&m.phase!="kickoff")return null;
                if(m.phase=="goal")return "Célébration";
                if(m.phase=="penalty")return "Penalty";
                if(m.phase=="corner")return "Corner";
                if(m.phase=="free-kick"&&b.position.x*simulation.Direction(m.restartSide)>16)return "Coup franc dangereux";
                if(m.phase=="kickoff")return "Engagement";
                return null;
            }
            if(b.kind=="shot"||b.goalAttempt)return "Occasion de but";
            if(b.held)return null;
            int side=MatchSimulation.PossessionSide(m);if(side<0)side=b.side;
            int direction=simulation.Direction(side);
            float progress=b.position.x*direction;
            Actor owner=null,source=null,receiver=null;float deepest=-100,secondDeepest=-100;
            foreach(var actor in m.actors){
                if(actor.id==b.owner)owner=actor;if(actor.id==b.from)source=actor;if(actor.id==b.to)receiver=actor;
                if(actor.side==side||actor.sentOff)continue;float depth=actor.position.x*direction;
                if(depth>deepest){secondDeepest=deepest;deepest=depth;}else if(depth>secondDeepest)secondDeepest=depth;
            }
            bool committed=(b.kind=="cross"||b.kind=="cutback"||b.kind=="through")&&b.end.x*direction>27&&Math.Abs(b.end.z)<24;
            bool penetrating=b.kind=="pass"&&b.end.x*direction>36&&Math.Abs(b.end.z)<18&&
                (b.end.x-b.start.x)*direction>6&&receiver!=null&&simulation.ShotQuality(receiver)>.08f;
            if(Mode==MatchViewingMode.KeyMoments){committed=committed&&b.end.x*direction>38&&Math.Abs(b.end.z)<14;penetrating=penetrating&&receiver!=null&&simulation.ShotQuality(receiver)>.18f;}
            bool buildup=Mode==MatchViewingMode.Extended&&(b.kind=="pass"||b.kind=="through"||b.kind=="cross")&&b.end.x*direction>18&&((b.end.x-b.start.x)*direction>8||Math.Abs(b.end.z-b.start.z)>25);
            if(committed||penetrating||buildup){
                int sequence=source?.actionSequence??0;
                bool fresh=flightSource!=b.from||flightKind!=b.kind||flightSequence!=sequence||b.elapsed<previousFlightElapsed;
                flightSource=b.from;flightKind=b.kind;flightSequence=sequence;previousFlightElapsed=b.elapsed;
                if(fresh)return committed||penetrating?"Ballon vers la surface":"Construction offensive";
            }
            if(owner!=null&&!owner.sentOff&&owner.slot>0){
                direction=simulation.Direction(owner.side);progress=owner.position.x*direction;
                bool finish=progress>32&&Math.Abs(owner.position.z)<18&&simulation.ShotQuality(owner)>.10f;
                var towardGoal=(new Point(direction*52.5f,0)-owner.position).Normalized;
                bool run=progress>32&&Math.Abs(owner.position.z)<22&&owner.velocity.x*direction>3&&
                    Point.Dot(owner.velocity.Normalized,towardGoal)>.75f&&
                    (simulation.ShotQuality(owner)>.08f||progress>secondDeepest-5);
                if(Mode==MatchViewingMode.KeyMoments){finish=finish&&progress>38&&simulation.ShotQuality(owner)>.18f;run=run&&finish;}
                bool transition=Mode==MatchViewingMode.Extended&&progress>10&&owner.velocity.x*direction>3&&Point.Dot(owner.velocity.Normalized,towardGoal)>.6f;
                // Presence in the final third is not itself a highlight. Open
                // the window for a real goal threat or advancing run, then
                // renew only after meaningful progress by that same carrier.
                bool fresh=threatOwner!=owner.id||threatPeriod!=m.period||
                    (owner.position.x-threatPosition.x)*direction>3||finish&&Point.Distance(owner.position,threatPosition)>6;
                if((finish||run||transition)&&fresh){threatOwner=owner.id;threatPeriod=m.period;threatPosition=owner.position;return finish?"Possibilité de frappe":run?"Percée vers la surface":"Transition offensive";}
            }
            return null;
        }
        public void Advance(float realSeconds,int liveSpeed)
        {
            if(float.IsNaN(realSeconds)||float.IsInfinity(realSeconds)||realSeconds<=0)return;
            int requestedSpeed=Math.Max(1,Math.Min(10,liveSpeed));
            if(requestedSpeed!=presentationSpeed&&simulation.State.clock<protectedUntil){float previousUntil=protectedUntil;protectedUntil=simulation.State.clock+(protectedUntil-simulation.State.clock)/presentationSpeed*requestedSpeed;visibleUntil+=protectedUntil-previousUntil;}
            presentationSpeed=requestedSpeed;
            PauseRequested=false;LastFrameSteps=0;BudgetLimited=false;EffectiveSpeed=0;Refresh();
            if(PauseRequested)return;
            float startClock=simulation.State.clock;
            double remaining=Math.Min(.1f,realSeconds);
            // Reconsider at every 0.1 s boundary: an accelerated frame cannot
            // silently consume a whole shot, goal or medical interruption.
            while(remaining>1e-8&&!simulation.State.halfTime&&!simulation.State.finished&&!PauseRequested&&LastFrameSteps<64){
                int speed=Quiet?Math.Max(4,Math.Min(simulation.State.HalfDuration==2700?120:24,QuietSpeed)):Math.Max(1,Math.Min(10,liveSpeed));
                double untilTick=Math.Max(1e-7,MatchSimulation.Step-simulation.State.remainder);
                double realStep=Math.Min(remaining,untilTick/speed);
                simulation.Advance(realStep*speed);remaining-=realStep;LastFrameSteps++;Refresh();
            }
            BudgetLimited=LastFrameSteps>=64&&remaining>1e-8&&!simulation.State.halfTime&&!simulation.State.finished;
            EffectiveSpeed=(simulation.State.clock-startClock)/Math.Min(.1f,realSeconds);
        }
    }
}
