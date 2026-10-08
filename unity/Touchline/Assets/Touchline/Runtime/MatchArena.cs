using UnityEngine;
using Touchline.Core;
using System;

namespace Touchline
{
    public sealed partial class MatchArena : MonoBehaviour
    {
        public MatchSimulation Simulation {get; private set;}
        public MatchBroadcast Broadcast {get;private set;}
        public bool QuietPresentation=>!GoalReplayActive&&Broadcast!=null&&Broadcast.Enabled&&Broadcast.Quiet;
        public bool Paused=true;
        public int Speed=1;
        public static readonly int[] LiveSpeeds={1,2,3,5,10};
        public MatchNameDisplay NameDisplay {get;private set;}=MatchNameDisplay.NearBall;
        public void SetNameDisplay(MatchNameDisplay mode){NameDisplay=(MatchNameDisplay)Mathf.Clamp((int)mode,0,2);PlayerPrefs.SetInt("match-player-names",(int)NameDisplay);}
        public MatchBallDisplay BallDisplay {get;private set;}=MatchBallDisplay.Discreet;
        public Vector3 BallDisplayPosition=>ball!=null?ball.position:Vector3.zero;
        public void SetBallDisplay(MatchBallDisplay mode){BallDisplay=(MatchBallDisplay)Mathf.Clamp((int)mode,0,2);PlayerPrefs.SetInt("match-ball-locator",(int)BallDisplay);}
        public void SetLiveSpeed(int speed){Speed=Array.IndexOf(LiveSpeeds,speed)>=0?speed:1;PlayerPrefs.SetInt("match-live-speed",Speed);}
        public Action<string> PlayerSelected;
        public Camera MatchCamera {get; private set;}
        public MatchViewport Viewport {get;private set;}
        public void BindViewport(UnityEngine.UIElements.Image image){Viewport??=gameObject.AddComponent<MatchViewport>();Viewport.Bind(this,image);}
        PlayerView[] players;
        public PlayerView PlayerVisual(int index)=>players!=null&&index>=0&&index<players.Length?players[index]:null;
        public string PlayerDisplayName(string id)=>database?.Find(id)?.name??id;
        public string PlayerSurname(string id){var player=database?.Find(id);return !string.IsNullOrWhiteSpace(player?.surname)?player.surname:MatchPlayerLabels.Surname(player?.name??id,player?.nationality);}
        readonly PausedPoseCache poseCache=new PausedPoseCache();
        float renderedAlpha=1,renderedClock=float.NaN;
        public int PoseEvaluations {get;private set;}
        Transform ball,world;
        readonly BallVisualRotation ballRotation=new BallVisualRotation();
        readonly GoalNetPresentation goalNet=new GoalNetPresentation();
        readonly ExitRestartPresentation exitRestart=new ExitRestartPresentation();
        ExitRestartSample exitSample;bool showingExit,exitWasRepositioned;
        public float RestartCutOpacity=>showingExit?exitSample.opacity:0;
        public string RestartCutCaption=>showingExit?exitSample.caption:"";
        public bool ExitBallIsOutgoing=>showingExit&&!exitSample.repositioned;
        RenderBudget renderBudget;
        float zoom=1;Vector3 focus,velocity;float cameraDistance,cameraDistanceVelocity;float cameraAspect=-1;bool cameraReset=true,cameraHasFocus;
        bool tactical;Vector2 pointerStart;float saveAt;
        bool wasQuiet;Database database;
        public Action SaveRequested;
        Material turf,white;Mesh pitchMesh;Texture2D turfGrain,boardAtlas;readonly System.Collections.Generic.List<Mesh> stadiumMeshes=new System.Collections.Generic.List<Mesh>();
        LineRenderer[] tacticalLines;bool showTactics;Material tacticalMaterial;
        public void Initialize(Database database,MatchSimulation simulation)
        {
            Simulation=simulation;this.database=database;Broadcast=new MatchBroadcast(simulation);wasQuiet=false;
            goalNet.Reset();ResetGoalReplay(simulation.State);
            exitRestart.Reset();exitSample=default;showingExit=exitWasRepositioned=false;
            NameDisplay=(MatchNameDisplay)Mathf.Clamp(PlayerPrefs.GetInt("match-player-names",1),0,2);Speed=PlayerPrefs.GetInt("match-live-speed",1);if(Array.IndexOf(LiveSpeeds,Speed)<0)Speed=1;
            BallDisplay=(MatchBallDisplay)Mathf.Clamp(PlayerPrefs.GetInt("match-ball-locator",1),0,2);
            tactical=PlayerPrefs.GetInt("match-camera-mode",0)==1;zoom=Mathf.Clamp(PlayerPrefs.GetFloat("match-camera-zoom",DefaultZoom),.8f,1.35f);cameraReset=true;cameraHasFocus=false;velocity=Vector3.zero;
            poseCache.Reset();renderedAlpha=1;renderedClock=float.NaN;
            PlayerView.UseMecanim=PlayerPrefs.GetInt("match-mecanim",1)==1; // animations capturées (Mixamo) ou procédurales
            renderBudget=TouchlineApp.Instance==null?null:TouchlineApp.Instance.GetComponent<RenderBudget>();renderBudget?.ResetMatchSample();
            var root=new GameObject("Stadium · metres");root.transform.SetParent(transform);world=root.transform;
            // Bandes de tonte claire/sombre ; la texture de grain les assombrit en moyenne (compensé).
            turfGrain=PitchTurf.Grain();turf=TurfMaterial(DarkStripe);white=PlayerView.Material(MarkingWhite);
            // Après-midi : l'ombre du toit d'en face sur la pelouse est simulée (matières assombries,
            // sans ombre portée reçue) car elle dépasse la distance des ombres temps réel.
            var career=TouchlineApp.Instance!=null?TouchlineApp.Instance.Career:null;var fixture=career?.world?.fixtures?.Find(f=>f.id==career.world.activeFixture);
            bool floodlit=StadiumLighting.Floodlit(PlayerPrefs.GetInt(StadiumLighting.PreferenceKey,StadiumLighting.Auto),fixture);
            float shadeEdge=floodlit?float.NegativeInfinity:StadiumLighting.RoofShadowEdge(),shadeEndX=floodlit?float.NegativeInfinity:StadiumLighting.RoofShadowEndX();var shade=StadiumLighting.ShadeTint();
            CreatePitchSurface(false,shadeEdge,shadeEndX,Color.white);
            Surface(StadiumGeometry.Surround(false,shadeEdge,shadeEndX),PlayerView.Material(SurroundGreen),false);
            Surface(StadiumGeometry.PitchMarkings(false,shadeEdge,shadeEndX),white,false);
            if(!floodlit){CreatePitchSurface(true,shadeEdge,shadeEndX,shade);
                Surface(StadiumGeometry.Surround(true,shadeEdge,shadeEndX),PlayerView.Material(SurroundGreen*shade),false,false);
                Surface(StadiumGeometry.PitchMarkings(true,shadeEdge,shadeEndX),PlayerView.Material(MarkingWhite*shade),false,false);}
            for(int sign=-1;sign<=1;sign+=2){var x=52.5f*sign;
                Line(new[]{new Vector3(x,0,-3.66f),new Vector3(x,2.44f,-3.66f),new Vector3(x,2.44f,3.66f),new Vector3(x,0,3.66f)},.12f);
                Surface(StadiumGeometry.GoalNet(sign),white,false);
            }
            var concrete=PlayerView.Material(new Color(.18f,.23f,.28f));var seats=PlayerView.Material(new Color(.23f,.31f,.36f));
            for(int side=-1;side<=1;side+=2){Surface(StadiumGeometry.Stand(side,false),concrete,true);Surface(StadiumGeometry.Stand(side,true),seats,true);Surface(StadiumGeometry.EndStand(side),seats,true);}
            // Le toit, le second anneau et le mur du fond projettent aussi une ombre réelle (joueurs proches de la caméra).
            Surface(StadiumGeometry.UpperStand(),seats,true);Surface(StadiumGeometry.StadiumShell(),PlayerView.Material(new Color(.26f,.29f,.33f)),true);
            Surface(StadiumGeometry.FloodlightMasts(),concrete,false);Surface(StadiumGeometry.FloodlightLamps(),PlayerView.Material(StadiumLighting.LampColor(floodlit)),false);
            var homeClub=Array.Find(database.clubs,c=>c.id==simulation.State.home);var awayClub=Array.Find(database.clubs,c=>c.id==simulation.State.away);
            if(!ColorUtility.TryParseHtmlString(homeClub?.color,out var homeColor))homeColor=new Color(.2f,.42f,.57f);
            if(!ColorUtility.TryParseHtmlString(awayClub?.color,out var awayColor))awayColor=new Color(.65f,.3f,.2f);
            boardAtlas=StadiumAtmosphere.BoardAtlas(homeColor,awayColor);var boards=PlayerView.Material(StadiumLighting.BoardGlow(floodlit));boards.mainTexture=boardAtlas;Surface(StadiumGeometry.PerimeterBoards(),boards,false);
            Surface(StadiumGeometry.TechnicalArea(),concrete,false);Surface(StadiumGeometry.ClubBanners(),PlayerView.Material(Color.Lerp(homeColor,Color.gray,.25f)),false);
            var crowdMesh=StadiumAtmosphere.Crowd(simulation.State.home,simulation.State.away);stadiumMeshes.Add(crowdMesh);
            var crowd=new GameObject(crowdMesh.name);crowd.transform.SetParent(world,false);crowd.AddComponent<MeshFilter>().sharedMesh=crowdMesh;
            var crowdRenderer=crowd.AddComponent<MeshRenderer>();crowdRenderer.sharedMaterials=Array.ConvertAll(StadiumAtmosphere.Palette(homeColor,awayColor),PlayerView.Material);
            crowdRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;crowdRenderer.receiveShadows=false;
            var upperMesh=StadiumAtmosphere.UpperCrowd(simulation.State.home,simulation.State.away);stadiumMeshes.Add(upperMesh);
            var upper=new GameObject(upperMesh.name);upper.transform.SetParent(world,false);upper.AddComponent<MeshFilter>().sharedMesh=upperMesh;
            var upperRenderer=upper.AddComponent<MeshRenderer>();upperRenderer.sharedMaterials=crowdRenderer.sharedMaterials;upperRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;upperRenderer.receiveShadows=false;
            var sky=StadiumLighting.Apply(world,floodlit);RenderSettings.fogStartDistance=110;RenderSettings.fogEndDistance=240;
            MatchCamera=new GameObject("Broadcast camera").AddComponent<Camera>();MatchCamera.transform.SetParent(world);MatchCamera.fieldOfView=46;MatchCamera.nearClipPlane=.15f;MatchCamera.farClipPlane=270;MatchCamera.clearFlags=CameraClearFlags.SolidColor;MatchCamera.backgroundColor=sky;MatchCamera.tag="MainCamera";MatchCamera.gameObject.AddComponent<AudioListener>();MatchCamera.transform.position=new Vector3(0,19,29);
            players=new PlayerView[22];var m=simulation.State;var homeStrip=MatchKitPalette.Home(homeColor);var awayStrip=MatchKitPalette.Away(homeStrip,awayColor);
            for(int i=0;i<22;i++){var p=m.actors[i];var color=p.side==0?homeStrip:awayStrip;var go=new GameObject("Player "+database.Find(p.id).name);go.transform.SetParent(world);players[i]=go.AddComponent<PlayerView>();players[i].Build(database.Find(p.id),p.side,p.slot,color);}
            var football=FootballBallMesh.Create(white,PlayerView.Material(new Color(.025f,.032f,.04f)));football.transform.SetParent(world,false);ball=football.transform;
            tacticalMaterial=PlayerView.Material(new Color(.75f,.9f,.25f));tacticalLines=new LineRenderer[11];for(int i=0;i<11;i++){var marker=new GameObject("Tactical intention "+i);marker.transform.SetParent(world);var line=marker.AddComponent<LineRenderer>();line.sharedMaterial=tacticalMaterial;line.positionCount=2;line.widthMultiplier=.075f;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.enabled=false;tacticalLines[i]=line;}
            BuildGoalReplay();
        }
        void Update()=>RenderFrame(Time.unscaledDeltaTime);
        // Explicit elapsed time also permits deterministic footage and replay
        // validation. Production calls this once per rendered frame.
        public void RenderFrame(float frameDelta)
        {
            if(Simulation==null)return;
            if(float.IsNaN(frameDelta)||float.IsInfinity(frameDelta)||frameDelta<=0)return;
            if(GoalReplayActive){if(!Paused)AdvanceGoalReplay(frameDelta);return;}
            if(!Paused&&!QuietPresentation)renderBudget?.RecordMatchFrame(frameDelta,MatchCamera.targetTexture,Speed);
            frameDelta=Mathf.Min(.1f,frameDelta);
            if(!Paused){Broadcast.Advance(frameDelta,Speed);if(Broadcast.PauseRequested)Paused=true;}else Broadcast.Refresh();
            var m=Simulation.State;if(m.halfTime||m.finished)Paused=true;
            if(Time.unscaledTime-saveAt>12){saveAt=Time.unscaledTime;SaveRequested?.Invoke();}
            MatchCamera.enabled=!QuietPresentation;
            if(QuietPresentation){wasQuiet=true;ClearUnseenGoalReplay();return;}
            bool returning=wasQuiet;wasQuiet=false;
            if(returning){poseCache.Reset();exitRestart.Reset();foreach(var player in players)player.ResetPresentation();}
            if(!Paused)renderedAlpha=Mathf.Clamp01((float)(m.remainder/.1));
            else if(m.halfTime||m.finished||renderedClock!=m.clock)renderedAlpha=1;
            var alpha=renderedAlpha;renderedClock=m.clock;
            bool showingGoal=goalNet.TryPosition(Simulation,m,Simulation.GoalContact,alpha,out var goalPose);
            showingExit=!showingGoal&&exitRestart.TrySample(Simulation,m,Simulation.ExitContact,alpha,out exitSample);
            bool cutCamera=showingExit&&exitSample.repositioned&&!exitWasRepositioned;
            exitWasRepositioned=showingExit&&exitSample.repositioned;
            var exitPosition=showingExit?new Vector3(exitSample.pose.position.x,exitSample.pose.height,exitSample.pose.position.z):Vector3.zero;
            var goalPosition=showingGoal?new Vector3(goalPose.position.x,goalPose.height,goalPose.position.z):Vector3.zero;
            var lookAtBall=showingGoal?goalPosition:showingExit?exitPosition:KeeperBallPresentation.Position(Simulation.KeeperContact,m,alpha,release:Simulation.ReleaseContact,block:Simulation.ImpactContact);
            for(int i=0;i<22;i++){bool visible=!m.actors[i].sentOff;if(players[i].gameObject.activeSelf!=visible)players[i].gameObject.SetActive(visible);if(!visible)continue;if(players[i].PlayerId!=m.actors[i].id)players[i].ChangeIdentity(database.Find(m.actors[i].id));var motion=PlayerMotionContext.From(m,m.actors[i],alpha,players[i].MotionStature);bool changed=poseCache.NeedsUpdate(i,m.actors[i],lookAtBall,motion);if(!Paused||changed){players[i].Render(m.actors[i],alpha,Paused||returning?1:frameDelta*Speed,lookAtBall,motion);PoseEvaluations++;}}
            if(showTactics)for(int i=0;i<11;i++){var aim=Simulation.MovementTarget(i);tacticalLines[i].SetPosition(0,players[i].transform.position+Vector3.up*.05f);tacticalLines[i].SetPosition(1,new Vector3(aim.x,.05f,aim.z));}
            Vector3? heldPosition=null;
            for(int i=0;i<22;i++)if(m.actors[i].id==m.ball.owner&&m.ball.held&&m.actors[i].action!="place-ball")heldPosition=players[i].HeldBallPosition;
            ball.position=showingGoal?goalPosition:showingExit?exitPosition:KeeperBallPresentation.Position(Simulation.KeeperContact,m,alpha,heldPosition,Simulation.ReleaseContact,Simulation.ImpactContact);
            bool ballVisible=!showingExit||exitSample.ballVisible;if(ball.gameObject.activeSelf!=ballVisible)ball.gameObject.SetActive(ballVisible);
            bool visuallyHeld=!ExitBallIsOutgoing&&m.ball.held&&(!KeeperBallPresentation.Current(Simulation.KeeperContact,m)||alpha>=Simulation.KeeperContact.fraction);
            ballRotation.Advance(ball.position,visuallyHeld,Paused?0:frameDelta*Speed);ball.rotation=ballRotation.Rotation;
            // Render the same ball path that the simulation uses; no separate
            // visual attachment that could hide a missed control or a rebound.
            bool setPiece=m.restart>0&&(m.phase=="penalty"||m.phase=="corner"||m.phase=="free-kick"&&ball.position.x*Simulation.Direction(m.restartSide)>20);
            bool portrait=MatchCamera.aspect<.8f;
            var target=tactical?new Vector3(0,.6f,0):new Vector3(Mathf.Clamp(ball.position.x*.90f,-46,46),.6f,Mathf.Clamp(ball.position.z*(portrait?.72f:setPiece?.46f:.58f),portrait?-25:-19,portrait?25:19));
            // A loaded match already has an action location. Establish it on
            // the first visible frame instead of panning in from midfield.
            if(!cameraHasFocus||returning||cutCamera){focus=target;velocity=Vector3.zero;cameraReset=true;cameraHasFocus=true;}
            else focus=Vector3.SmoothDamp(focus,target,ref velocity,.5f/Mathf.Sqrt(Mathf.Max(1,Speed)),160,frameDelta);
            // Fast passes move the action window instead of pulling the camera
            // hundreds of metres away to retain its previous focus.
            if(!tactical){focus.x=Mathf.Clamp(focus.x,ball.position.x-20,ball.position.x+20);focus.z=Mathf.Clamp(focus.z,ball.position.z-13,ball.position.z+13);}
            ReframeCamera(frameDelta);
            CaptureGoalReplay(m,alpha);
            if((Viewport==null||!Viewport.Bound)&&Input.GetMouseButtonDown(0))pointerStart=Input.mousePosition;
            if((Viewport==null||!Viewport.Bound)&&Input.GetMouseButtonUp(0)&&Vector2.Distance(pointerStart,Input.mousePosition)<8&&!TouchlineApp.Instance.PointerOverInterface(Input.mousePosition)){var ray=MatchCamera.ScreenPointToRay(Input.mousePosition);if(Physics.Raycast(ray,out var hit,250)){var view=hit.collider.GetComponentInParent<PlayerView>();if(view!=null){Paused=true;PlayerSelected?.Invoke(view.PlayerId);}}}
        }
        public void CameraMode(){tactical=!tactical;cameraReset=true;PlayerPrefs.SetInt("match-camera-mode",tactical?1:0);}
        public bool TacticalOverlayVisible=>showTactics;
        public bool TacticalCamera=>tactical;
        public void ReframeCamera(float delta=0)
        {
            if(Simulation==null||ball==null||QuietPresentation)return; if(GoalReplayActive){ReframeGoalReplayCamera();return;}
            int direction=ball.position.x>=0?1:-1;var center=tactical?new Vector3(0,.6f,0):focus;
            BroadcastFraming.Apply(MatchCamera,center,ball.position,tactical,Mathf.Abs(ball.position.x)>28,direction,zoom);
            float required=Vector3.Distance(MatchCamera.transform.position,center);
            // Expanding the safe frame is immediate; contracting it is gradual.
            // The second viewport pass must not advance smoothing twice per frame.
            if(cameraReset||Mathf.Abs(cameraAspect-MatchCamera.aspect)>.02f){cameraDistance=required;cameraDistanceVelocity=0;cameraReset=false;}
            else if(required>cameraDistance){cameraDistance=required;cameraDistanceVelocity=0;}
            else if(delta>0)cameraDistance=Mathf.SmoothDamp(cameraDistance,required,ref cameraDistanceVelocity,.8f,20,delta);
            cameraAspect=MatchCamera.aspect;MatchCamera.transform.position=center-MatchCamera.transform.forward*cameraDistance;
            MatchCamera.farClipPlane=Mathf.Max(270,cameraDistance+150);RenderSettings.fogStartDistance=Mathf.Max(110,cameraDistance+60);RenderSettings.fogEndDistance=Mathf.Max(240,cameraDistance+180);
        }
        public void ToggleTacticalOverlay(){showTactics=!showTactics;foreach(var line in tacticalLines)line.enabled=showTactics;}
        public void Zoom(float delta){zoom=Mathf.Clamp(zoom+delta,.8f,1.35f);cameraReset=true;PlayerPrefs.SetFloat("match-camera-zoom",zoom);}
        public void ScaleZoom(float factor){if(float.IsNaN(factor)||float.IsInfinity(factor)||factor<=0)return;zoom=Mathf.Clamp(zoom*factor,.8f,1.35f);cameraReset=true;PlayerPrefs.SetFloat("match-camera-zoom",zoom);}
        // shaded : partie à l'ombre simulée du toit, matières multipliées par tint, sans ombre reçue.
        void CreatePitchSurface(bool shaded,float shadeEdge,float shadeEndX,Color tint)
        {
            var go=new GameObject(shaded?"Pitch in roof shadow":"105 × 68 m pitch");go.transform.SetParent(world,false);
            var mesh=PitchTurf.Surface(shaded,shadeEdge,shadeEndX);if(shaded)stadiumMeshes.Add(mesh);else pitchMesh=mesh;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{TurfMaterial(LightStripe*tint),shaded?TurfMaterial(DarkStripe*tint):turf};renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=!shaded;
        }
        static readonly Color MarkingWhite=new Color(.88f,.89f,.81f),SurroundGreen=new Color(.075f,.18f,.09f);
        static readonly Color LightStripe=new Color(.118f,.325f,.136f),DarkStripe=new Color(.088f,.252f,.100f); // tonte (avant grain)
        const float DefaultZoom=.85f; // cadrage télé par défaut, plus serré (joueurs plus lisibles) ; réglable au pincement
        Material TurfMaterial(Color stripe){var m=PlayerView.Material(stripe/PitchTurf.MeanBrightness);m.color=new Color(m.color.r,m.color.g,m.color.b,1);m.mainTexture=turfGrain;return m;}
        void Surface(Mesh mesh,Material material,bool shadows,bool receiveShadows=true){stadiumMeshes.Add(mesh);var go=new GameObject(mesh.name);go.transform.SetParent(world,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=shadows?UnityEngine.Rendering.ShadowCastingMode.On:UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=receiveShadows;}
        void Line(Vector3[] points,float width){var go=new GameObject("Pitch marking");go.transform.SetParent(world);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=white;line.useWorldSpace=true;line.positionCount=points.Length;line.SetPositions(points);line.widthMultiplier=width;line.numCornerVertices=1;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;if(Array.TrueForAll(points,p=>p.y<.1f)){line.alignment=LineAlignment.TransformZ;go.transform.rotation=Quaternion.Euler(90,0,0);}}
        static void DisposeArenaObject(UnityEngine.Object item){if(item==null)return;if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void OnDestroy(){var materials=new System.Collections.Generic.HashSet<Material>();foreach(var r in GetComponentsInChildren<Renderer>())if(r.GetComponentInParent<PlayerView>()==null)foreach(var material in r.sharedMaterials)if(material!=null)materials.Add(material);foreach(var material in materials)DisposeArenaObject(material);foreach(var mesh in stadiumMeshes)DisposeArenaObject(mesh);DisposeArenaObject(pitchMesh);DisposeArenaObject(turfGrain);DisposeArenaObject(boardAtlas);if(world!=null)DisposeArenaObject(world.gameObject);}
    }
}
