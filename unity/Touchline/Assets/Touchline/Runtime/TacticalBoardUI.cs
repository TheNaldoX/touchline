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
        string tacticalTab="Composition",benchSearch="";bool includeStarters,phasePitchVisible;
        string[] lineupUndo;VisualElement tacticalPitch,benchList,dragGhost;ScrollView tacticsOuter;Vector2 dragPosition;IVisualElementScheduledItem dragScroll;readonly VisualElement[] tacticalTokens=new VisualElement[11];
        void PendingChanges(VisualElement parent)
        {
            var pending=Career.match?.pendingSubstitutions?.Where(p=>p.side==0).ToArray();if(pending==null||pending.Length==0)return;
            var panel=Card(parent,"pending-changes");panel.name="pending-changes";Text(panel,"Au prochain arrêt de jeu","section-title");
            Text(panel,"Ces changements restent annulables. Ils utiliseront une seule fenêtre s’ils entrent ensemble.","muted");
            foreach(var request in pending){var row=Row(panel);Text(row,Database.Find(request.outgoing).name+" → "+Database.Find(request.incoming).name);var cancel=Button(row,"Annuler",()=>{new MatchSimulation(Database,Career.match).CancelSubstitution(request.outgoing);Save();Build();});cancel.name="cancel-sub-"+request.outgoing;}
        }
        void Tactics()
        {
            if(arena!=null)arena.Paused=true;selectedSlot=Mathf.Clamp(selectedSlot,0,10);
            var title=Row(content,"tactics-title");Heading(title,"Tactique");var shapes=new List<string>{"4-3-3","4-4-2","4-2-3-1","3-4-2-1"};var shape=new DropdownField("Système",shapes,Math.Max(0,shapes.IndexOf(Career.tactic.formation)));title.Add(shape);shape.RegisterValueChangedCallback(e=>{try{Career.ChangeTacticalFormation(Database,e.newValue);lineupUndo=null;Save();Build();}catch(Exception error){shape.SetValueWithoutNotify(Career.tactic.formation);Message(error.Message);}});
            if(Career.match!=null){var back=Button(title,"Retour au match",()=>Navigate("Match"));back.name="tactics-return-match";back.AddToClassList("primary");}
            bool shortWide=root.ClassListContains("short-wide");var tabs=Row(shortWide?title:content,"tactics-tabs");foreach(var name in new[]{"Composition","Avec ballon","Sans ballon","Transitions","Adjoint"}){var b=Button(tabs,name,()=>{tacticalTab=name;placement=false;Build();});if(name==tacticalTab)b.AddToClassList("active");}
            if(tacticalTab=="Adjoint"){var advice=Scroll(content);Heading(advice,"Lecture de votre adjoint");TacticalAdvicePanel(advice);var fold=advice.Q<Foldout>();if(fold!=null)fold.value=true;return;}
            if(tacticalTab!="Composition"){TacticalInstructions();return;}
            var toolbar=Row(shortWide?title:content,"tactics-tools");Text(toolbar,Career.match==null?"Glissez sur un partenaire pour permuter. Touchez un poste, puis Aligner.":"Glissez pour permuter. Préparez les entrées du banc au prochain arrêt de jeu.","muted");
            if(Career.match==null){Button(toolbar,"Annuler",()=>{if(lineupUndo==null)return;Career.lineup=lineupUndo;lineupUndo=null;Save();Build();}).SetEnabled(lineupUndo!=null);Button(toolbar,"Onze conseillé",()=>{lineupUndo=(string[])Career.lineup.Clone();Career.lineup=Core.Career.Select(Database,Career.club,Career.tactic);Save();Build();});}
            else Text(toolbar,Career.match.substitutions[0]+" / 5 changements · "+Career.match.homeWindows.Count+" / 3 fenêtres utilisées","muted");
            var scroll=Scroll(content);tacticsOuter=scroll;var columns=Row(scroll,"tactics-workspace");var pitchColumn=new VisualElement();pitchColumn.AddToClassList("tactics-pitch-column");columns.Add(pitchColumn);CreateTacticalPitch(pitchColumn,false,false);
            var side=new VisualElement();side.AddToClassList("tactics-bench");columns.Add(side);var selected=Database.Find(Lineup[selectedSlot]);var banner=Card(side,"selected-post");Text(banner,FrenchFootballPositions.Label(Career.tactic.withoutBall[selectedSlot].role)+" · "+selected.name,"section-title");Text(banner,"Poste sélectionné · "+PlayerFitness(selected).ToString("0")+" % de condition","muted");var role=Row(banner);var duties=new List<string>{"Défense","Soutien","Attaque"};string current=Career.tactic.withBall[selectedSlot].duty;var duty=new DropdownField("Mission",duties,current=="defend"?0:current=="attack"?2:1);role.Add(duty);duty.SetEnabled(selectedSlot!=0);duty.RegisterValueChangedCallback(e=>{Career.tactic.withBall[selectedSlot].duty=duty.index==0?"defend":duty.index==2?"attack":"support";Save();});Button(role,"Fiche",()=>PlayerProfile(selected.id));
            PendingChanges(side);var filter=new TextField("Rechercher"){value=benchSearch,name="tactics-bench-search",tooltip="Rechercher un joueur, avec ou sans accents"};side.Add(filter);filter.RegisterValueChangedCallback(e=>{benchSearch=e.newValue;PopulateTacticalBench();});var all=new Toggle("Inclure les titulaires pour permuter"){value=includeStarters,name="tactics-include-starters"};side.Add(all);all.RegisterValueChangedCallback(e=>{includeStarters=e.newValue;PopulateTacticalBench();});
            if(root.ClassListContains("narrow"))Button(side,"Revenir au terrain",()=>tacticsOuter.ScrollTo(tacticalPitch));benchList=Scroll(side);benchList.AddToClassList("bench-scroll");PopulateTacticalBench();
        }
        void CreateTacticalPitch(VisualElement parent,bool withBall,bool allowPlacement)
        {
            tacticalPitch=new VisualElement{name="tactical-pitch"};tacticalPitch.AddToClassList("pitch");tacticalPitch.AddToClassList("tactics-board");if(root.ClassListContains("short-wide"))tacticalPitch.style.height=Mathf.Clamp(root.resolvedStyle.height-238,240,360);parent.Add(tacticalPitch);PitchLines(tacticalPitch);var slots=withBall?Career.tactic.withBall:Career.tactic.withoutBall;
            for(int i=0;i<11;i++){
                int index=i;var p=Database.Find(Lineup[i]);var token=new VisualElement{name="tactical-slot-"+i,focusable=true};token.AddToClassList("token");token.AddToClassList("tactic-player");token.EnableInClassList("selected-player",i==selectedSlot);tacticalPitch.Add(token);tacticalTokens[i]=token;Position(token,slots[i]);
                Text(token,p.number+"  "+FrenchFootballPositions.Short(slots[i].role),"player-position");Text(token,MatchPlayerLabels.Surname(p.name,p.nationality),"player-name");Text(token,PlayerFitness(p).ToString("0")+" %","player-condition");foreach(var label in token.Query<Label>().ToList())label.pickingMode=PickingMode.Ignore;
                bool excluded=Career.match?.actors[i].sentOff==true;token.EnableInClassList("unavailable-player",excluded||!Career.Available(p.id));
                AttachTacticalDrag(token,p.id,index,allowPlacement);token.RegisterCallback<NavigationSubmitEvent>(e=>{selectedSlot=index;Build();e.StopPropagation();});
            }
        }
        void PopulateTacticalBench()
        {
            if(benchList==null)return;benchList.Clear();string role=Career.tactic.withoutBall[selectedSlot].role;
            var candidates=Database.Squad(Career.club).Where(p=>p.id!=Lineup[selectedSlot]&&(includeStarters||!Lineup.Contains(p.id))&&DirectoryMatches(p.name,benchSearch.Trim())).OrderByDescending(p=>Career.LineupRestriction(Database,selectedSlot,p.id)==null).ThenByDescending(p=>p.Fit(role)).ThenByDescending(p=>p.rating*PlayerFitness(p)/100).ToList();
            foreach(var p in candidates){var row=new VisualElement{name="bench-player-"+p.id};row.AddToClassList("bench-player");benchList.Add(row);var handle=new VisualElement{name="bench-drag-"+p.id};handle.AddToClassList("bench-handle");var icon=new Label("↕"){pickingMode=PickingMode.Ignore};handle.Add(icon);row.Add(handle);AttachTacticalDrag(handle,p.id,-1,false);
                var copy=new VisualElement();copy.AddToClassList("bench-copy");row.Add(copy);Text(copy,p.name,"bench-name");Text(copy,FrenchFootballPositions.CompactList(p.positions??new[]{p.position})+" · "+PlayerFitness(p).ToString("0")+" % · "+(p.Fit(role)>=1?"Poste naturel":p.Fit(role)>=.8f?"Poste proche":"Hors poste"),"bench-detail");string restriction=Career.LineupRestriction(Database,selectedSlot,p.id);if(restriction!=null)Text(copy,restriction,"status-warning");var button=Button(row,Lineup.Contains(p.id)?"Permuter":Career.match!=null&&Career.match.restart<=0&&!Career.match.halfTime?"Préparer":"Aligner",()=>AssignFromTactics(selectedSlot,p.id));button.name="assign-"+p.id;button.SetEnabled(restriction==null);handle.SetEnabled(restriction==null);
            }
            if(candidates.Count==0)Text(benchList,"Aucun joueur dans ce filtre.","muted");
        }
        void AssignFromTactics(int slot,string id)
        {
            if(Lineup[slot]==id)return;string error=Career.LineupRestriction(Database,slot,id);if(error!=null){Message(error);return;}
            void Apply(){try{if(Career.match==null)lineupUndo=(string[])Career.lineup.Clone();Career.AssignTacticalPlayer(Database,slot,id);selectedSlot=slot;Save();Build();}catch(Exception e){Message(e.Message);}}
            if(Career.match!=null&&!Lineup.Contains(id)&&(Career.match.halfTime||Career.match.restart>0))Confirm("Effectuer le remplacement ?",Database.Find(Lineup[slot]).name+" → "+Database.Find(id).name+". Le jeu est arrêté : ce changement sera immédiat et le joueur sorti ne pourra pas revenir.",Apply);else Apply();
        }
        int TacticalDropTarget(Vector2 point,int source)
        {
            int target=-1;float closest=float.MaxValue;for(int i=0;i<11;i++){if(i==source)continue;var bounds=tacticalTokens[i].worldBound;bounds.xMin-=12;bounds.xMax+=12;bounds.yMin-=12;bounds.yMax+=12;if(!bounds.Contains(point))continue;float d=Vector2.Distance(point,bounds.center);if(d<closest){target=i;closest=d;}}return target;
        }
        void ClearTacticalDrag(){dragScroll?.Pause();dragScroll=null;dragGhost?.RemoveFromHierarchy();dragGhost=null;foreach(var token in tacticalTokens)token?.RemoveFromClassList("drop-player");}
        void AttachTacticalDrag(VisualElement handle,string id,int sourceSlot,bool movePosition)
        {
            Vector2 start=default;bool dragging=false;int pointer=-1;
            handle.RegisterCallback<PointerDownEvent>(e=>{if(e.button!=0||pointer>=0)return;pointer=e.pointerId;start=e.position;dragging=false;handle.CapturePointer(pointer);e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            handle.RegisterCallback<PointerMoveEvent>(e=>{if(e.pointerId!=pointer||!handle.HasPointerCapture(pointer)||tacticalTab!="Composition"&&!movePosition)return;if(!dragging&&Vector2.Distance(start,e.position)<8)return;dragging=true;
                if(dragGhost==null){dragGhost=new Label(Database.Find(id).name){pickingMode=PickingMode.Ignore};dragGhost.AddToClassList("tactic-drag-ghost");root.Add(dragGhost);dragScroll=root.schedule.Execute(()=>{if(dragGhost==null||tacticsOuter==null)return;var bounds=tacticsOuter.worldBound;float delta=dragPosition.y<bounds.yMin+45?-9:dragPosition.y>bounds.yMax-45?9:0;tacticsOuter.scrollOffset+=Vector2.up*delta;}).Every(16);}dragPosition=e.position;var local=root.WorldToLocal(e.position);dragGhost.style.left=local.x-55;dragGhost.style.top=local.y-24;
                int target=movePosition?-1:TacticalDropTarget(e.position,sourceSlot);for(int i=0;i<11;i++)tacticalTokens[i].EnableInClassList("drop-player",i==target);e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            handle.RegisterCallback<PointerUpEvent>(e=>{if(e.pointerId!=pointer)return;int captured=pointer;pointer=-1;if(handle.HasPointerCapture(captured))handle.ReleasePointer(captured);e.StopImmediatePropagation();bool moved=dragging;dragging=false;int target=moved?TacticalDropTarget(e.position,sourceSlot):-1;ClearTacticalDrag();
                if(!moved){if(sourceSlot>=0){selectedSlot=sourceSlot;Build();if(tacticalTab=="Composition"&&root.ClassListContains("narrow"))root.schedule.Execute(()=>tacticsOuter.ScrollTo(benchList.parent)).StartingIn(40);}return;}
                if(movePosition&&sourceSlot>=0&&tacticalPitch.worldBound.Contains(e.position)){var pos=tacticalPitch.WorldToLocal(e.position);var slots=tacticalTab=="Avec ballon"?Career.tactic.withBall:Career.tactic.withoutBall;slots[sourceSlot].x=Mathf.Clamp(pos.x/tacticalPitch.resolvedStyle.width*100,8,92);slots[sourceSlot].y=Mathf.Clamp(100-pos.y/tacticalPitch.resolvedStyle.height*100,6,92);Save();Build();}
                else if(tacticalTab=="Composition"&&target>=0)AssignFromTactics(target,id);
            },TrickleDown.TrickleDown);
            handle.RegisterCallback<PointerCancelEvent>(e=>{if(e.pointerId!=pointer)return;pointer=-1;dragging=false;if(handle.HasPointerCapture(e.pointerId))handle.ReleasePointer(e.pointerId);ClearTacticalDrag();});
            handle.RegisterCallback<PointerCaptureOutEvent>(e=>{if(pointer<0)return;pointer=-1;dragging=false;ClearTacticalDrag();});
            handle.RegisterCallback<DetachFromPanelEvent>(_=>ClearTacticalDrag());
        }
        void TacticalInstructions()
        {
            var scroll=Scroll(content);tacticsOuter=scroll;if(tacticalTab=="Transitions"){
                InstructionToggle(scroll,"Après la perte","Contre-pressing",Career.tactic.counterPress,v=>Career.tactic.counterPress=v,"Presser immédiatement pour récupérer ; demande plus d’efforts et peut ouvrir des espaces.");
                InstructionToggle(scroll,"À la récupération","Contre-attaque",Career.tactic.counterAttack,v=>Career.tactic.counterAttack=v,"Accélérer vers l’avant lorsque l’adversaire est désorganisé.");return;
            }
            bool with=tacticalTab=="Avec ballon";var tools=Row(scroll,"tactics-tools");Text(tools,with?"Organisation en possession":"Organisation défensive","section-title");var move=new Toggle("Déplacer les postes"){value=placement};tools.Add(move);move.RegisterValueChangedCallback(e=>{placement=e.newValue;Build();});Text(scroll,placement?"Glissez un poste vers sa nouvelle zone. Seule cette phase est modifiée.":"Activez Déplacer les postes pour ajuster la structure. Les permutations se font dans Composition.","muted");
            bool narrow=root.ClassListContains("narrow");if(narrow){var preview=Button(tools,phasePitchVisible?"Masquer le terrain":"Voir le terrain",()=>{phasePitchVisible=!phasePitchVisible;Build();});preview.name="phase-pitch-preview";preview.SetEnabled(!placement);}
            var columns=Row(scroll,"tactics-workspace");if(!narrow||placement||phasePitchVisible){var pitchColumn=new VisualElement();pitchColumn.AddToClassList("tactics-pitch-column");columns.Add(pitchColumn);CreateTacticalPitch(pitchColumn,with,placement);}var settings=new VisualElement();settings.AddToClassList("tactics-bench");columns.Add(settings);
            if(with)DeliveryInstructions(settings);
            if(with){InstructionChoice(settings,"Largeur offensive",new[]{"Étroite","Resserrée","Normale","Large","Très large"},Career.tactic.width,v=>Career.tactic.width=v,"Écarte les joueurs pour ouvrir les couloirs, au prix de soutiens plus éloignés.");InstructionChoice(settings,"Rythme",new[]{"Très patient","Patient","Normal","Rapide","Très rapide"},Career.tactic.tempo,v=>Career.tactic.tempo=v,"Accélère la circulation et les décisions ; exige plus de qualité technique.");InstructionChoice(settings,"Longueur des passes",new[]{"Très courtes","Courtes","Mixtes","Directes","Très directes"},Career.tactic.directness,v=>Career.tactic.directness=v,"Privilégie les relais proches ou des transmissions plus longues.");InstructionChoice(settings,"Prise de risque",new[]{"Très prudente","Prudente","Équilibrée","Offensive","Très offensive"},Career.tactic.mentality,v=>Career.tactic.mentality=v,"Fait avancer davantage de joueurs, avec moins de couverture derrière eux.");InstructionToggle(settings,"Dernier tiers","Construire dans la surface",Career.tactic.workIntoBox,v=>Career.tactic.workIntoBox=v,"Cherche une meilleure position avant de tirer ; réduit les tentatives lointaines.");}
            else{InstructionChoice(settings,"Ligne défensive",new[]{"Très basse","Basse","Normale","Haute","Très haute"},Career.tactic.line,v=>Career.tactic.line=v,"Rapproche la défense du milieu, mais laisse davantage de profondeur dans son dos.");InstructionChoice(settings,"Déclenchement du pressing",new[]{"Rare","Mesuré","Normal","Fréquent","Très intense"},Career.tactic.pressing,v=>Career.tactic.pressing=v,"Augmente la pression sur le porteur et la fatigue de votre équipe.");InstructionChoice(settings,"Largeur du bloc",new[]{"Très resserré","Resserré","Normal","Large","Très large"},Career.tactic.defensiveWidth,v=>Career.tactic.defensiveWidth=v,"Protège l’axe ou les côtés ; écarter le bloc ouvre davantage d’intervalles au centre.");}
        }
        void DeliveryInstructions(VisualElement parent)
        {
            var card=Card(parent,"instruction-card");Text(card,"Orientation des attaques","section-title");
            var focusCodes=new[]{"balanced","left","centre","right"};
            var focus=new DropdownField("Construire",new List<string>{"Tous les couloirs","Côté gauche","Dans l’axe","Côté droit"},Math.Max(0,Array.IndexOf(focusCodes,Career.tactic.attackFocus))){name="tactic-attack-focus"};card.Add(focus);
            focus.RegisterValueChangedCallback(_=>{Career.tactic.attackFocus=focusCodes[focus.index];Career.BindMatchTactic();Save();});
            Text(card,"Favorise les partenaires disponibles dans ce couloir. Le porteur conserve la possibilité de jouer ailleurs si la passe est risquée.","muted");
            var crossCodes=new[]{"mixed","low","floated"};
            var delivery=new DropdownField("Centres",new List<string>{"Variés selon le couloir libre","Rasants","Aériens et flottants"},Math.Max(0,Array.IndexOf(crossCodes,Career.tactic.crossing))){name="tactic-crossing-style"};card.Add(delivery);
            delivery.RegisterValueChangedCallback(_=>{Career.tactic.crossing=crossCodes[delivery.index];Career.BindMatchTactic();Save();});
            Text(card,"Un centre rasant arrive rapidement dans les pieds. Un ballon aérien laisse davantage de temps aux appels et aux duels de tête.","muted");
        }
        void InstructionChoice(VisualElement parent,string title,string[] labels,float value,Action<float> apply,string explanation)
        {
            var card=Card(parent,"instruction-card");Text(card,title,"section-title");var options=Row(card,"instruction-options");int selected=Mathf.RoundToInt(value*4);for(int i=0;i<labels.Length;i++){int index=i;var b=Button(options,labels[i],()=>{apply(index/4f);Save();var buttons=options.Query<Button>().ToList();for(int j=0;j<buttons.Count;j++)buttons[j].EnableInClassList("active",j==index);});if(i==selected)b.AddToClassList("active");}Text(card,explanation,"muted");
        }
        void InstructionToggle(VisualElement parent,string title,string label,bool value,Action<bool> apply,string explanation){var card=Card(parent,"instruction-card");Text(card,title,"section-title");var toggle=new Toggle(label){value=value};card.Add(toggle);toggle.RegisterValueChangedCallback(e=>{apply(e.newValue);Save();});Text(card,explanation,"muted");}
    }
}
