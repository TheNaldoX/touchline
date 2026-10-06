using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline
{
    public sealed partial class TouchlineApp
    {
        string squadGroup="Tous",squadPosition="Tous postes",squadSort="Sélection";
        bool SquadAvailable(PlayerData p){var actor=Career.match?.actors.FirstOrDefault(a=>a.id==p.id);return Career.Available(p.id)&&actor?.sentOff!=true&&actor?.injured!=true;}
        string SquadStatus(PlayerData p)
        {
            var m=Career.match;var actor=m?.actors.FirstOrDefault(a=>a.id==p.id);
            if(actor?.sentOff==true)return "Exclu";
            if(actor?.injured==true||Career.Injury(p.id)!=null)return "Blessé";
            if(!Career.Available(p.id))return "Indisponible";
            if(m?.pendingSubstitutions?.Any(s=>s.incoming==p.id)==true)return "Entrée préparée";
            if(m?.pendingSubstitutions?.Any(s=>s.outgoing==p.id)==true)return "Sortie préparée";
            if(actor!=null)return "Sur le terrain";
            if(m?.used.Contains(p.id)==true)return "Sorti";
            return Lineup.Contains(p.id)?"Titulaire":"Banc";
        }
        void Squad()
        {
            var title=Row(content,"squad-title");Heading(title,"Votre effectif");var actions=Row(title,"squad-title-actions");Button(actions,"Comparer",()=>ComparePlayer(Lineup[0])).name="squad-compare";Button(actions,"Préparer le onze",()=>Navigate("Tactique")).AddToClassList("primary");
            var squad=Database.Squad(Career.club);var summary=Text(content,"","muted");summary.name="squad-summary";
            var searchField=new TextField("Rechercher"){value=search,name="squad-search"};searchField.AddToClassList("squad-search");content.Add(searchField);
            var filters=Row(content,"squad-filters");
            var position=new DropdownField(new List<string>{"Tous postes","GB","DEF","MIL","ATT"},squadPosition){name="squad-position",tooltip="Filtrer les postes",formatListItemCallback=FrenchFootballPositions.Label,formatSelectedValueCallback=FrenchFootballPositions.Label};filters.Add(position);
            var group=new DropdownField(new List<string>{"Tous","Disponibles","À ménager","Indisponibles"},squadGroup){name="squad-group",tooltip="Filtrer la disponibilité"};filters.Add(group);
            var sort=new DropdownField(new List<string>{"Sélection","Condition","Nom"},squadSort){name="squad-sort",tooltip="Ordre de l’effectif"};filters.Add(sort);
            var empty=Text(content,"Aucun joueur ne correspond à ces filtres.","empty-state");empty.name="squad-empty";
            var list=new ListView{name="squad-list",fixedItemHeight=82,selectionType=SelectionType.None,virtualizationMethod=CollectionVirtualizationMethod.FixedHeight};list.AddToClassList("squad-list");content.Add(list);
            portraits??=gameObject.AddComponent<PortraitStore>();var visible=new List<PlayerData>();
            list.makeItem=()=>new SquadPlayerRow(this);
            list.bindItem=(element,index)=>BindSquadRow((SquadPlayerRow)element,visible[index],index);
            list.unbindItem=(element,index)=>((SquadPlayerRow)element).Release();
            void Populate(){
                var rows=squad.Where(p=>French.CompareInfo.IndexOf(p.name,search,CompareOptions.IgnoreCase|CompareOptions.IgnoreNonSpace)>=0&&(squadPosition=="Tous postes"||Core.FootballPositions.Matches(p,squadPosition)));
                if(squadGroup=="Disponibles")rows=rows.Where(SquadAvailable);else if(squadGroup=="À ménager")rows=rows.Where(p=>SquadAvailable(p)&&PlayerFitness(p)<85);else if(squadGroup=="Indisponibles")rows=rows.Where(p=>!SquadAvailable(p));
                var eleven=Lineup;visible=squadSort=="Nom"?rows.OrderBy(p=>p.name).ToList():squadSort=="Condition"?rows.OrderBy(p=>PlayerFitness(p)).ThenBy(p=>p.name).ToList():rows.OrderBy(p=>Array.IndexOf(eleven,p.id)<0?99:Array.IndexOf(eleven,p.id)).ThenBy(p=>p.position).ThenBy(p=>p.number).ToList();
                summary.text=visible.Count+" / "+squad.Count+" joueurs · "+squad.Count(SquadAvailable)+" disponibles · "+squad.Count(p=>SquadAvailable(p)&&PlayerFitness(p)<85)+" à ménager";
                empty.style.display=visible.Count==0?DisplayStyle.Flex:DisplayStyle.None;list.style.display=visible.Count==0?DisplayStyle.None:DisplayStyle.Flex;list.itemsSource=visible;list.Rebuild();
            }
            var reset=Button(filters,"Effacer",()=>{search="";squadPosition="Tous postes";squadGroup="Tous";squadSort="Sélection";searchField.SetValueWithoutNotify(search);position.SetValueWithoutNotify(squadPosition);group.SetValueWithoutNotify(squadGroup);sort.SetValueWithoutNotify(squadSort);Populate();});reset.name="squad-reset";reset.AddToClassList("squad-reset");
            searchField.RegisterValueChangedCallback(e=>{search=e.newValue;Populate();});position.RegisterValueChangedCallback(e=>{squadPosition=e.newValue;Populate();});group.RegisterValueChangedCallback(e=>{squadGroup=e.newValue;Populate();});sort.RegisterValueChangedCallback(e=>{squadSort=e.newValue;Populate();});Populate();
        }
        sealed class SquadPlayerRow:VisualElement
        {
            public string player;public int version;public readonly Image photo;public readonly Label initials,identity,detail,status,condition;public readonly ProgressBar fitness;public readonly Button open;
            public SquadPlayerRow(TouchlineApp app)
            {
                AddToClassList("squad-player-row");var portrait=new VisualElement();portrait.AddToClassList("squad-row-portrait");Add(portrait);initials=Text(portrait,"","squad-row-initials");photo=new Image{scaleMode=ScaleMode.ScaleToFit};photo.AddToClassList("squad-row-photo");portrait.Add(photo);
                var copy=new VisualElement();copy.AddToClassList("squad-row-copy");Add(copy);identity=Text(copy,"","squad-row-name");detail=Text(copy,"","squad-row-detail");status=Text(copy,"","squad-row-status");
                Vector2 down=Vector2.zero;bool pressed=false;copy.RegisterCallback<PointerDownEvent>(e=>{down=e.position;pressed=true;});copy.RegisterCallback<PointerCancelEvent>(_=>pressed=false);copy.RegisterCallback<PointerUpEvent>(e=>{if(pressed&&Vector2.Distance(down,e.position)<8&&player!=null)app.PlayerProfile(player);pressed=false;});
                var state=new VisualElement();state.AddToClassList("squad-row-fitness");Add(state);condition=Text(state,"","squad-row-condition");fitness=new ProgressBar{lowValue=0,highValue=100};state.Add(fitness);
                open=Button(this,"Fiche",()=>{if(player!=null)app.PlayerProfile(player);});open.AddToClassList("squad-open");RegisterCallback<DetachFromPanelEvent>(_=>Release());
            }
            public void Release(){version++;player=null;photo.image=null;photo.style.display=DisplayStyle.None;initials.style.display=DisplayStyle.Flex;}
        }
        void BindSquadRow(SquadPlayerRow row,PlayerData p,int index)
        {
            row.Release();row.player=p.id;row.name="squad-row-"+p.id;row.open.name="squad-open-"+p.id;row.EnableInClassList("squad-even",index%2==0);row.initials.text=string.Concat(p.name.Split(' ').Where(s=>s.Length>0).Take(2).Select(s=>s.Substring(0,1)));
            row.identity.text=p.name;row.identity.tooltip=p.name;row.detail.text=p.number+" · "+FrenchFootballPositions.CompactList(p.positions??new[]{p.position})+" · "+p.age+" ans";row.status.text=SquadStatus(p);float condition=PlayerFitness(p);row.condition.text=condition.ToString("0")+" %";row.fitness.value=condition;row.EnableInClassList("squad-warning",condition<85||!SquadAvailable(p));
            int version=row.version;portraits.Load(p.id,texture=>{if(row.player!=p.id||row.version!=version||texture==null)return;row.photo.image=texture;row.photo.style.display=DisplayStyle.Flex;row.initials.style.display=DisplayStyle.None;});
        }
    }
}
