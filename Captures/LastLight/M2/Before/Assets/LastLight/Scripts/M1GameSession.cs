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
    public sealed class M1GameSession : M1Session,IM1SessionUI
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
        public override bool Paused=>!active||director.Busy||WindowOpen||director.Global.Systems.Get<PauseSystem>().Paused||building||Time.frameCount<=suppressInputUntil;
        public override bool BuildingMode=>building;
        public override float ActiveTime=>V.ActiveTime;
        public override Inventory Backpack=>E.Backpack;
        public override StoryState Story=>N.State;
        public override M1Actor Actor=>actor;
        public override IM1SessionUI UI=>this;
        public override Material SignalMaterial=>signal;
        public override bool RaidActive=>T.Active;
        public override Vector3 RaidTarget=>raidTarget;
        public override int Pickups { get=>E.Pickups;set=>E.Pickups=value; }
        public override int RaidStrikes { get=>T.Strikes;set=>throw new InvalidOperationException("请通过 RecordRaidStrike 提交命中"); }
        public void Configure(bool isHome,M1Actor player,M1Camera camera,Camera view,Material cue,Transform dynamicRoot,Transform liang,Light heat,M1Node[] interactions,M1Beast[] enemies,GameObject grid)
        {home=isHome;actor=player;follow=camera;cameraView=view;signal=cue;objects=dynamicRoot;liangVisual=liang;stoveLight=heat;nodes=interactions;beasts=enemies;gridRoot=grid;}
        public void Bind(M1Director owner){director=owner;}
        public void ValidateConfiguration()
        {
            if(navigation==null||actor==null||follow==null||cameraView==null||signal==null||objects==null||nodes==null||nodes.Length==0||beasts==null)
                throw new InvalidOperationException(gameObject.scene.path+"/"+name+$": 缺少引用 actor={actor!=null}, follow={follow!=null}, camera={cameraView!=null}, signal={signal!=null}, objects={objects!=null}, nodes={nodes?.Length}, beasts={beasts!=null}");
            if(home&&(liangVisual==null||stoveLight==null))throw new InvalidOperationException(name+": 家园缺少梁或暖炉灯光");
            if(nodes.Any(n=>n==null)||nodes.Select(n=>n.StableId).Distinct().Count()!=nodes.Length)throw new InvalidOperationException(name+": 节点为空或稳定标识重复");
        }
        public async Task ActivateAsync()
        {
            if(active)return;scope=new ResourceScope();sceneLifetime=new CancellationTokenSource();
            repairPath=new NavMeshPath();navigationRepairId=0;nextRepairPath=0;
            navigation.BuildNavMesh();
            if(navigation.navMeshData==null)throw new InvalidOperationException("M1 场景导航生成失败");
            if(home){liangNavigation=liangVisual.GetComponent<M1Navigator>()??liangVisual.gameObject.AddComponent<M1Navigator>();liangNavigation.Initialize(this,1.4f);}
            follow.Configure(actor.transform);follow.Initialize(this);actor.Initialize(this);
            foreach(var node in nodes){node.SetReadyAt(E.NodeReady.TryGetValue(node.StableId,out var time)?time:0);node.Refresh(ActiveTime);}
            foreach(var beast in beasts)beast.Initialize(this,false,beast.transform.GetChild(0));
            audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=0;
            cues=new AudioClip[4];for(int i=0;i<cues.Length;i++)cues[i]=Tone(i);
            if(home)await SyncBuildings();
            hud=new M1PanelModel{Title="留灯地球  /  冬末",Body=HudText};
            await director.Global.UI.OpenAsync("M1HUD",hud);
            active=true;Notify(home?"WASD 移动 · E 交互 · B 建造 · Tab 背包 · J 日志":"旧工坊：工程件不需要战斗，沿外围可以绕行。");
        }
        public void Deactivate()
        {
            if(!active&&scope==null)return;
            active=false;sceneLifetime?.Cancel();liangNavigation?.Shutdown();if(raider!=null)raider.Instance.GetComponent<M1Navigator>()?.Shutdown();foreach(var enemy in beasts)if(enemy!=null)enemy.GetComponent<M1Navigator>()?.Shutdown();if(navigation!=null){navigation.RemoveData();if(navigation.navMeshData!=null){Destroy(navigation.navMeshData);navigation.navMeshData=null;}}CloseWindow();if(director!=null&&director.Global!=null)director.Global.UI.Close("M1HUD");
            if(director!=null&&director.Global.Session!=null){C.CancelRepair();C.LiangStatus=Story.liang?"等待下一处维修":"等待启动件";}
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
            bool sheltered=home&&V.Warm&&(Vector3.Distance(actor.transform.position,new Vector3(0,0,-9))<6||InHouse(actor.transform.position));
            V.Tick(dt,Story.stove,sheltered);
            if(resting){V.Rest(dt);repairTick-=dt;if(repairTick<=0){resting=false;Notify("休息结束。检查口粮与燃料，再安排下一步。");}}
            if(V.Health<=0||actor.transform.position.y< -6){director.Run(RescueAsync());return;}
            if(home)
            {
                stoveLight.enabled=Story.stove&&V.Warm;
                TickRepairs(dt);
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
            return $"生命 {V.Health:0}   饱食 {V.Hunger:0}   暴露 {V.Exposure:0}\n背包 {Backpack.UsedSlots}/12 格   {(home?"家园":"旧工坊")} · 主动时间 {ActiveTime/60:0.0} 分钟\n\n{N.Objective(C)}\n\n"+
                (T.Active?$"枝角兽袭击：仓区受击 {T.Strikes}/3\n":Story.raidFinished?$"重建缓冲：{Mathf.Max(0,T.BufferUntil-ActiveTime)/60:0.0} 分钟\n":"")+
                NavigationText()+"\n"+(target!=null?"[E] "+target.Label+"\n":"")+(ActiveTime<noticeUntil?notice:"")+"\n\nWASD 移动 · Shift 冲刺 · 左键锤击\nE 交互 · F 进食 · B 建造 · Tab 背包 · J 日志 · Esc 暂停";
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
                else if(!C.HasHouse) return "建造地块在终端北侧；材料先存入公共仓储。";
                else if(!Story.core)destination=nodes.First(n=>n.Kind==NodeKind.Travel);
                else if(!Story.liang||!Story.reward)destination=nodes.First(n=>n.Kind==NodeKind.Liang);
                else destination=nodes.First(n=>n.Kind==NodeKind.Terminal);
            }
            else destination=nodes.First(n=>n.Kind==(Story.core?NodeKind.Shortcut:NodeKind.Core));
            if(destination==null)return "安全拾荒点会补充；不需要打败敌人取得基础资源。";
            var d=destination.transform.position-actor.transform.position;d.y=0;float horizontal=Vector3.Dot(d,cameraView.transform.right),vertical=Vector3.Dot(d,cameraView.transform.forward);
            string arrow=Mathf.Abs(horizontal)>Mathf.Abs(vertical)?horizontal>0?"→":"←":vertical>0?"↑":"↓";
            return $"{arrow} {destination.Label.Split('：')[0]} · {d.magnitude:0} 米";
        }
        public override void Notify(string message){notice=message;noticeUntil=ActiveTime+9;}
        private M1Node NearestNode()=>nodes.Where(n=>n.Available(ActiveTime)).OrderBy(n=>(n.transform.position-actor.transform.position).sqrMagnitude).FirstOrDefault(n=>Vector3.Distance(n.transform.position,actor.transform.position)<2.7f);
        public override void InteractNearest()
        {
            if(Paused)return;
            var node=NearestNode();if(node!=null){node.Use(this);return;}
            if(home){var built=C.Grid.Items.OrderBy(b=>(Position(b)-actor.transform.position).sqrMagnitude).FirstOrDefault(b=>Vector3.Distance(Position(b),actor.transform.position)<3);
                if(built!=null){OpenFacility(built);return;}}
            Notify("靠近带提示的物件后按 E。");
        }
        public override void UseNode(M1Node node)
        {
            if(Paused||!nodes.Contains(node)||Vector3.Distance(node.transform.position,actor.transform.position)>2.8f)return;
            switch(node.Kind)
            {
                case NodeKind.Pickup:
                    if(E.Pickup(node.StableId,node.Resource,node.Amount,node.Respawn,ActiveTime)){node.SetReadyAt(E.NodeReady[node.StableId]);node.Refresh(ActiveTime);Notify(Catalog.ResourceNames[(int)node.Resource]+" +"+node.Amount);Sound(0);}else Notify("背包空间不足，回家存入公共仓储。");break;
                case NodeKind.Terminal:OpenTerminal();break;
                case NodeKind.Depot:OpenInventory();break;
                case NodeKind.EmergencyBench:OpenCrafting();break;
                case NodeKind.EmergencyStove:OpenStove();break;
                case NodeKind.Pod:OpenRest(null);break;
                case NodeKind.Liang:OpenLiang();break;
                case NodeKind.Core:
                    if(!Story.core){Story.core=true;Story.shortcut=true;N.Log("找到建筑机启动件。关键件永久保留，旧工坊回程捷径已开放。");Sound(1);}OpenText("一枚仍然温热的启动件","旧工坊的维护盒仍在供电。启动件没有被撤离者带走。\n\n你把它收进任务栏。它不占背包，倒下也不会丢失。出口旁的回程捷径已经打开。",()=>{});break;
                case NodeKind.Record:Story.readRecord=true;N.Log("撤离记录：建筑机被留下，等待下一位人类授权者。");OpenText("撤离记录  /  07","最后一批运输机离开后，维护村转入低功耗。\n\n“保留一盏灯。若仍有人回来，让他知道这里可以修好。”\n\n系统把你称为人类授权者，梁还在等待启动。",()=>{});break;
                case NodeKind.Travel:
                    if(home&&(!Story.stove||!C.HasHouse||!C.Grid.Items.Any(b=>b.type==Structure.Bed)||!C.Grid.Items.Any(b=>b.type==Structure.Storage))){Notify("先修好暖炉，搭出带床和仓储的小屋，再安排第一次远行。");break;}
                    if(T.Active){Notify("先处理家园袭击，或在暂停菜单请求救援。");break;}director.Run(director.TravelAsync(home?"Workshop":"Home"));break;
                case NodeKind.Shortcut:if(Story.shortcut)director.Run(director.TravelAsync("Home"));else Notify("找到启动件后，可以从这里回家。");break;
                case NodeKind.AidRoute:
                    if(!Story.aidRoute)Notify("旧排水道入口尚未获得标记。常规工坊路线始终开放。");
                    else if(E.Pickup(node.StableId,Resource.Scrap,6,180,ActiveTime)){node.SetReadyAt(E.NodeReady[node.StableId]);Notify("沿岑标出的低风险通道取得废金属 ×6。");}break;
            }
        }
        private void OpenText(string title,string body,Action after)=>Show("M1Window",new M1PanelModel{Title=title,Body=()=>body,Labels=new[]{"知道了"},Command=_=>{CloseWindow();after();}});
        private void Show(string id,M1PanelModel model)
        {
            CloseWindow();window=id;
            model.Closed=()=>{if(window==id){window=null;building=false;ghost?.Dispose();ghost=null;}};
            director.Run(director.Global.UI.OpenAsync(id,model));
        }
        public void CloseWindow(){var old=window;window=null;building=false;suppressInputUntil=Time.frameCount+1;if(gridRoot!=null)gridRoot.SetActive(false);ghost?.Dispose();ghost=null;if(old!=null&&director?.Global!=null)director.Global.UI.Close(old);}
        private string Stock()=>string.Join("\n",Enum.GetValues(typeof(Resource)).Cast<Resource>().Select(r=>$"{Catalog.ResourceNames[(int)r],-6}   随身 {Backpack[r],3}   /   公共仓储 {E.Storage[r],3}"));
        public void OpenInventory()
        {
            if(window=="M1Inventory"){CloseWindow();return;}
            bool deposit=true;
            Show("M1Inventory",new M1PanelModel{Title="背包与公共仓储",Body=()=>Stock()+$"\n\n当前：{(deposit?"存入":"取出")}单份资源。{(home?"家园可访问公共仓储":"外出时仅查看，回家后可存取")}\n关键件：{(Story.core?"梁的启动件（已保护）":"尚未找到")}",Labels=Catalog.ResourceNames.Concat(new[]{"切换存入 / 取出","全部存入","关闭"}).ToArray(),Command=i=>{if(i==10){CloseWindow();return;}if(i==8){deposit=!deposit;return;}if(!home){Notify("回家后才能存取公共仓储。");return;}if(i==9){Notify("存入 "+Backpack.Deposit(E.Storage)+" 单位物资。");return;}if(!(deposit?Backpack.Transfer(E.Storage,(Resource)i,1):E.Storage.Transfer(Backpack,(Resource)i,1)))Notify("数量不足或目标容量已满。");}});
        }
        public void OpenJournal()
        {
            if(window=="M1Journal"){CloseWindow();return;}
            Show("M1Journal",new M1PanelModel{Title="维护日志",Body=()=>N.Objective(C)+"\n\n梁："+C.LiangStatus+"\n承诺："+PromiseText()+"\n\n"+string.Join("\n\n",N.Journal)+"\n\n记录的是你的行动。30–45 分钟是内容目标，不是倒计时。",Labels=new[]{"关闭"},Command=_=>CloseWindow()});
        }
        public void OpenPause()=>Show("M1Pause",new M1PanelModel{Title="暂停",Body=()=>"世界与危险已暂停。\n\n本原型仅保留本次运行状态；重新开始会清空本轮进度。\n\n如果被地形卡住，可请求维护拖车送回家。救援按倒下规则结算损失。",Labels=new[]{"继续","请求救援","重新开始本轮"},Command=i=>{CloseWindow();if(i==1)director.Run(RescueAsync());if(i==2)Show("M1Window",new M1PanelModel{Title="重新开始？",Body=()=>"本轮建筑、库存和故事选择会重置。",Labels=new[]{"重新开始","取消"},Command=j=>{CloseWindow();if(j==0)director.Run(director.RestartAsync());}});}});
        private void OpenCrafting()=>Show("M1Window",new M1PanelModel{Title="应急手工台",Body=()=>Stock()+"\n\n木材 ×1 → 燃料 ×1\n废金属 ×2 → 维修零件 ×1\n手工入口不需要梁、电力或完好工作台。",Labels=new[]{"制作燃料","加工零件","关闭"},Command=i=>{if(i==2){CloseWindow();return;}Notify((i==0?E.CraftFuel():E.CraftParts())?"加工完成，产物已入公共仓储。":"材料不足或仓储已满。");}});
        private void OpenStove()=>Show("M1Window",new M1PanelModel{Title="应急暖炉",Body=()=>Story.stove?$"暖炉已修复。供暖剩余 {Mathf.Max(0,V.StoveUntil-ActiveTime):0} 秒\n\n添加燃料 ×1 可供暖 5 分钟。屋内与炉旁可以消除暴露并恢复生命。\n\n{Stock()}":"石料 ×2、废金属 ×2：修复暖炉。\n\n在安全拾荒带拾取，先存进公共仓储。修复后提供一次应急燃料与口粮。",Labels=new[]{"修复 / 添燃料","关闭"},Command=i=>{if(i==1){CloseWindow();return;}if(!Story.stove){if(!E.Storage.Exchange(Catalog.StoveRepair,new Cost(Resource.Fuel,1),new Cost(Resource.Ration,2))){Notify("需要石料 ×2、废金属 ×2，且公共仓储有空间。");return;}Story.stove=true;N.Log("应急暖炉修复。安全拾荒点会补充，饥饿与寒冷从现在开始缓慢消耗。");V.Fuel(E.Storage);Sound(1);}else Notify(V.Fuel(E.Storage)?"添入燃料，暖灯又亮了一阵。":"公共仓储没有燃料，木材可在手工台转换。");}});
        private void OpenLiang()=>Show("M1Window",new M1PanelModel{Title="梁  /  建筑修复",Body=()=>!Story.liang?"梁的镜头蒙着霜。\n“启动核心缺失。等待人类授权者。”\n\n前往旧工坊取回固定启动件，不需要打败枝角兽。":$"“先保证门能打开。其他的，我们慢慢修。”\n\n岗位：{C.LiangStatus}\n已完成维修：{C.LiangRepairs}\n领取四份零件后，岑会通过终端求援。\n\n维修从公共仓储消耗材料，停机时主角仍能维修。",Labels=new[]{"安装启动件 / 领取奖励","开关修复岗位","维修梁（零件 ×1）","关闭"},Command=i=>{if(i==3){CloseWindow();return;}if(i==0){if(!Story.liang){if(!Story.core){Notify("还缺旧工坊的启动件。");return;}Story.liang=true;C.LiangWorking=true;N.Log("梁已苏醒，建筑修复岗位可用。");Sound(1);}if(Story.ClaimReward(E.Storage)){Notify("领取维修零件 ×4。门闩加固和岑的求援会竞争这些零件。");N.Log("梁交付四份维修零件，终端收到求援信号。");}else if(!Story.reward)Notify("公共仓储已满，空出空间后领取。");}if(i==1&&Story.liang){C.LiangWorking=!C.LiangWorking;C.CancelRepair();C.LiangStatus=C.LiangWorking?"寻找受损建筑":"岗位已暂停";}if(i==2&&C.LiangBroken){if(E.Storage.Pay(new Cost(Resource.Parts,1))){C.LiangBroken=false;Notify("梁已恢复。");}else Notify("维修梁需要零件 ×1，应急手工台始终可用。");}}});
        private string PromiseText()=>Story.promise==0?"无":Story.promise==1?"待履约：访客床位 + 口粮 ×2":Story.promise==2?"已延期一次，准备完成后履约或撤回":Story.promise==3?"已履约，访客床已预留":"已明确撤回，取消本次合作";
        private void OpenTerminal()
        {
            if(!Story.awake){Story.awake=true;N.Log("维护终端：人类授权者，欢迎回来。沿暖灯修复家园。");OpenText("你醒来时，地球很安静","维护舱在你身后关闭。雪把旧路埋了一半，终端还有一点光。\n\n“人类授权者，欢迎回来。”\n\n先在附近捡取木材、石料和废金属。将材料存入公共仓储，再修复应急暖炉。",()=>{});return;}
            if(Story.liang&&Story.reward&&Story.choice==0){OpenChoice();return;}
            Show("M1Window",new M1PanelModel{Title="家园终端",Body=()=>N.Objective(C)+"\n\n"+PromiseText()+"\n\n门闩加固：维修零件 ×4（所有门抵抗冲撞）\n防御：可建木障延缓，也可用维修锤牵制。第一次袭击等你确认才开始。",Labels=new[]{"防御说明","加固门闩","我准备好了","履行床位承诺","延期一次","撤回承诺","阅读后果 / 收束","关闭"},Command=i=>{switch(i){case 0:Story.defenseRead=true;OpenText("枝角兽正在靠近","枝角兽会先低头，地面出现冲撞线，再向前冲刺。\n\n侧移可以避开冲撞；维修锤需要前摇与耐力。木障能延缓它，受损墙和门归零后会留下缺口。\n\n三次冲撞进入仓区即防御失败：每类库存最多损失 10%，且不超过 5 单位；最多损坏两台普通设备。修好即可恢复，关键进度不会失去。\n\n袭击中不能调整布局，可以正常维修。生存、生产与袭击在阅读时暂停。",()=>{});break;
                case 1:if(Story.reinforced)Notify("门闩已经加固。");else if(!C.Grid.Items.Any(b=>b.type==Structure.Door))Notify("先建造一扇门。");else if(E.Storage.Pay(Catalog.Reinforcement)){Story.reinforced=true;N.Log("门闩完成加固，门的冲撞损伤减轻。");Notify("加固完成。");}else Notify("门闩加固需要维修零件 ×4。");break;
                case 2:CloseWindow();director.Run(StartRaidAsync());break;
                case 3:var bed=C.Grid.Items.FirstOrDefault(b=>b.type==Structure.Bed&&!b.Damaged&&string.IsNullOrEmpty(b.occupant));if(Story.Fulfill(E.Storage,bed)){N.Log("已交付访客床位与口粮 ×2，获得木材 ×8、废金属 ×4。");Notify("岑送来约定物资，访客床位已预留。");C.Changed();}else Notify("需待履约承诺、未占用完好访客床、口粮 ×2 和奖励存放空间。");break;
                case 4:Notify(Story.ExtendPromise()?"已延期一次。准备好后仍可履约。":"当前不能再延期。");break;
                case 5:Notify(Story.WithdrawPromise()?"已明确撤回承诺，本次合作取消。主线仍可继续。":"没有可撤回的承诺。");break;
                case 6:ShowConsequences();break;
                case 7:CloseWindow();break;}}});
        }
        private void OpenChoice()=>Show("M1Event",new M1PanelModel{Title="借你一盏灯",Body=()=>"岑：“你那边的灯亮了。我的同行者倒在旧井边。我需要两份维修零件。放在围栏外就好，不必开门。”\n\n梁：“门闩还差四份零件。援助会推迟加固。木障仍能争取时间。”\n\n援助：零件 ×2，开放额外低风险搜集通道。\n交换：零件 ×1，承诺一张访客床与口粮 ×2；履约才收到搬运收益。\n拒绝：不交物资，可以保留零件加固门闩。\n\n三条路线都能继续，回程捷径不受选择影响。\n公共仓储零件："+E.Storage[Resource.Parts],Labels=new[]{"援助：给两份零件","交换：一份并承诺床位","拒绝：留下守住这里","稍后回答"},Command=i=>{if(i==3){CloseWindow();return;}if(!Story.Choose(i+1,E.Storage)){Notify("对应零件不足，先回手工台加工；也可以稍后回答。");return;}N.Log(i==0?"援助了岑，旧排水道低风险搜集通道已标记。":i==1?"约定一份零件换搬运，承诺访客床位与口粮。":"拒绝了本次求援。岑离开，基本路线仍然开放。");CloseWindow();Sound(1);}});
        private void ShowConsequences()
        {
            if(!Story.raidFinished){Notify("先完成教学防御，再查看来访后果。");return;}
            string outcome=Story.choice==1?"岑：“他站起来了。旧排水道是安全的，我替你标在路牌上。”\n\n低风险搜集通道已开放，你的援助有了回应。":Story.choice==2?"岑看向屋内的床位。\n\n"+PromiseText()+"。她记住了你实际完成的安排。":"岑没有再请求开门。你留下了零件，选择先守住这里。\n\n她的离开没有带来报复。以后仍可能遇见普通交易。";
            Story.consequence=true;N.Log(outcome);
            bool recovery=!C.Grid.Items.Any(b=>b.Damaged)&&!C.LiangBroken&&Story.promise!=1&&Story.promise!=2;
            if(!recovery){OpenText("来访之后",outcome+"\n\n"+N.Objective(C),()=>{});return;}
            Story.completed=true;
            Show("M1End",new M1PanelModel{Title="冬末  /  灯留在这里",Body=()=>outcome+$"\n\n外出、建设、选择与一次危险已经完成。\n主动时间：{ActiveTime/60:0.0} 分钟\n本轮墙钟时间：{(Time.realtimeSinceStartupAsDouble-director.StartedWallTime)/60:0.0} 分钟\n建筑：{C.Grid.Items.Count} 处 · 梁维修：{C.LiangRepairs} 次 · 救援：{V.Rescues} 次\n\n这里是 M1 原型的终点。下一阶段才进入春季与温室。",Labels=new[]{"继续整理家园","重新开始本轮"},Command=i=>{CloseWindow();if(i==1)director.Run(director.RestartAsync());}});
        }
        public override void Eat(){Notify(V.Eat(Backpack)?"吃下一份口粮。":"饱食已满或随身没有口粮。");}
        private void OpenRest(Building bed)=>Show("M1Window",new M1PanelModel{Title="短暂休息",Body=()=>"休息 20 秒主动时间，加快生命与暴露恢复。不会跳过未处理的袭击。\n\n"+(bed!=null?"第一次使用会将此床分配给主角；访客承诺需要另一张床。":"维护舱始终提供基本休息。"),Labels=new[]{"休息","关闭"},Command=i=>{CloseWindow();if(i==1)return;if(T.Active||Story.choice>0&&!Story.raidFinished){Notify("先处理已预告的威胁，不能用睡眠跳过袭击。");return;}if(bed!=null){if(bed.Damaged||bed.occupant=="visitor"){Notify("这张床暂时不能使用。");return;}foreach(var b in C.Grid.Items)if(b.occupant=="player")b.occupant="";bed.occupant="player";}resting=true;repairTick=20;Notify("休息中，20 秒后恢复活动。");}});
        private void OpenFacility(Building b)
        {
            if(b.Damaged){Show("M1Window",new M1PanelModel{Title="维修 "+Catalog.StructureNames[(int)b.type],Body=()=>"生命 "+b.health+"/100\n需要 "+Catalog.Describe(Catalog.Repair(b.type))+"\n维修需要 8 秒主动时间，靠近目标；完成时一次扣料。",Labels=new[]{"开始维修","关闭"},Command=i=>{CloseWindow();if(i==0){C.CancelRepair();repairByLiang=false;Notify(C.BeginRepair(b,E.Storage)?"正在维修，保持在目标附近。":"材料不足。");}}});return;}
            switch(b.type){case Structure.Storage:OpenInventory();break;case Structure.Workbench:OpenCrafting();break;case Structure.Stove:OpenStove();break;case Structure.Bed:OpenRest(b);break;default:Notify("完好建筑。按 B 可调整布局；受损后按 E 维修。");break;}
        }
        private void TickRepairs(float dt)
        {
            if(C.RepairTarget!=0)
            {
                var target=C.Find(C.RepairTarget);if(target==null){C.CancelRepair();return;}
                bool reachable;
                if(repairByLiang){if(!C.LiangWorking||C.LiangBroken){C.CancelRepair();return;}reachable=WalkLiang(Position(target),dt);}
                else reachable=Vector3.Distance(actor.transform.position,Position(target))<3;
                if(C.TickRepair(dt,E.Storage,reachable,repairByLiang)){Notify(repairByLiang?"梁完成了一处维修。":"维修完成。");Sound(1);}
                else if(!repairByLiang)C.LiangStatus="等待主角完成维修";else if(reachable)C.LiangStatus=$"正在维修 {C.RepairProgress:0}/8 秒";
                return;
            }
            liangNavigation?.Stop();
            if(!Story.liang){C.LiangStatus="等待启动件";return;}
            if(C.LiangBroken){C.LiangStatus="待维修：零件 ×1";return;}
            if(!C.LiangWorking){C.LiangStatus="岗位已暂停";return;}
            var damaged=C.Grid.Items.FirstOrDefault(b=>b.Damaged);
            if(damaged==null){C.LiangStatus="巡检完成，等待受损建筑";return;}
            if(!C.BeginRepair(damaged,E.Storage)){C.LiangStatus="缺少材料："+Catalog.Describe(Catalog.Repair(damaged.type));return;}
            repairByLiang=true;
        }
        private bool WalkLiang(Vector3 target,float dt)
        {
            if(liangNavigation==null||!liangNavigation.Available){C.LiangStatus="导航不可用，等待恢复";return false;}
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
                if(float.IsPositiveInfinity(best)){liangNavigation.Stop();C.LiangStatus="通路阻挡，等待清理";navigationRepairId=0;return false;}
            }
            if(Vector3.Distance(liangVisual.position,repairDestination)<.3f){liangNavigation.Stop();return true;}
            liangNavigation.Go(repairDestination);C.LiangStatus="前往维修点";return false;
        }

        public async Task StartRaidAsync()
        {
            bool counter=C.Grid.Items.Any(b=>b.type==Structure.Barricade&&!b.Damaged)||Story.reinforced;
            if(!home||!Story.liang||!C.HasHouse||!counter||!Story.defenseRead||Story.choice==0){Notify("需完成家园与梁、回应岑、读防御说明，并建好至少一道木障或加固门闩。");return;}
            if(T.Active||Story.raidFinished)return;
            var incoming=await director.Global.Systems.Get<ResourceSystem>().RentAsync("LastLight/M1/Prefabs/Raider",objects,scope,1,sceneLifetime.Token);
            if(!T.Start(ActiveTime,Story,true)){incoming.Dispose();return;}raider=incoming;
            var go=raider.Instance;go.transform.position=new Vector3(0,0,-24);go.SetActive(true);go.GetComponent<M1Beast>().Initialize(this,true,go.transform.GetChild(0));
            N.Log("你确认准备，枝角兽进入家园。阅读暂停世界，袭击中可维修但不能改布局。");Notify("枝角兽来了！侧移避开预警，维修锤牵制它。");Sound(3);
        }
        private void FinishRaid(bool failed)
        {
            if(!T.Finish(failed,ActiveTime,Story,E.Storage))return;
            if(failed){foreach(var b in C.Grid.Items.Where(b=>b.type==Structure.Workbench||b.type==Structure.Storage||b.type==Structure.Stove).Take(2)){if(T.RecordEquipment(b.id)){b.health=0;C.Changed();}}}
            raider?.Dispose();raider=null;
            N.Log(failed?"防御失败：损失按上限结算一次，维护系统保住了关键成果。进入两游戏日重建缓冲。":"枝角兽退去。防御成功，进入两游戏日重建缓冲。");Notify(failed?"枝角兽带走少量普通物资后离开。维修设备即可恢复。":"枝角兽退却了。检查建筑，与终端查看后果。");
        }
        private async Task RescueAsync(){if(T.Active)FinishRaid(true);V.Rescue(Backpack);C.CancelRepair();resting=false;N.Log("维护拖车将你送回家。随身普通资源损失 20%，关键进度保留。");if(!home)await director.TravelAsync("Home");else actor.Teleport(new Vector3(0,0,-14));Notify("救援完成：生命与基本饱食恢复，安全拾荒带始终可用。");}
        public override void TakeDamage(float damage){if(Paused)return;V.Damage(damage);Sound(2);Notify("受到冲撞，侧移避开下一次预警。");}
        public override bool TryRecordDamage(int buildingId)=>T.RecordEquipment(buildingId);
        public override void DamageBuilding(int id,float amount){if(Story.reinforced&&C.Find(id)?.type==Structure.Door)amount*=.45f;if(C.Damage(id,amount,T)&&buildings.TryGetValue(id,out var lease))lease.Instance.GetComponent<M1Built>().UpdateVisual();}
        public override void RecordRaidStrike()=>T.Strike();
        public override void Melee(Vector3 position,Vector3 forward)
        {
            foreach(var enemy in beasts.Concat(raider!=null?new[]{raider.Instance.GetComponent<M1Beast>()}:Array.Empty<M1Beast>()))
            {if(!enemy.Active)continue;Vector3 d=enemy.transform.position-position;d.y=0;if(d.magnitude<2.6f&&Vector3.Dot(forward,d.normalized)>.25f){enemy.Hit(28);Sound(2);Notify("维修锤命中。");}}
        }
        private static Vector3 Position(Building b){var p=new Vector3(b.x*2,0,b.z*2);if(Catalog.Edge(b.type))p+=new Vector3(BuildGrid.DX[b.rotation],0,BuildGrid.DZ[b.rotation]);return p;}
        private static Quaternion Facing(Building b)=>Quaternion.Euler(0,b.rotation*90,0);
        private async Task SyncBuildings()
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
                    if(!buildings.TryGetValue(b.id,out var instance)){instance=await director.Global.Systems.Get<ResourceSystem>().RentAsync("LastLight/M1/Prefabs/"+b.type,objects,scope,32,token);token.ThrowIfCancellationRequested();buildings.Add(b.id,instance);}
                    var go=instance.Instance;go.transform.SetPositionAndRotation(Position(b),Facing(b));var built=go.GetComponent<M1Built>();built.Initialize(b,this);built.UpdateVisual();go.SetActive(true);
                    if(Catalog.Edge(b.type))go.GetComponent<M1Occluder>()?.Bind(actor.transform,cameraView);
                }
                foreach(var roof in roofs)roof.Dispose();roofs.Clear();
                var roofCells=new HashSet<(int,int)>();foreach(var b in C.Grid.Items.Where(b=>b.type==Structure.Floor))foreach(var cell in C.Grid.Room(b.x,b.z,out _))roofCells.Add(cell);
                foreach(var cell in roofCells){var roof=await director.Global.Systems.Get<ResourceSystem>().RentAsync("LastLight/M1/Prefabs/Roof",objects,scope,225,token);token.ThrowIfCancellationRequested();roofs.Add(roof);roof.Instance.transform.position=new Vector3(cell.Item1*2,0,cell.Item2*2);roof.Instance.GetComponent<M1Occluder>().Bind(actor.transform,cameraView);roof.Instance.SetActive(true);}
                displayedRevision=revision;
            }
            finally{syncing=false;}
        }
        public override void ToggleBuild()
        {
            if(building){CloseWindow();return;}if(!home){Notify("只能在家园标线内建造。");return;}if(T.Active){Notify("袭击中禁止布局编辑，靠近受损建筑按 E 可维修。");return;}
            Show("M1Build",new M1PanelModel{Title="家园建造",Body=()=>"2 米网格 · 材料来自公共仓储\n左键放置 / 选择，R 旋转，Esc 退出\n方向键平移，滚轮缩放\n\n"+Catalog.StructureNames[(int)selection]+"："+Catalog.Describe(Catalog.Recipe(selection))+"\n"+buildHint+"\n\n"+(buildAction==0?"模式：放置":buildAction==1?"模式：拆除，完好返还材料":"模式：移动，先选设施再选位置"),Labels=Catalog.StructureNames.Concat(new[]{"拆除模式","移动模式","退出建造"}).ToArray(),Command=i=>{if(i<9){selection=(Structure)i;buildAction=0;movingId=0;director.Run(RefreshGhost());}else if(i<11){buildAction=i-8;movingId=0;ghost?.Dispose();ghost=null;}else CloseWindow();}});
            building=true;gridRoot.SetActive(true);selection=Structure.Floor;buildAction=0;rotation=0;rotateHeld=false;placeHeld=Mouse.current!=null&&Mouse.current.leftButton.isPressed;director.Run(RefreshGhost());
        }
        private async Task RefreshGhost()
        {
            ghost?.Dispose();ghost=null;
            var selected=selection;
            var lease=await director.Global.Systems.Get<ResourceSystem>().RentAsync("LastLight/M1/Prefabs/"+selected,objects,scope,32,sceneLifetime.Token);
            if(!building||selection!=selected){lease.Dispose();return;}
            ghost=lease;foreach(var obstacle in ghost.Instance.GetComponentsInChildren<NavMeshObstacle>())obstacle.enabled=false;foreach(var c in ghost.Instance.GetComponentsInChildren<Collider>())c.enabled=false;ghost.Instance.SetActive(true);
        }
        private void BuildInput()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null)return;
            bool rotate=keyboard!=null&&keyboard.rKey.isPressed;if(rotate&&!rotateHeld)rotation=(rotation+1)%4;rotateHeld=rotate;
            bool click=mouse.leftButton.isPressed&&!placeHeld;placeHeld=mouse.leftButton.isPressed;
            var ray=cameraView.ScreenPointToRay(mouse.position.ReadValue());if(!new Plane(Vector3.up,Vector3.zero).Raycast(ray,out var distance))return;
            var point=ray.GetPoint(distance);gx=Mathf.RoundToInt(point.x/2);gz=Mathf.RoundToInt(point.z/2);
            buildHint=C.Grid.Validate(selection,gx,gz,rotation);
            if(buildHint==""&&!E.Storage.CanPay(Catalog.Recipe(selection)))buildHint="公共仓储材料不足";
            if(ghost!=null){var preview=new Building(0,selection,gx,gz,rotation);ghost.Instance.transform.SetPositionAndRotation(Position(preview),Facing(preview));foreach(var r in ghost.Instance.GetComponentsInChildren<Renderer>()){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",buildHint==""?new Color(.2f,.85f,.6f):new Color(.95f,.3f,.15f));r.SetPropertyBlock(block);}}
            if(!click||EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
            if(buildAction==0){TryBuild(selection,gx,gz,rotation);return;}
            if(buildAction==2&&movingId!=0){var target=C.Find(movingId);if(target!=null&&C.Grid.Move(target,gx,gz,rotation,out var error)){movingId=0;C.Changed();director.Run(SyncBuildings());}else Notify("此位置不能移动到达，检查占地与通路。");return;}
            M1Built hit=null;foreach(var h in Physics.RaycastAll(ray,200).OrderBy(h=>h.distance)){hit=h.collider.GetComponentInParent<M1Built>();if(hit!=null&&hit.Data!=null)break;}
            if(hit==null||hit.Data==null){Notify("请选择一处建筑。");return;}
            if(buildAction==1){if(C.Grid.Remove(hit.Data,E.Storage,out var error)){C.Changed();director.Run(SyncBuildings());}else Notify(error);}
            else {if(hit.Data.Damaged||Catalog.Edge(hit.Data.type)||hit.Data.type==Structure.Floor||!string.IsNullOrEmpty(hit.Data.occupant)){Notify("仅可移动未占用的完好设施。");return;}movingId=hit.Data.id;selection=hit.Data.type;Notify("已选择设施，点击新的网格位置。");}
        }
        public override void Sound(int cue){if(audioSource!=null&&cues!=null)audioSource.PlayOneShot(cues[Mathf.Clamp(cue,0,3)],.12f);}
        public bool TryBuild(Structure type,int x,int z,int direction)
        {
            if(!home||!building||T.Active){Notify("进入家园建造模式后才能放置。");return false;}
            if(type!=Structure.Floor&&Vector3.Distance(Position(new Building(0,type,x,z,direction)),actor.transform.position)<1.1f){Notify("这里站着主角，请先移开。");return false;}
            var placed=C.Grid.Place(type,x,z,direction,E.Storage,out var error);
            if(placed==null){Notify(error);return false;}C.Changed();Sound(1);director.Run(SyncBuildings());return true;
        }
        private static AudioClip Tone(int cue){int count=4410;var samples=new float[count];for(int i=0;i<count;i++)samples[i]=Mathf.Sin(i*(cue==3?330:cue==2?120:cue==1?660:440)*Mathf.PI*2/22050)*Mathf.Pow(1f-(float)i/count,2);var clip=AudioClip.Create("M1 cue "+cue,count,1,22050,false);clip.SetData(samples,0);return clip;}
        public override async void Footprint(Vector3 position,Vector3 forward)
        {
            if(footBusy||scope==null)return;footBusy=true;
            try{var lease=await director.Global.Systems.Get<ResourceSystem>().RentAsync("LastLight/M1/Prefabs/Footprint",objects,scope,128,sceneLifetime.Token);if(!active){lease.Dispose();return;}position+=Vector3.Cross(Vector3.up,forward)*((footSide++%2==0?1:-1)*.11f);if(Physics.Raycast(position+Vector3.up*.4f,Vector3.down,out var ground,2,~0,QueryTriggerInteraction.Ignore))position=ground.point;lease.Instance.transform.SetPositionAndRotation(position+Vector3.up*.006f,Quaternion.LookRotation(forward));lease.Instance.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;lease.Instance.SetActive(true);footprints.Enqueue(lease);if(footprints.Count>128)footprints.Dequeue().Dispose();}
            catch(OperationCanceledException){/* Scene-owned footprint request was cancelled on exit. */}
            catch(ObjectDisposedException){if(active)throw;}
            finally{footBusy=false;}
        }
    }
}
