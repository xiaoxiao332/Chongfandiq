using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LastLight
{
    public sealed partial class M1GameSession : M1Session,IM1SessionUI
    {
        [SerializeField] private bool home;
        [SerializeField] private NavMeshSurface navigation;
        private M1Navigator liangNavigation;
        private NavMeshPath repairPath;
        private Vector3 repairDestination;
        private int navigationRepairId;
        private float nextRepairPath;
        public void ConfigureNavigation(NavMeshSurface surface){navigation=surface;}
        [SerializeField] private M1Actor actor;
        [SerializeField] private M1Camera follow;
        [SerializeField] private Camera cameraView;
        [SerializeField] private Material signal;
        [SerializeField] private Transform objects;
        [SerializeField] private Transform liangVisual;
        [SerializeField] private Light stoveLight;
        [SerializeField] private M1Node[] nodes;
        [SerializeField] private M1Beast[] beasts;
        [SerializeField] private GameObject gridRoot;
        [SerializeField] private Vector3 raidTarget=new Vector3(0,0,-12);
        private M1Director director;
        private ResourceScope scope;
        private CancellationTokenSource sceneLifetime;
        private readonly Dictionary<int,InstanceLease> buildings=new Dictionary<int,InstanceLease>();
        private readonly List<InstanceLease> roofs=new List<InstanceLease>();
        private readonly Queue<InstanceLease> footprints=new Queue<InstanceLease>();
        private InstanceLease ghost;
        private InstanceLease raider;
        private AudioSource audioSource;
        private AudioClip[] cues;
        private bool active,syncing,building,repairByLiang,resting;
        private bool footBusy;
        private int footSide;
        private int displayedRevision=-1,rotation,movingId,buildAction;
        private Structure selection;
        private string window,notice="",buildHint="";
        private float noticeUntil,repairTick,refreshAt;
        private int gx,gz;
        private int suppressInputUntil;
        private bool rotateHeld,placeHeld;
        private M1PanelModel hud;
        private M1EconomySystem E=>director.Economy;
        private M1ConstructionSystem C=>director.Construction;
        private M1SurvivalSystem V=>director.Survival;
        private M1StorySystem N=>director.Narrative;
        private M1ThreatSystem T=>director.Threat;
        public bool IsHome=>home;
        public bool Ready=>active;
        public bool Resting=>resting;
        public bool WindowOpen=>window!=null;
        public override bool Paused=>!active||director.Restoring||director.AtMainMenu||director.Busy||WindowOpen||director.Global.Systems.Get<PauseSystem>().Paused||building||Time.frameCount<=suppressInputUntil;
        public override bool BuildingMode=>building;
        public override float ActiveTime=>V.ActiveTime;
        public override Inventory Backpack=>E.Backpack;
        public override StoryState Story=>N.State;
        public override M1Actor Actor=>actor;
        public override IM1SessionUI UI=>this;
        public override Material SignalMaterial=>signal;
        public override bool RaidActive=>T.Active;
        public override Vector3 RaidTarget=>director.IsM2&&director.World.Data.spring?SpringRaidTarget():raidTarget;
        public override int Pickups { get=>E.Pickups;set=>E.Pickups=value; }
        public override int RaidStrikes { get=>T.Strikes;set=>throw new InvalidOperationException(L.K("teb03034bab")); }
        public void Configure(bool isHome,M1Actor player,M1Camera camera,Camera view,Material cue,Transform dynamicRoot,Transform liang,Light heat,M1Node[] interactions,M1Beast[] enemies,GameObject grid)
        {home=isHome;actor=player;follow=camera;cameraView=view;signal=cue;objects=dynamicRoot;liangVisual=liang;stoveLight=heat;nodes=interactions;beasts=enemies;gridRoot=grid;}
        public void Bind(M1Director owner){director=owner;}
        public void ValidateConfiguration()
        {
            if(navigation==null||actor==null||follow==null||cameraView==null||signal==null||objects==null||nodes==null||nodes.Length==0||beasts==null)
                throw new InvalidOperationException(gameObject.scene.path+"/"+name+L.F("t8081bdcc86",actor!=null,follow!=null,cameraView!=null,signal!=null,objects!=null,nodes?.Length,beasts!=null));
            if(home&&(liangVisual==null||stoveLight==null))throw new InvalidOperationException(name+L.K("ta633eaa682"));
            if(nodes.Any(n=>n==null)||nodes.Select(n=>n.StableId).Distinct().Count()!=nodes.Length)throw new InvalidOperationException(name+L.K("te914b222c8"));
        }
        public async Task ActivateAsync()
        {
            if(active)return;scope=new ResourceScope();sceneLifetime=new CancellationTokenSource();
            repairPath=new NavMeshPath();navigationRepairId=0;nextRepairPath=0;
            navigation.BuildNavMesh();
            if(navigation.navMeshData==null)throw new InvalidOperationException(L.K("tfb0789e50f"));
            if(home){liangNavigation=liangVisual.GetComponent<M1Navigator>()??liangVisual.gameObject.AddComponent<M1Navigator>();liangNavigation.Initialize(this,1.4f);}
            follow.Configure(actor.transform);follow.Initialize(this);actor.Initialize(this);
            foreach(var node in nodes){node.SetReadyAt(E.NodeReady.TryGetValue(node.StableId,out var time)?time:0);node.Refresh(ActiveTime);}
            foreach(var beast in beasts)beast.Initialize(this,false,beast.transform.GetChild(0));
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;
            cues=new AudioClip[4];for(int i=0;i<cues.Length;i++)cues[i]=Tone(i);
            if(home)await SyncBuildings();
            if(director.IsM2){await RestoreEnemiesAsync();ApplySeasonVisuals();}
            hud=new M1PanelModel{Title=director.IsM2?L.K("td3f9844799"):L.K("t6eb186dd22"),Body=HudText};
            await director.Global.UI.OpenAsync("M1HUD",hud);
            active=true;Notify(home?L.K("t3657f48b28"):L.K("t128025fd34"));
        }
        public void Deactivate()
        {
            if(!active&&scope==null)return;
            active=false;sceneLifetime?.Cancel();liangNavigation?.Shutdown();if(raider!=null)raider.Instance.GetComponent<M1Navigator>()?.Shutdown();foreach(var enemy in beasts)if(enemy!=null)enemy.GetComponent<M1Navigator>()?.Shutdown();if(navigation!=null){navigation.RemoveData();if(navigation.navMeshData!=null){Destroy(navigation.navMeshData);navigation.navMeshData=null;}}CloseWindow();if(director!=null&&director.Global!=null)director.Global.UI.Close("M1HUD");
            if(director!=null&&director.Global.Session!=null){C.CancelRepair();C.LiangStatus=Story.liang?L.K("t0afc4ed740"):L.K("t2ae339494f");}
            scope?.Dispose();scope=null;buildings.Clear();roofs.Clear();footprints.Clear();ghost=null;raider=null;
            if(cues!=null)foreach(var clip in cues)if(clip!=null)Destroy(clip);cues=null;
            if(audioSource!=null)Destroy(audioSource);audioSource=null;displayedRevision=-1;
        }
        private void OnDestroy(){if(active||scope!=null)Deactivate();}
        private void Update()
        {
            if(!active)return;
            if(building){BuildInput();return;}
            if(Paused)return;
            float dt=Time.deltaTime;
            if(director.IsM2)TickM2(dt);
            bool sheltered=home&&V.Warm&&(Vector3.Distance(actor.transform.position,new Vector3(0,0,-9))<6||InHouse(actor.transform.position));
            V.Tick(dt,Story.stove,sheltered,director.IsM2&&director.World.Data.spring);
            if(resting){V.Rest(dt);repairTick-=dt;if(repairTick<=0){resting=false;Notify(L.K("t612d324928"));}}
            if(V.Health<=0||actor.transform.position.y< -6){director.Run(RescueAsync());return;}
            if(home)
            {
                stoveLight.enabled=Story.stove&&V.Warm;
                if(!director.IsM2||!director.World.Data.bridgeOrdered)TickRepairs(dt);
                if(displayedRevision!=C.Revision&&!syncing)director.Run(SyncBuildings());
            }
            if(Time.unscaledTime>=refreshAt){foreach(var n in nodes)n.Refresh(ActiveTime);refreshAt=Time.unscaledTime+.3f;}
            if(T.Active)
            {
                bool failed=T.Strikes>=3;
                var beast=raider?.Instance.GetComponent<M1Beast>();
                if(failed||beast!=null&&!beast.Active||ActiveTime-T.StartedAt>=100)FinishRaid(failed);
            }
        }
        private bool InHouse(Vector3 p){int x=Mathf.RoundToInt(p.x/2),z=Mathf.RoundToInt(p.z/2);return C.Grid.IsFloor(x,z)&&C.Grid.Room(x,z,out var door).Count>0&&door;}
        private string HudText()
        {
            var target=NearestNode();
            return (director.IsM2?M2HudHeader():"")+L.F("t018ec04c58",V.Health,V.Hunger,V.Exposure,Backpack.UsedSlots,(home?L.K("tdbe00e5028"):gameObject.scene.name=="Greenhouse"?L.K("tad248c21cc"):L.K("tcb5e5cb0fb")),ActiveTime/60,(director.IsM2?M2Objective():N.Objective(C)))+
                (T.Active?L.F("t601fbd4079",T.Strikes):Story.raidFinished?L.F("tc4e359e749",Mathf.Max(0,T.BufferUntil-ActiveTime)/60):"")+
                NavigationText()+"\n"+(target!=null?"["+(director.IsM2?actor.Binding("Interact"):"E")+"] "+target.Label+"\n":"")+(ActiveTime<noticeUntil?notice:"")+(director.IsM2?M2ControlHints():L.K("t32feb039a8"));
        }
        private string NavigationText()
        {
            M1Node destination=null;
            if(home)
            {
                if(!Story.awake)destination=nodes.First(n=>n.Kind==NodeKind.Terminal);
                else if(!Story.stove)
                {
                    if(E.Storage[Resource.Stone]>=2&&E.Storage[Resource.Scrap]>=2)destination=nodes.First(n=>n.Kind==NodeKind.EmergencyStove);
                    else if(Backpack[Resource.Stone]+E.Storage[Resource.Stone]>=2&&Backpack[Resource.Scrap]+E.Storage[Resource.Scrap]>=2)destination=nodes.First(n=>n.Kind==NodeKind.Depot);
                    else {var needed=Backpack[Resource.Stone]+E.Storage[Resource.Stone]<2?Resource.Stone:Resource.Scrap;destination=nodes.Where(n=>n.Kind==NodeKind.Pickup&&n.Resource==needed&&n.Available(ActiveTime)).OrderBy(n=>(n.transform.position-actor.transform.position).sqrMagnitude).FirstOrDefault();}
                }
                else if(!C.HasHouse) return L.K("t85c78e079b");
                else if(!Story.core)destination=nodes.First(n=>n.Kind==NodeKind.Travel);
                else if(!Story.liang||!Story.reward)destination=nodes.First(n=>n.Kind==NodeKind.Liang);
                else destination=nodes.First(n=>n.Kind==NodeKind.Terminal);
            }
            else if(director.IsM2&&gameObject.scene.name=="Greenhouse")destination=nodes.FirstOrDefault(n=>n.Kind==NodeKind.Sample&&!director.World.Data.samples[n.Amount])??nodes.First(n=>n.Kind==NodeKind.Travel);
            else destination=nodes.First(n=>n.Kind==(Story.core?NodeKind.Shortcut:NodeKind.Core));
            if(destination==null)return L.K("t1fcaf9de02");
            var d=destination.transform.position-actor.transform.position;d.y=0;float horizontal=Vector3.Dot(d,cameraView.transform.right),vertical=Vector3.Dot(d,cameraView.transform.forward);
            string arrow=Mathf.Abs(horizontal)>Mathf.Abs(vertical)?horizontal>0?"→":"←":vertical>0?"↑":"↓";
            return L.F("t310949291f",arrow,destination.Label.Split('：')[0],d.magnitude);
        }
        public override void Notify(string message){notice=message;noticeUntil=ActiveTime+9;}
        private M1Node NearestNode()=>nodes.Where(n=>n.Available(ActiveTime)).OrderBy(n=>(n.transform.position-actor.transform.position).sqrMagnitude).FirstOrDefault(n=>Vector3.Distance(n.transform.position,actor.transform.position)<2.7f);
        public override void InteractNearest()
        {
            if(Paused)return;
            var node=NearestNode();if(node!=null){node.Use(this);return;}
            if(home){var built=C.Grid.Items.OrderBy(b=>(Position(b)-actor.transform.position).sqrMagnitude).FirstOrDefault(b=>Vector3.Distance(Position(b),actor.transform.position)<3);
                if(built!=null){OpenFacility(built);return;}}
            Notify(L.K("t8d56d822a1"));
        }
        public override void UseNode(M1Node node)
        {
            if(Paused||!nodes.Contains(node)||Vector3.Distance(node.transform.position,actor.transform.position)>2.8f)return;
            switch(node.Kind)
            {
                case NodeKind.Bridge:OpenBridge();break;
                case NodeKind.Greenhouse:if(director.World.Data.bridge&&!T.Active)director.Run(director.TravelAsync("Greenhouse"));else Notify(L.K("tf677d9062f"));break;
                case NodeKind.Sample:director.World.Data.samples[node.Amount]=true;director.AutoSave();OpenText(L.K("tad248c21cc"),L.K("td4b2f6ee10"),()=>{});break;
                case NodeKind.GreenhouseRecord:director.World.Data.greenhouseRecord=true;N.Log(L.K("t148df5fd2e"));director.RequestAutoSave();OpenText(L.K("tcc13fe962a"),L.K("t148df5fd2e"),()=>{});break;
                case NodeKind.Pickup:
                    if(E.Pickup(node.StableId,node.Resource,node.Amount,node.Respawn,ActiveTime)){node.SetReadyAt(E.NodeReady[node.StableId]);node.Refresh(ActiveTime);Notify(Catalog.ResourceNames[(int)node.Resource]+" +"+node.Amount);Sound(0);}else Notify(L.K("t31bd392b96"));break;
                case NodeKind.Terminal:OpenTerminal();break;
                case NodeKind.Depot:OpenInventory();break;
                case NodeKind.EmergencyBench:OpenCrafting();break;
                case NodeKind.EmergencyStove:OpenStove();break;
                case NodeKind.Pod:OpenRest(null);break;
                case NodeKind.Liang:OpenLiang();break;
                case NodeKind.Core:
                    if(!Story.core){Story.core=true;Story.shortcut=true;N.Log(L.K("t5670aeb9b3"));Sound(1);}OpenText(L.K("t5050d2e396"),L.K("t8e233da0b2"),()=>{});break;
                case NodeKind.Record:Story.readRecord=true;N.Log(L.K("t6410f0da8b"));OpenText(L.K("t26e408d405"),L.K("t7ace1c2754"),()=>{});break;
                case NodeKind.Travel:
                    if(director.IsM2&&home&&!C.Grid.Items.Any(b=>b.type==Structure.Workbench&&!b.Damaged)){Notify(L.K("t9cc67bb2b4"));break;}
                    if(home&&(!Story.stove||!C.HasHouse||!C.Grid.Items.Any(b=>b.type==Structure.Bed)||!C.Grid.Items.Any(b=>b.type==Structure.Storage))){Notify(L.K("td110feb9b2"));break;}
                    if(T.Active){Notify(L.K("t04327013d3"));break;}director.Run(director.TravelAsync(home?"Workshop":"Home"));break;
                case NodeKind.Shortcut:if(Story.shortcut)director.Run(director.TravelAsync("Home"));else Notify(L.K("t2c81c0bfe9"));break;
                case NodeKind.AidRoute:
                    if(!Story.aidRoute)Notify(L.K("t7d20d0b635"));
                    else if(E.Pickup(node.StableId,Resource.Scrap,6,180,ActiveTime)){node.SetReadyAt(E.NodeReady[node.StableId]);Notify(L.K("t0c3430bd63"));}break;
            }
        }
        private void OpenText(string title,string body,Action after)=>Show("M1Window",new M1PanelModel{Title=title,Body=()=>body,Labels=new[]{L.K("tcb63c62e50")},Command=_=>{CloseWindow();after();}});
        private void Show(string id,M1PanelModel model)
        {
            if(director.IsM2&&model.Command!=null){var command=model.Command;model.Command=i=>{command(i);director.RequestAutoSave();};}
            CloseWindow();window=id;
            model.Closed=()=>{if(window==id){window=null;building=false;ghost?.Dispose();ghost=null;}};
            director.Run(director.Global.UI.OpenAsync(id,model));
        }
        public void CloseWindow(){var old=window;window=null;building=false;suppressInputUntil=Time.frameCount+1;if(gridRoot!=null)gridRoot.SetActive(false);ghost?.Dispose();ghost=null;if(old!=null&&director?.Global!=null)director.Global.UI.Close(old);}
        private string Stock()=>string.Join("\n",Enum.GetValues(typeof(Resource)).Cast<Resource>().Select(r=>L.F("t120b3ed549",Catalog.ResourceNames[(int)r],Backpack[r],E.Storage[r])));
        public void OpenInventory()
        {
            if(window=="M1Inventory"){CloseWindow();return;}
            bool deposit=true;
            Show("M1Inventory",new M1PanelModel{Title=L.K("t82b6b84bcf"),Body=()=>Stock()+L.F("td5c363b26c",(deposit?L.K("t5fd9621110"):L.K("t44fd7b3c1c")),(home?L.K("t7f92393921"):L.K("tcdcf7f24a7")),(Story.core?L.K("t760d13f681"):L.K("tc9301e7375"))),Labels=Catalog.ResourceNames.Concat(new[]{L.K("t4ab4cc07ba"),L.K("tef2bde0bfa"),L.K("t6c14bd7f6f")}).ToArray(),Command=i=>{if(i==10){CloseWindow();return;}if(i==8){deposit=!deposit;return;}if(!home){Notify(L.K("tb08e27cc88"));return;}if(i==9){Notify(L.K("td38fea4a0e")+Backpack.Deposit(E.Storage)+L.K("t03caa791c9"));return;}if(!(deposit?Backpack.Transfer(E.Storage,(Resource)i,1):E.Storage.Transfer(Backpack,(Resource)i,1)))Notify(L.K("t469a7ded50"));}});
        }
        public void OpenJournal()
        {
            if(window=="M1Journal"){CloseWindow();return;}
            Show("M1Journal",new M1PanelModel{Title=L.K("t68bfd09817"),Body=()=>(director.IsM2?M2Objective():N.Objective(C))+L.K("t9b586eec8a")+C.LiangStatus+L.K("td771d0a1b6")+PromiseText()+"\n\n"+string.Join("\n\n",N.Journal)+L.K("t2eaa17bba8"),Labels=new[]{L.K("t6c14bd7f6f")},Command=_=>CloseWindow()});
        }
        public void OpenPause(){if(director.IsM2){OpenM2Pause();return;}OpenM1Pause();}
        private void OpenM1Pause()=>Show("M1Pause",new M1PanelModel{Title=L.K("t130448bce6"),Body=()=>L.K("t3232f61dac"),Labels=new[]{L.K("t1fc1afc5c5"),L.K("tcad0a16196"),L.K("tc5a1847f69")},Command=i=>{CloseWindow();if(i==1)director.Run(RescueAsync());if(i==2)Show("M1Window",new M1PanelModel{Title=L.K("tff7f7651ec"),Body=()=>L.K("t96df785b9d"),Labels=new[]{L.K("tf17fc9b7f6"),L.K("t4d0b4688c7")},Command=j=>{CloseWindow();if(j==0)director.Run(director.RestartAsync());}});}});
        private void OpenCrafting()=>Show("M1Window",new M1PanelModel{Title=L.K("tbcf0dd070b"),Body=()=>Stock()+L.K("t8a11cc18b1"),Labels=new[]{L.K("t33e9873705"),L.K("t01c321bc7e"),L.K("t6c14bd7f6f")},Command=i=>{if(i==2){CloseWindow();return;}Notify((i==0?E.CraftFuel():E.CraftParts())?L.K("t56ca415588"):L.K("tab0fb1c9fe"));}});
        private void OpenStove()=>Show("M1Window",new M1PanelModel{Title=L.K("t6555216586"),Body=()=>Story.stove?L.F("tc75ec33ffb",Mathf.Max(0,V.StoveUntil-ActiveTime),Stock()):L.K("t9a3409d702"),Labels=new[]{L.K("t5bd21fc6b0"),L.K("t6c14bd7f6f")},Command=i=>{if(i==1){CloseWindow();return;}if(!Story.stove){if(!E.Storage.Exchange(Catalog.StoveRepair,new Cost(Resource.Fuel,1),new Cost(Resource.Ration,2))){Notify(L.K("ta4843e8c7e"));return;}Story.stove=true;N.Log(L.K("t7c4ce681bd"));V.Fuel(E.Storage);Sound(1);}else Notify(V.Fuel(E.Storage)?L.K("t369941cdb1"):L.K("t0454652567"));}});
        private void OpenLiang()=>Show("M1Window",new M1PanelModel{Title=L.K("t05a4410426"),Body=()=>!Story.liang?L.K("tbe756a2bb8"):L.F("t0bf964ee64",C.LiangStatus,C.LiangRepairs),Labels=new[]{L.K("t52938b6a3e"),L.K("t6e219fd234"),L.K("t4420e0d76c"),L.K("t6c14bd7f6f")},Command=i=>{if(i==3){CloseWindow();return;}if(i==0){if(!Story.liang){if(!Story.core){Notify(L.K("tc84a6ea9d1"));return;}Story.liang=true;C.LiangWorking=true;N.Log(L.K("t602923b430"));Sound(1);}if(Story.ClaimReward(E.Storage)){Notify(L.K("t9715fefb27"));N.Log(L.K("t4dffb50500"));}else if(!Story.reward)Notify(L.K("t87bf0f0634"));}if(i==1&&Story.liang){C.LiangWorking=!C.LiangWorking;C.CancelRepair();C.LiangStatus=C.LiangWorking?L.K("tae4e3e499a"):L.K("t20e5a6dff8");}if(i==2&&C.LiangBroken){if(E.Storage.Pay(new Cost(Resource.Parts,1))){C.LiangBroken=false;Notify(L.K("t4f5258571b"));}else Notify(L.K("t021fd58b33"));}}});
        private string PromiseText()=>Story.promise==0?L.K("t72077749f7"):Story.promise==1?L.K("t916af5813a"):Story.promise==2?L.K("t97c62fc950"):Story.promise==3?L.K("tbf8fcb9bc9"):L.K("tb6837a7177");
        private void OpenTerminal()
        {
            if(director.IsM2&&Story.completed){OpenBridge();return;}
            if(!Story.awake){Story.awake=true;N.Log(L.K("tf9dc39456c"));OpenText(L.K("t92b710b780"),L.K("t7ca7450e1a"),()=>{});return;}
            if(Story.liang&&Story.reward&&Story.choice==0){OpenChoice();return;}
            Show("M1Window",new M1PanelModel{Title=L.K("te9a948c32d"),Body=()=>(director.IsM2?M2Objective():N.Objective(C))+"\n\n"+PromiseText()+L.K("t765dfc6b4e"),Labels=new[]{L.K("td7a24a980c"),L.K("t71918ed7a8"),L.K("tfe02737dd7"),L.K("t0ddc089d7d"),L.K("t4fdfd76278"),L.K("t16df7b2f6e"),L.K("t078d1ce443"),L.K("t6c14bd7f6f")},Command=i=>{switch(i){case 0:Story.defenseRead=true;OpenText(L.K("td21b5674ed"),L.K("t800e7e65e6"),()=>{});break;
                case 1:if(Story.reinforced)Notify(L.K("tfd9c60c78f"));else if(!C.Grid.Items.Any(b=>b.type==Structure.Door))Notify(L.K("t95e38540b5"));else if(E.Storage.Pay(Catalog.Reinforcement)){Story.reinforced=true;N.Log(L.K("t20a1a3ec70"));Notify(L.K("t356f7cd687"));}else Notify(L.K("tc96615a2af"));break;
                case 2:CloseWindow();director.Run(StartRaidAsync());break;
                case 3:var bed=C.Grid.Items.FirstOrDefault(b=>b.type==Structure.Bed&&!b.Damaged&&string.IsNullOrEmpty(b.occupant));if(Story.Fulfill(E.Storage,bed)){N.Log(L.K("tadc53d726d"));Notify(L.K("t792b2682ba"));C.Changed();}else Notify(L.K("tab744f4872"));break;
                case 4:Notify(Story.ExtendPromise()?L.K("t8f8f3f3de7"):L.K("t287eb225e9"));break;
                case 5:Notify(Story.WithdrawPromise()?L.K("t3eeb22e437"):L.K("t7ac3f01c44"));break;
                case 6:ShowConsequences();break;
                case 7:CloseWindow();break;}}});
        }
        private void OpenChoice()=>Show("M1Event",new M1PanelModel{Title=L.K("t5687fbb08f"),Body=()=>L.K("tb319ed3042")+E.Storage[Resource.Parts],Labels=new[]{L.K("tdfe6ab3e1a"),L.K("t2625cdd2b3"),L.K("tf06bc110fd"),L.K("t28f5a573c0")},Command=i=>{if(i==3){CloseWindow();return;}if(!Story.Choose(i+1,E.Storage)){Notify(L.K("t3d2bffb0aa"));return;}N.Log(i==0?L.K("tfdd7ec0703"):i==1?L.K("t657fecea81"):L.K("td9b859d86e"));CloseWindow();Sound(1);}});
        private void ShowConsequences()
        {
            if(!Story.raidFinished){Notify(L.K("t3fa4458060"));return;}
            string outcome=Story.choice==1?L.K("tbd237f3a93"):Story.choice==2?L.K("t531dea9842")+PromiseText()+L.K("t6bd50d25f4"):L.K("tb036315092");
            Story.consequence=true;N.Log(outcome);
            bool recovery=!C.Grid.Items.Any(b=>b.Damaged)&&!C.LiangBroken&&Story.promise!=1&&Story.promise!=2;
            if(!recovery){OpenText(L.K("t6dea749e5b"),outcome+"\n\n"+(director.IsM2?M2Objective():N.Objective(C)),()=>{});return;}
            Story.completed=true;
            if(director.IsM2){OpenBridge();return;}
            Show("M1End",new M1PanelModel{Title=L.K("t166fd2787c"),Body=()=>outcome+L.F("t9a2c717e3f",ActiveTime/60,(Time.realtimeSinceStartupAsDouble-director.StartedWallTime)/60,C.Grid.Items.Count,C.LiangRepairs,V.Rescues),Labels=new[]{L.K("t8194215f65"),L.K("tc5a1847f69")},Command=i=>{CloseWindow();if(i==1)director.Run(director.RestartAsync());}});
        }
        public override void Eat(){Notify(V.Eat(Backpack)?L.K("tdfea33e936"):L.K("tb60396ec54"));}
        private void OpenRest(Building bed){if(director.IsM2){OpenM2Rest(bed);return;}OpenM1Rest(bed);}
        private void OpenM1Rest(Building bed)=>Show("M1Window",new M1PanelModel{Title=L.K("t5b827051a8"),Body=()=>L.K("tde82f0306a")+(bed!=null?L.K("ta2e6130ceb"):L.K("t5a0ed2cb19")),Labels=new[]{L.K("tda11d57634"),L.K("t6c14bd7f6f")},Command=i=>{CloseWindow();if(i==1)return;if(T.Active||Story.choice>0&&!Story.raidFinished){Notify(L.K("t46b300be3c"));return;}if(bed!=null){if(bed.Damaged||bed.occupant=="visitor"){Notify(L.K("t144b9ede4a"));return;}foreach(var b in C.Grid.Items)if(b.occupant=="player")b.occupant="";bed.occupant="player";}resting=true;repairTick=20;Notify(L.K("te0af22ecde"));}});
        private void OpenFacility(Building b)
        {
            if(b.Damaged){Show("M1Window",new M1PanelModel{Title=L.K("tff10cbff94")+Catalog.StructureNames[(int)b.type],Body=()=>L.K("t11d4fcf2be")+b.health+L.K("t63faac2c55")+Catalog.Describe(Catalog.Repair(b.type))+L.K("tdd5b5d8c1c"),Labels=new[]{L.K("tfdf2eaab94"),L.K("t6c14bd7f6f")},Command=i=>{CloseWindow();if(i==0){C.CancelRepair();repairByLiang=false;Notify(C.BeginRepair(b,E.Storage)?L.K("t82b8f0cc03"):L.K("te15531d486"));}}});return;}
            switch(b.type){case Structure.Storage:OpenInventory();break;case Structure.Workbench:OpenCrafting();break;case Structure.Stove:OpenStove();break;case Structure.Bed:OpenRest(b);break;case Structure.Planter:OpenPlanter(b);break;default:Notify(L.K("te622ceec42"));break;}
        }
        private void TickRepairs(float dt)
        {
            if(C.RepairTarget!=0)
            {
                var target=C.Find(C.RepairTarget);if(target==null){C.CancelRepair();return;}
                bool reachable;
                if(repairByLiang){if(!C.LiangWorking||C.LiangBroken){C.CancelRepair();return;}reachable=WalkLiang(Position(target),dt);}
                else reachable=Vector3.Distance(actor.transform.position,Position(target))<3;
                if(C.TickRepair(dt,E.Storage,reachable,repairByLiang)){Notify(repairByLiang?L.K("tef11312b54"):L.K("t9464ada769"));Sound(1);}
                else if(!repairByLiang)C.LiangStatus=L.K("t30fbe67d5f");else if(reachable)C.LiangStatus=L.F("t888815abcf",C.RepairProgress);
                return;
            }
            liangNavigation?.Stop();
            if(!Story.liang){C.LiangStatus=L.K("t2ae339494f");return;}
            if(C.LiangBroken){C.LiangStatus=L.K("tc3182d0a08");return;}
            if(!C.LiangWorking){C.LiangStatus=L.K("t20e5a6dff8");return;}
            var damaged=C.Grid.Items.FirstOrDefault(b=>b.Damaged);
            if(damaged==null){C.LiangStatus=L.K("tbaa1c2303c");return;}
            if(!C.BeginRepair(damaged,E.Storage)){C.LiangStatus=L.K("t73775b9dd0")+Catalog.Describe(Catalog.Repair(damaged.type));return;}
            repairByLiang=true;
        }
        private bool WalkLiang(Vector3 target,float dt)
        {
            if(liangNavigation==null||!liangNavigation.Available){C.LiangStatus=L.K("t89847eba82");return false;}
            var agent=liangNavigation.Agent;
            if(navigationRepairId!=C.RepairTarget||Time.time>=nextRepairPath)
            {
                navigationRepairId=C.RepairTarget;nextRepairPath=Time.time+1;
                float best=float.PositiveInfinity;
                for(int i=0;i<16;i++)
                {
                    var offset=Quaternion.Euler(0,i*22.5f,0)*Vector3.forward*1.8f;
                    if(!NavMesh.SamplePosition(target+offset,out var candidate,.5f,agent.areaMask)||Vector3.Distance(candidate.position,target)>2.65f)continue;
                    var from=candidate.position+Vector3.up*.6f;var delta=target+Vector3.up*.6f-from;
                    if(Physics.Raycast(from,delta.normalized,out var obstruction,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                    {var built=obstruction.collider.GetComponentInParent<M1Built>();if(built==null||built.Data?.id!=C.RepairTarget)continue;}
                    if(!NavMesh.CalculatePath(agent.nextPosition,candidate.position,agent.areaMask,repairPath)||repairPath.status!=NavMeshPathStatus.PathComplete)continue;
                    float length=0;var corners=repairPath.corners;for(int k=1;k<corners.Length;k++)length+=Vector3.Distance(corners[k-1],corners[k]);
                    if(length<best){best=length;repairDestination=candidate.position;}
                }
                if(float.IsPositiveInfinity(best)){liangNavigation.Stop();C.LiangStatus=L.K("t864170a60d");navigationRepairId=0;return false;}
            }
            if(Vector3.Distance(liangVisual.position,repairDestination)<.3f){liangNavigation.Stop();return true;}
            liangNavigation.Go(repairDestination);C.LiangStatus=L.K("teba3b8ec21");return false;
        }

        public async Task StartRaidAsync()
        {
            bool counter=C.Grid.Items.Any(b=>b.type==Structure.Barricade&&!b.Damaged)||Story.reinforced;
            if(!home||!Story.liang||!C.HasHouse||!counter||!Story.defenseRead||Story.choice==0){Notify(L.K("t79c638bb2e"));return;}
            if(T.Active||Story.raidFinished)return;
            var incoming=await director.Global.Systems.Get<ResourceSystem>().RentAsync(director.Prefix+"Prefabs/Raider",objects,scope,1,sceneLifetime.Token);
            if(!T.Start(ActiveTime,Story,true)){incoming.Dispose();return;}raider=incoming;
            var go=raider.Instance;go.transform.position=new Vector3(0,0,-24);go.SetActive(true);go.GetComponent<M1Beast>().Initialize(this,true,go.transform.GetChild(0));
            N.Log(L.K("tdaeecfe845"));Notify(L.K("td695d5beff"));Sound(3);
        }
        private void FinishRaid(bool failed)
        {
            if(director.IsM2&&director.World.Data.spring){if(!T.FinishSeasonal(failed,ActiveTime,E.Storage))return;director.World.Data.seasonalRaids++;director.World.Data.nextThreat=director.World.Data.springTime+1800;}
            else if(!T.Finish(failed,ActiveTime,Story,E.Storage))return;
            if(failed){foreach(var b in C.Grid.Items.Where(b=>b.type==Structure.Workbench||b.type==Structure.Storage||b.type==Structure.Stove).Take(2)){if(T.RecordEquipment(b.id)){b.health=0;C.Changed();}}}
            raider?.Dispose();raider=null;
            N.Log(failed?L.K("t9aa46a7bdf"):L.K("t39037b9ec2"));Notify(failed?L.K("t12c51d056b"):L.K("tc5ada7267b"));
        }
        private async Task RescueAsync(){if(T.Active)FinishRaid(true);V.Rescue(Backpack);C.CancelRepair();resting=false;N.Log(L.K("te863c7e3d4"));if(!home)await director.TravelAsync("Home");else actor.Teleport(new Vector3(0,0,-14));Notify(L.K("tf58b9ade1c"));}
        public override void TakeDamage(float damage){if(Paused)return;V.Damage(damage);Sound(2);Notify(L.K("t4dc5fc817a"));}
        public override bool TryRecordDamage(int buildingId)=>T.RecordEquipment(buildingId);
        public override void DamageBuilding(int id,float amount){if(Story.reinforced&&C.Find(id)?.type==Structure.Door)amount*=.45f;if(C.Damage(id,amount,T)&&buildings.TryGetValue(id,out var lease))lease.Instance.GetComponent<M1Built>().UpdateVisual();}
        public override void RecordRaidStrike()=>T.Strike();
        public override void Melee(Vector3 position,Vector3 forward)
        {
            foreach(var enemy in beasts.Concat(raider!=null?new[]{raider.Instance.GetComponent<M1Beast>()}:Array.Empty<M1Beast>()))
            {if(!enemy.Active)continue;Vector3 d=enemy.transform.position-position;d.y=0;if(d.magnitude<2.6f&&Vector3.Dot(forward,d.normalized)>.25f){enemy.Hit(28);Sound(2);Notify(L.K("t3ae4ad6ab0"));}}
        }
        private static Vector3 Position(Building b){var p=new Vector3(b.x*2,0,b.z*2);if(Catalog.Edge(b.type))p+=new Vector3(BuildGrid.DX[b.rotation],0,BuildGrid.DZ[b.rotation]);return p;}
        private static Quaternion Facing(Building b)=>Quaternion.Euler(0,b.rotation*90,0);
        private Task buildingSync;
        public async Task QuiesceAsync(){Deactivate();if(buildingSync!=null)try{await buildingSync;}catch(OperationCanceledException){} }
        private Task SyncBuildings(){if(buildingSync!=null&&!buildingSync.IsCompleted)return buildingSync;buildingSync=SyncBuildingsCore();return buildingSync;}
        private async Task SyncBuildingsCore()
        {
            if(syncing)return;
            var token=sceneLifetime.Token;
            syncing=true;int revision=C.Revision;
            try
            {
                foreach(var id in buildings.Keys.Where(id=>C.Find(id)==null).ToArray()){buildings[id].Dispose();buildings.Remove(id);}
                foreach(var b in C.Grid.Items.ToArray())
                {
                    token.ThrowIfCancellationRequested();
                    if(!buildings.TryGetValue(b.id,out var instance)){instance=await director.Global.Systems.Get<ResourceSystem>().RentAsync(director.Prefix+"Prefabs/"+b.type,objects,scope,32,token);token.ThrowIfCancellationRequested();buildings.Add(b.id,instance);}
                    var go=instance.Instance;go.transform.SetPositionAndRotation(Position(b),Facing(b));var built=go.GetComponent<M1Built>();built.Initialize(b,this);built.UpdateVisual();go.SetActive(true);
                    if(Catalog.Edge(b.type))go.GetComponent<M1Occluder>()?.Bind(actor.transform,cameraView);
                }
                foreach(var roof in roofs)roof.Dispose();roofs.Clear();
                var roofCells=new HashSet<(int,int)>();foreach(var b in C.Grid.Items.Where(b=>b.type==Structure.Floor))foreach(var cell in C.Grid.Room(b.x,b.z,out _))roofCells.Add(cell);
                foreach(var cell in roofCells){token.ThrowIfCancellationRequested();var roof=await director.Global.Systems.Get<ResourceSystem>().RentAsync(director.Prefix+"Prefabs/Roof",objects,scope,225,token);token.ThrowIfCancellationRequested();roofs.Add(roof);roof.Instance.transform.position=new Vector3(cell.Item1*2,0,cell.Item2*2);roof.Instance.GetComponent<M1Occluder>().Bind(actor.transform,cameraView);roof.Instance.SetActive(true);}
                displayedRevision=revision;
            }
            catch(ObjectDisposedException) when(token.IsCancellationRequested){throw new OperationCanceledException(token);}
            finally{syncing=false;}
        }
        public override void ToggleBuild()
        {
            if(building){CloseWindow();return;}if(!home){Notify(L.K("t100869655f"));return;}if(T.Active){Notify(L.K("tdb73d90a9a"));return;}
            Show("M1Build",new M1PanelModel{Title=L.K("t1160302d5d"),Body=()=>L.K("t1934127502")+Catalog.StructureNames[(int)selection]+"："+Catalog.Describe(Catalog.Recipe(selection))+"\n"+buildHint+"\n\n"+(buildAction==0?L.K("t3e67e9542c"):buildAction==1?L.K("t5b2004b257"):L.K("tf2fa124fd3")),Labels=Catalog.StructureNames.Take(director.IsM2?10:9).Concat(new[]{L.K("t65ad70ad2c"),L.K("t3ed305885c"),L.K("te08b33f8f3")}).ToArray(),Command=i=>{int count=director.IsM2?10:9;if(i<count){selection=(Structure)i;buildAction=0;movingId=0;director.Run(RefreshGhost());}else if(i<count+2){buildAction=i-count+1;movingId=0;ghost?.Dispose();ghost=null;}else CloseWindow();}});
            building=true;gridRoot.SetActive(true);selection=Structure.Floor;buildAction=0;rotation=0;rotateHeld=false;placeHeld=Mouse.current!=null&&Mouse.current.leftButton.isPressed;director.Run(RefreshGhost());
        }
        private async Task RefreshGhost()
        {
            ghost?.Dispose();ghost=null;
            var selected=selection;
            var lease=await director.Global.Systems.Get<ResourceSystem>().RentAsync(director.Prefix+"Prefabs/"+selected,objects,scope,32,sceneLifetime.Token);
            if(!building||selection!=selected){lease.Dispose();return;}
            ghost=lease;foreach(var obstacle in ghost.Instance.GetComponentsInChildren<NavMeshObstacle>())obstacle.enabled=false;foreach(var c in ghost.Instance.GetComponentsInChildren<Collider>())c.enabled=false;ghost.Instance.SetActive(true);
        }
        private void BuildInput()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null)return;
            bool rotate=director.IsM2?actor.Actions.FindAction("Player/Rotate").IsPressed():keyboard!=null&&keyboard.rKey.isPressed;if(rotate&&!rotateHeld)rotation=(rotation+1)%4;rotateHeld=rotate;
            bool click=mouse.leftButton.isPressed&&!placeHeld;placeHeld=mouse.leftButton.isPressed;
            var ray=cameraView.ScreenPointToRay(mouse.position.ReadValue());if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance))return;
            var point=ray.GetPoint(distance);gx=Mathf.RoundToInt(point.x/2);gz=Mathf.RoundToInt(point.z/2);
            buildHint=C.Grid.Validate(selection,gx,gz,rotation);
            if(buildHint==""&&!E.Storage.CanPay(Catalog.Recipe(selection)))buildHint=L.K("t72a0ba6af1");
            if(ghost!=null){var preview=new Building(0,selection,gx,gz,rotation);ghost.Instance.transform.SetPositionAndRotation(Position(preview),Facing(preview));foreach(var r in ghost.Instance.GetComponentsInChildren<Renderer>()){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",buildHint==""?new Color(.2f,.85f,.6f):new Color(.95f,.3f,.15f));r.SetPropertyBlock(block);}}
            if(!click||EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
            if(buildAction==0){TryBuild(selection,gx,gz,rotation);return;}
            if(buildAction==2&&movingId!=0){var target=C.Find(movingId);if(target!=null&&C.Grid.Move(target,gx,gz,rotation,out var error)){movingId=0;C.Changed();director.Run(SyncBuildings());}else Notify(L.K("t5c869007c8"));return;}
            M1Built hit=null;foreach(var h in Physics.RaycastAll(ray,200).OrderBy(h=>h.distance)){hit=h.collider.GetComponentInParent<M1Built>();if(hit!=null&&hit.Data!=null)break;}
            if(hit==null||hit.Data==null){Notify(L.K("t0ea01a7d74"));return;}
            if(buildAction==1){if(C.Grid.Remove(hit.Data,E.Storage,out var error)){C.Changed();director.Run(SyncBuildings());}else Notify(error);}
            else {if(hit.Data.Damaged||Catalog.Edge(hit.Data.type)||hit.Data.type==Structure.Floor||!string.IsNullOrEmpty(hit.Data.occupant)){Notify(L.K("t19bf540917"));return;}movingId=hit.Data.id;selection=hit.Data.type;Notify(L.K("t2652d94ad4"));}
        }
        public override void Sound(int cue){if(audioSource!=null&&cues!=null)audioSource.PlayOneShot(cues[Mathf.Clamp(cue,0,3)],.12f);}
        public bool TryBuild(Structure type,int x,int z,int direction)
        {
            if(!home||!building||T.Active){Notify(L.K("tb764843740"));return false;}
            if(type!=Structure.Floor&&Vector3.Distance(Position(new Building(0,type,x,z,direction)),actor.transform.position)<1.1f){Notify(L.K("tabf5199c52"));return false;}
            var placed=C.Grid.Place(type,x,z,direction,E.Storage,out var error);
            if(placed==null){Notify(error);return false;}C.Changed();Sound(1);director.Run(SyncBuildings());return true;
        }
        private static AudioClip Tone(int cue){int count=4410;var samples=new float[count];for(int i=0;i<count;i++)samples[i]=Mathf.Sin(i*(cue==3?330:cue==2?120:cue==1?660:440)*Mathf.PI*2/22050)*Mathf.Pow(1f-(float)i/count,2);var clip=AudioClip.Create("M1 cue "+cue,count,1,22050,false);clip.SetData(samples,0);return clip;}
        public override async void Footprint(Vector3 position,Vector3 forward)
        {
            if(footBusy||scope==null)return;footBusy=true;
            try{var lease=await director.Global.Systems.Get<ResourceSystem>().RentAsync(director.Prefix+"Prefabs/Footprint",objects,scope,128,sceneLifetime.Token);if(!active){lease.Dispose();return;}position+=Vector3.Cross(Vector3.up,forward)*((footSide++%2==0?1:-1)*.11f);if(Physics.Raycast(position+Vector3.up*.4f,Vector3.down,out var ground,2,~0,QueryTriggerInteraction.Ignore))position=ground.point;lease.Instance.transform.SetPositionAndRotation(position+Vector3.up*.006f,Quaternion.LookRotation(forward));lease.Instance.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;lease.Instance.SetActive(true);footprints.Enqueue(lease);if(footprints.Count>128)footprints.Dequeue().Dispose();}
            catch(OperationCanceledException){/* Scene-owned footprint request was cancelled on exit. */}
            catch(ObjectDisposedException){if(active)throw;}
            finally{footBusy=false;}
        }
    }
}
