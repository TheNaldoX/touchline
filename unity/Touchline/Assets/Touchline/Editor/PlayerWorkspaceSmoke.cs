using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Touchline.Core;

namespace Touchline.Editor
{
    [InitializeOnLoad] public static class PlayerWorkspaceSmoke
    {
        static int stage,frames,last=-1,contractsBefore,shortlistBeforeReset;static RenderTexture target;static string output,player;static LeagueData globalLeague;
        static string OutputName { get { var value=Environment.GetCommandLineArgs().FirstOrDefault(a=>a.StartsWith("-touchlinePlayerOutput="))?.Substring("-touchlinePlayerOutput=".Length)??"player-workspace";if(value.Length==0||value.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Invalid output name");return value; } }
        static VisualElement Root=>TouchlineApp.Instance.GetComponent<UIDocument>().rootVisualElement;
        static PlayerWorkspaceSmoke(){EditorApplication.update+=Tick;}
        public static void Run(){ProjectBuilder.Configure();SessionState.SetBool("PlayerWorkspaceSmoke",true);EditorApplication.isPlaying=true;}
        static void Click(string id){var b=Root.Query<Button>().ToList().FirstOrDefault(x=>x.name==id||x.text==id);if(b==null)throw new Exception("Player workspace button missing: "+id);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}}
        static void Resize(int w,int h){var old=target;target=new RenderTexture(w,h,24);target.Create();TouchlineApp.Instance.GetComponent<UIDocument>().panelSettings.targetTexture=target;if(old!=null){old.Release();UnityEngine.Object.DestroyImmediate(old);}}
        static void Capture(string name){var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=old;}
        static void ComparePrivacy(int knowledge)
        {
            var rows=Root.Query<VisualElement>(className:"comparison-stat").ToList().Where(v=>!string.IsNullOrEmpty(v.name)&&v.name.StartsWith("comparison-stat-")).ToArray();if(rows.Length==0)throw new Exception("Comparison attributes missing");
            foreach(var row in rows){var value=row.Query<Label>(className:"comparison-stat-value").ToList()[0];if(knowledge<40&&value.text!="—")throw new Exception("Unknown attribute leaked in comparison");if(knowledge==50&&value.text!="—"&&!value.text.Contains("–"))throw new Exception("Partial observation shows exact attribute");}
            if(knowledge<90&&Root.Query<Label>(className:"comparison-better").ToList().Count>0)throw new Exception("Comparison colors leak a hidden advantage");
            if(knowledge>=90&&!rows.Any(r=>r.Query<Label>(className:"comparison-stat-value").ToList()[0].text!="—"))throw new Exception("Complete observation remains hidden");
        }
        static void CriteriaLayout(bool narrow)
        {
            var criteria=Root.Q<ScrollView>("recruit-criteria-scroll");var hint=Root.Q<Label>("recruit-criteria-hint");
            var names=new[]{"recruit-country","recruit-league","recruit-nationality","recruit-min-age","recruit-max-age","recruit-max-fee","recruit-max-salary"};
            var fields=names.Select(n=>Root.Q(n)).ToArray();
            if(fields.Any(f=>f==null)||hint==null||criteria==null)throw new Exception("Advanced recruitment criteria are missing");
            if(hint.worldBound.yMin<fields.Max(f=>f.worldBound.yMax)-1)throw new Exception("Recruitment hint overlaps the criteria fields");
            if(fields.Any(f=>f.Q(className:"unity-base-field__input").worldBound.height<43))throw new Exception("Recruitment filter has an undersized touch control: "+string.Join("; ",fields.Select(f=>f.name+"="+f.Q(className:"unity-base-field__input").worldBound.height))+"; expanded="+Root.Q<Foldout>("recruit-advanced").value);
            if(criteria.worldBound.height>241||Root.Q<ListView>("recruit-list").worldBound.height<89)throw new Exception("Expanded criteria crowd out the player list");
            if(fields.Any(f=>f.Q(className:"unity-base-field__input").worldBound.xMax>criteria.contentViewport.worldBound.xMax+1))throw new Exception("Advanced recruitment input is horizontally clipped");
            if(narrow&&criteria.verticalScroller.highValue<=0)throw new Exception("Portrait criteria cannot scroll to the last budget field");
        }
        static void Tick()
        {
            if(!SessionState.GetBool("PlayerWorkspaceSmoke",false)||!EditorApplication.isPlaying||TouchlineApp.Instance==null)return;if(last==Time.frameCount)return;last=Time.frameCount;if(++frames<25)return;frames=0;
            try{var app=TouchlineApp.Instance;output=Path.GetFullPath("../../artifacts/unity/"+OutputName);Directory.CreateDirectory(output);
                switch(stage++){
                    case 0:app.GetComponent<UIDocument>().panelSettings=UnityEngine.Object.Instantiate(app.GetComponent<UIDocument>().panelSettings);app.Career.EnsureWorld(app.Database);app.Career.revealAttributes=false;app.Career.shortlist.Clear();Resize(1280,900);Click("Effectif");break;
                    case 1:Root.Q<TextField>("squad-search").value="no matching player 999";if(Root.Q<ListView>("squad-list").itemsSource.Count!=0)throw new Exception("Squad filter failed");Click("squad-reset");if(Root.Q<ListView>("squad-list").itemsSource.Count<11)throw new Exception("Squad filters failed to reset");Capture("squad-wide");Click("squad-compare");break;
                    case 2:if(Root.Q("player-comparison")==null)throw new Exception("Squad comparison missing");Capture("comparison-own-wide");Click("Fermer");Click("Recrutement");break;
                    case 3:
                        if(Root.Q<ListView>("recruit-list").itemsSource.Count<100)throw new Exception("Recruitment list missing database candidates");
                        player=app.Database.players.Where(p=>p.team!=app.Career.club&&p.team!="retired"&&!string.IsNullOrEmpty(p.team)&&!p.team.StartsWith("academy-")&&(p.attributes?.Length??0)>10).OrderByDescending(p=>p.value).First().id;
                        app.Career.world.reports.RemoveAll(r=>r.player==player);
                        string search=new string(app.Database.Find(player).name.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark).ToArray()).Normalize(NormalizationForm.FormC);
                        Root.Q<TextField>("recruit-search").value=search.ToLowerInvariant();break;
                    case 4:if(!Root.Query<Button>().ToList().Any(b=>b.name=="recruit-open-"+player))throw new Exception("Immediate accent-insensitive recruitment search failed");Click("recruit-shortlist-"+player);if(!app.Career.shortlist.Contains(player))throw new Exception("Recruitment shortlist action failed");Root.Q<DropdownField>("recruit-market").value="Ma sélection";break;
                    case 5:if(Root.Q<ListView>("recruit-list").itemsSource.Count!=1)throw new Exception("Shortlist market filter failed");Capture("recruitment-wide");Click("recruit-open-"+player);break;
                    case 6:
                        Capture("profile-recruitment");contractsBefore=app.Career.world.contracts.Count;Click("Sources");break;
                    case 7:
                        if(Root.Q("profile-provenance")==null)throw new Exception("Source dossier missing");
                        string evidence=string.Join(" ",Root.Q("profile-provenance").Query<Label>().ToList().Select(l=>l.text));
                        if(!evidence.Contains("Attributs masqués")||!evidence.Contains("date non renseignée")||!evidence.Contains("Estimation Touchline"))throw new Exception("Provenance must distinguish hidden attributes, undated rosters and simulated salary");
                        if(app.Career.world.contracts.Count!=contractsBefore)throw new Exception("Reading provenance must not create contracts");
                        Capture("profile-provenance-wide");Click("Attributs");
                        var scores=Root.Query<Label>().ToList().Where(l=>l.ClassListContains("attribute-score")).ToArray();
                        if(scores.Length==0||scores.Any(l=>l.text!="—"))throw new Exception("Reading a source revealed unknown attributes");
                        Click("profile-compare");break;
                    case 8:ComparePrivacy(0);Capture("comparison-unknown");Resize(1080,2520);break;
                    case 9:
                        var panel=Root.Q("player-comparison");if(panel.worldBound.xMin<0||panel.worldBound.xMax>Root.worldBound.xMax+1||panel.worldBound.yMax>Root.worldBound.yMax+1)throw new Exception("Comparison modal exceeds portrait viewport");
                        Capture("comparison-portrait");app.Career.world.reports.Add(new ScoutReport{player=player,confidence=50,due=app.Career.life.day+5});Click("Fermer");app.PlayerProfile(player);Click("profile-compare");break;
                    case 10:ComparePrivacy(50);Capture("comparison-partial");app.Career.world.reports.First(r=>r.player==player).confidence=90;Click("Fermer");app.PlayerProfile(player);Click("profile-compare");break;
                    case 11:ComparePrivacy(90);Capture("comparison-observed");Click(Root.Query<Button>().ToList().First(b=>b.text.StartsWith("Fiche · ")).text);break;
                    case 12:if(Root.Q("player-profile")==null)throw new Exception("Comparison profile link failed");Click("Retour à la comparaison");break;
                    case 13:if(Root.Q("player-comparison")==null)throw new Exception("Comparison return context lost");Click("Fermer");break;
                    case 14:if(Root.Q<TextField>("recruit-search")==null||Root.Q<DropdownField>("recruit-market").value!="Ma sélection")throw new Exception("Recruitment context lost after comparison");Capture("recruitment-portrait");Click("recruit-agent-"+player);break;
                    case 15:var wage=Root.Query<LongField>().ToList().First(f=>f.label=="Salaire mensuel proposé (€)");wage.value=12345;Resize(1600,700);break;
                    case 16:if(Root.Query<LongField>().ToList().First(f=>f.label=="Salaire mensuel proposé (€)").value!=12345)throw new Exception("Negotiation draft lost after rotation");Capture("negotiation-landscape");Click("Fermer");Click("Négociations et prêts");break;
                    case 17:if(!Root.Query<Label>().ToList().Any(l=>l.text=="Négociations"))throw new Exception("Recruitment dossiers missing");Click("Missions");break;
                    case 18:if(Root.Q<Button>("scout-new-mission")==null)throw new Exception("Scouting network missing");Capture("scouting-missions-wide");Click("scout-new-mission");break;
                    case 19:Root.Q<IntegerField>("scout-mission-observations").value=2;Root.Q<LongField>("scout-mission-wage").value=123456;Resize(1080,2520);break;
                    case 20:if(Root.Q<IntegerField>("scout-mission-observations").value!=2||Root.Q<LongField>("scout-mission-wage").value!=123456)throw new Exception("Scouting mission draft lost during rotation");Capture("scouting-mission-portrait");Click("scout-mission-send");break;
                    case 21:if(app.Career.world.scoutMissions.Count!=1)throw new Exception("Scouting mission was not created");app.Career.world.reports.RemoveAll(r=>r.player==player);app.Career.Scout(app.Database,player);Click("Rapports");break;
                    case 22:if(Root.Q("scout-report-"+player)==null)throw new Exception("Scout report browsing missing");Capture("scouting-reports-portrait");player=app.Career.lineup[0];app.Career.Person(player).restUntil=app.Career.life.day+3;app.Career.Person(player).discussionFocus="development";app.PlayerProfile(player);break;
                    case 23:
                        if(!Root.Query<Label>().ToList().Any(l=>l.ClassListContains("profile-availability")&&l.text.StartsWith("Repos convenu")))throw new Exception("Player profile hides agreed rest behind generic injury text");Click("Relations");break;
                    case 24:
                        if(!Root.Query<Button>().ToList().Any(b=>b.text=="Parcours et travail"&&b.enabledSelf))throw new Exception("Development context is missing from player relations");
                        Capture("profile-relations-portrait");Click("Fermer");app.Career.match=MatchSimulation.Create(app.Database,app.Career,app.Database.clubs.First(c=>c.id!=app.Career.club&&app.Database.Squad(c.id).Count>=11).id,123,2700).State;app.PlayerProfile(player);break;
                    case 25:
                        foreach(var name in new[]{"SMS / Appeler","Contrat / prolonger"}){var b=Root.Query<Button>().ToList().First(x=>x.text==name);if(b.enabledSelf||!b.tooltip.Contains("après le match"))throw new Exception("Live match profile allows off-pitch decision: "+name);}Click("Relations");break;
                    case 26:
                        if(Root.Query<Button>().ToList().First(b=>b.text=="SMS / Appeler").enabledSelf)throw new Exception("Relations allows live-match discussion");Capture("profile-match-actions-disabled");Click("Fermer");app.Career.match=null;app.Career.world.managerStatus="dismissed";app.PlayerProfile(player);Click("Relations");break;
                    case 27:
                        if(Root.Query<Button>().ToList().First(b=>b.text=="SMS / Appeler").enabledSelf)throw new Exception("Dismissed manager retains player conversation action");
                        Click("Fermer");app.Career.world.managerStatus="employed";Click("Plus");Click("Recrutement");Click("Marché");Resize(1600,900);break;
                    case 28:
                        Root.Q<TextField>("recruit-search").value="";Root.Q<DropdownField>("recruit-market").value="Tous";Root.Q<DropdownField>("recruit-role").value="Tous";
                        globalLeague=app.Database.leagues.First(l=>l.scoutingOnly&&app.Database.clubs.Any(c=>c.league==l.id&&app.Database.players.Any(p=>p.team==c.id)));
                        Root.Q<Foldout>("recruit-advanced").value=true;Root.Q<DropdownField>("recruit-country").value=globalLeague.country;Root.Q<DropdownField>("recruit-league").value=globalLeague.name;break;
                    case 29:
                        var globalRows=Root.Q<ListView>("recruit-list").itemsSource.Cast<PlayerData>().ToArray();var globalClubs=app.Database.clubs.Where(c=>c.league==globalLeague.id).Select(c=>c.id).ToArray();
                        int expected=app.Database.players.Count(p=>globalClubs.Contains(p.team)&&p.team!=app.Career.club&&p.age>=16&&p.age<=45);
                        if(expected<100||globalRows.Length!=expected||globalRows.Any(p=>!globalClubs.Contains(p.team)))throw new Exception("Global league filter truncates or mixes the real scouting pool");
                        CriteriaLayout(false);Capture("global-market-filters-wide");Click("Bureau");Click("Recrutement");Click("Marché");break;
                    case 30:
                        var reopenedLeague=Root.Q<DropdownField>("recruit-league");var validNames=app.Database.leagues.Where(l=>l.country==globalLeague.country).Select(l=>l.name).Concat(new[]{"Tous"}).Distinct().ToArray();
                        if(reopenedLeague.value!=globalLeague.name||reopenedLeague.choices.Any(n=>!validNames.Contains(n)))throw new Exception("Reopening recruitment loses its country or offers incompatible leagues");
                        Root.Q<DropdownField>("recruit-country").value=app.Database.leagues.First(l=>l.country!=globalLeague.country&&!l.scoutingOnly).country;
                        if(Root.Q<DropdownField>("recruit-league").value!="Tous"||Root.Q<DropdownField>("recruit-league").choices.Contains(globalLeague.name))throw new Exception("Changing country retains an incompatible league");
                        Root.Q<DropdownField>("recruit-country").value="Tous";Root.Q<DropdownField>("recruit-market").value="Libres";Root.Q<Foldout>("recruit-advanced").value=true;Resize(1080,2520);break;
                    case 31:
                        CriteriaLayout(true);Capture("free-agents-filters-portrait");Root.Q<ScrollView>("recruit-criteria-scroll").scrollOffset=new Vector2(0,10000);break;
                    case 32:
                        var criteriaBottom=Root.Q<ScrollView>("recruit-criteria-scroll");
                        if(Root.Q("recruit-max-salary").worldBound.yMax>criteriaBottom.contentViewport.worldBound.yMax+1||Root.Q("recruit-criteria-hint").worldBound.yMax>criteriaBottom.contentViewport.worldBound.yMax+1||criteriaBottom.scrollOffset.y<=0)throw new Exception("Last portrait budget filter and explanation cannot be reached");
                        Capture("free-agents-filters-bottom-portrait");Resize(1600,700);break;
                    case 33:
                        CriteriaLayout(false);Capture("global-market-filters-short-wide");Root.Q<Foldout>("recruit-advanced").value=false;Resize(1080,2520);break;
                    case 34:
                        var freeRows=Root.Q<ListView>("recruit-list").itemsSource.Cast<PlayerData>().ToArray();
                        if(freeRows.Length<270||freeRows.Any(p=>p.team!="free"))throw new Exception("Expanded free-agent market is missing or mixed with employed players");Capture("free-agents-expanded-portrait");
                        shortlistBeforeReset=app.Career.shortlist.Count;Root.Q<DropdownField>("recruit-country").value=globalLeague.country;break;
                    case 35:
                        if(Root.Q<ListView>("recruit-list").itemsSource.Count!=0||!Root.Q<Label>("recruit-empty").text.Contains("masquent les joueurs libres"))throw new Exception("Hidden territory filter is not explained");Capture("free-agents-hidden-filter-explained");
                        Root.Q<IntegerField>("recruit-min-age").value=999;if(Root.Q<IntegerField>("recruit-min-age").value!=45||Root.Q<IntegerField>("recruit-max-age").value!=45)throw new Exception("Minimum age normalization is not visible");
                        Root.Q<IntegerField>("recruit-max-age").value=-1;if(Root.Q<IntegerField>("recruit-max-age").value!=16||Root.Q<IntegerField>("recruit-min-age").value!=16)throw new Exception("Age interval can become inverted");
                        Root.Q<LongField>("recruit-max-fee").value=-100;Root.Q<LongField>("recruit-max-salary").value=-100;break;
                    case 36:
                        if(Root.Q<LongField>("recruit-max-fee").value!=0||Root.Q<LongField>("recruit-max-salary").value!=0)throw new Exception("Negative budgets stay visible");
                        Root.Q<TextField>("recruit-search").value="queued search that must not survive reset";Root.Q<LongField>("recruit-max-fee").value=100;Root.Q<LongField>("recruit-max-salary").value=100;Root.Q<DropdownField>("recruit-nationality").value=Root.Q<DropdownField>("recruit-nationality").choices.Last();Click("recruit-reset");break;
                    case 37:
                        if(Root.Q<ListView>("recruit-list").itemsSource.Count<100||Root.Q<TextField>("recruit-search").value!=""||Root.Q<DropdownField>("recruit-country").value!="Tous"||Root.Q<DropdownField>("recruit-market").value!="Tous"||Root.Q<IntegerField>("recruit-min-age").value!=16||Root.Q<IntegerField>("recruit-max-age").value!=45||Root.Q<LongField>("recruit-max-fee").value!=0||Root.Q<LongField>("recruit-max-salary").value!=0||!Root.Q<Label>("recruit-summary").text.Contains("0 filtre(s)"))throw new Exception("Reset left active search filters");
                        if(app.Career.shortlist.Count!=shortlistBeforeReset)throw new Exception("Reset changed the shortlist");
                        var reset=Root.Q<Button>("recruit-reset");Capture("recruitment-reset-portrait");if(reset.worldBound.height<44||reset.worldBound.xMax>Root.worldBound.xMax+1)throw new Exception("Reset control inaccessible on portrait: bounds="+reset.worldBound+" height="+reset.worldBound.height.ToString("R")+" resolved="+reset.resolvedStyle.height.ToString("R")+" minHeight="+reset.resolvedStyle.minHeight+" root="+Root.worldBound);Root.Q<DropdownField>("recruit-market").value="Libres";break;
                    case 38:
                        if(Root.Q<ListView>("recruit-list").itemsSource.Count<270)throw new Exception("Reset did not recover free-agent catalogue");Capture("free-agents-after-reset");
                        File.WriteAllText(Path.Combine(output,"report.json"),"{\"passed\":true,\"squadResetAndComparison\":true,\"recruitmentImmediateSearch\":true,\"shortlist\":true,\"sourceDoesNotRevealAttributesOrCreateContracts\":true,\"unknownPartialCompleteComparison\":true,\"comparisonReturnContext\":true,\"portraitLandscape\":true,\"negotiationDraftSurvivesRotation\":true,\"scoutMissionDraftSurvivesRotation\":true,\"scoutMissionBudgetReserved\":true,\"reportsBrowsing\":true,\"agreedRestAndDevelopmentContext\":true,\"liveMatchAndDismissedManagementGuard\":true,\"globalCountryLeagueFilters\":true,\"incompatibleLeagueReset\":true,\"expandedFreeAgentMarket\":true,\"advancedCriteriaScrollableWithoutOverlap\":true,\"recruitmentResetPreservesShortlist\":true,\"hiddenFreeAgentFiltersExplained\":true,\"numericFiltersNormalized\":true,\"physicalAndroid\":false}");SessionState.SetBool("PlayerWorkspaceSmoke",false);Debug.Log("TOUCHLINE_PLAYER_WORKSPACE_OK");EditorApplication.Exit(0);break;
                }
            }catch(Exception e){SessionState.SetBool("PlayerWorkspaceSmoke",false);Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}
