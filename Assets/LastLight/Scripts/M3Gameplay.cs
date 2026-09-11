using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace LastLight
{
    public sealed partial class M1GameSession
    {
        public bool M3Enabled=>director!=null&&director.IsM3;
        private M3WorldSystem W3=>director.M3;
        private M3State S3=>W3.Data;
        private float pulseAt,pulseFireAt=-1;private Vector3 pulseDirection;
        private M3Worker[] m3Workers;
        private readonly bool[] m3Access={false,false,false};
        private M3Season shownSeason=(M3Season)(-1);
        private string M3Objective()
        {
            if(!S3.robots[1].awake)return M3Text.Pair("第二章：到温室取得三种种源，修复园艺机芽。","Chapter II: collect all three crop samples and repair Ya in the greenhouse.");
            if(!S3.relayRoad)return M3Text.Pair("第二章：带回绝缘样本，修复通往中继站的线路；旧工坊还有发射结构资料。","Chapter II: recover insulation, repair relay access, and find launch plans in the workshop.");
            if(!S3.emergencyPower||!S3.robots[2].awake)return M3Text.Pair("第三章：中继站手动供电，修复守。维修守只需零件。","Chapter III: start emergency power at the relay and repair Shou using parts.");
            if(!S3.navigation)return M3Text.Pair("第三章：阅读温室与中继站的两份身份记录，完成结构、动力、导航工程。","Chapter III: read both identity archives and complete structure, power and navigation.");
            return M3Text.Pair("第四章：在工程控制台查看三条未来道路，完成请求、答复与准备。","Chapter IV: review all three futures at the engineering console and settle requests, replies and preparations.");
        }
        private void TickM3(float dt)
        {
            W3.Reconcile(C,Story);
            if(home)
            {
                if(m3Workers==null)m3Workers=gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<M3Worker>(true)).ToArray();
                m3Access[0]=!C.LiangBroken&&liangNavigation!=null&&liangNavigation.Available;
                foreach(var worker in m3Workers)m3Access[worker.Index]=worker.Tick(this,S3.robots[worker.Index],C.Grid.Items,dt);
                C.LiangWorking=S3.robots[0].enabled&&S3.robots[0].job==M3Job.Repair&&!C.LiangBroken;
            }
            W3.Advance(dt,C,director.World,E.Storage,home?m3Access:new[]{true,true,true},T.Active||director.World.Data.warning);
            var season=M3Rules.Season(director.World.Data.springTime);
            if(director.World.Data.spring)
            {
                bool heat=season==M3Season.Summer,cold=season==M3Season.Winter;
                bool protectedHome=home&&InHouse(actor.transform.position)&&M3Rules.Usable(C.Grid.Items,Structure.Stove)&&(heat?S3.generatorSeconds>0:V.Warm);
                V.ApplyM3Exposure(dt,heat||cold,protectedHome,S3.clothing);
                if(season!=shownSeason){shownSeason=season;ApplyM3Season(season);}
            }
            if(S3.pulse&&actor.Pressed("Pulse")&&Time.time>=pulseAt&&(UnityEngine.EventSystems.EventSystem.current==null||!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
            {
                if(!Backpack.Pay(new Cost(Resource.Cell,1))){Notify(M3Text.Pair("背包缺少电芯。","Carry an energy cell to fire."));return;}
                var ray=cameraView.ScreenPointToRay(Mouse.current.position.ReadValue());var plane=new Plane(Vector3.up,actor.transform.position);
                pulseDirection=plane.Raycast(ray,out var distance)?ray.GetPoint(distance)-actor.transform.position:actor.transform.forward;pulseDirection.y=0;pulseDirection.Normalize();
                pulseFireAt=Time.time+.3f;pulseAt=Time.time+1.2f;actor.WorkFeedback();
            }
            if(pulseFireAt>=0&&Time.time>=pulseFireAt){pulseFireAt=-1;FirePulse();}
        }
        private void FirePulse()
        {
            var start=actor.transform.position+Vector3.up*.75f;var end=start+pulseDirection*14;
            foreach(var hit in Physics.RaycastAll(start,pulseDirection,14,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {
                if(hit.collider.GetComponentInParent<M1Actor>()!=null)continue;
                var enemy=hit.collider.GetComponentInParent<M1Beast>();if(enemy!=null)enemy.Hit(45);end=hit.point;break;
            }
            var go=new GameObject("Pulse impact");go.transform.SetParent(objects,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=signal;line.positionCount=2;line.SetPositions(new[]{start,end});line.widthMultiplier=.08f;Destroy(go,.15f);Sound(2);
        }
        private void ApplyM3Season(M3Season season)
        {
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var visual in root.GetComponentsInChildren<M3SeasonVisual>(true))visual.Apply(season);
        }
        private void OpenM3Node(M1Node node)
        {
            switch(node.Amount)
            {
                case 0:OpenM3Hub();break;
                case 1:OpenM3Actions("wake-ya","ya-request");break;
                case 2:OpenM3Actions("insulation","relay-road");break;
                case 3:if(S3.relayRoad&&!T.Active){CloseWindow();director.Run(director.TravelAsync("Relay"));}else Notify(M3Text.Pair("先修复中继站通路。","Repair relay access first."));break;
                case 4:OpenM3Actions("emergency-power","wake-shou","shou-request");break;
                case 5:OpenM3Actions("navigation-record","maker-record");break;
                case 6:OpenM3Engineering();break;
                case 7:OpenM3Actions("structure-plan");break;
                case 8:S3.duMet=true;OpenM3Traveler();break;
                case 9:OpenM3Cen();break;
                case 10:OpenM3Robots();break;
            }
        }
        public void OpenM3Hub()
        {
            Present(new M1PanelModel{Title=M3Text.Pair("家园与共同工程","Home and shared engineering"),Body=()=>M3Objective()+"\n\n"+M3Text.Season(M3Rules.Season(director.World.Data.springTime))+" · "+M3Text.Pair("下一季：","Next season: ")+M3Text.Season((M3Season)(((int)M3Rules.Season(director.World.Data.springTime)+1)%4))+"\n"+M3Text.Pair("春备口粮，夏备散热，秋储燃料，冬护作物。","Spring: food. Summer: cooling. Autumn: fuel reserves. Winter: protected crops."),
                Labels=new[]{M3Text.Pair("伙伴与能源","Partners and energy"),M3Text.Pair("共同工程","Shared engineering"),M3Text.Pair("制作与补给","Craft and supplies"),M3Text.Pair("人物与承诺","People and promises"),M3Text.Pair("三条未来道路","Three futures"),M3Text.Pair("日志与记录","Journal and records"),M3Text.Pair("关闭","Close")},Command=i=>{if(i==0)OpenM3Robots();if(i==1)OpenM3Engineering();if(i==2)OpenM3Craft();if(i==3)OpenM3Traveler();if(i==4)OpenM3Endings();if(i==5)OpenM3Journal();if(i==6)CloseWindow();}});
        }
        private void OpenM3Actions(params string[] ids)
        {
            var actions=ids.Select(id=>M3Progression.Actions.First(a=>a.Id==id)).ToArray();
            Present(new M1PanelModel{Title=M3Text.Pair("准备与交付","Preparation and delivery"),Body=()=>string.Join("\n\n",actions.Select(a=>a.Name+"\n"+Catalog.Describe(a.Costs)+"\n"+M3Progression.Missing(a.Id,S3,C,director.World.Data))),Labels=actions.Select(a=>a.Name).Concat(new[]{M3Text.Pair("返回","Back")}).ToArray(),Command=i=>{
                if(i==actions.Length){OpenM3Hub();return;}
                if(M3Progression.Execute(actions[i].Id,S3,C,director.World.Data,E.Storage,out var reason)){N.Log(actions[i].Name);actor.WorkFeedback();director.RequestAutoSave();ShowM3Story(actions[i].Id);}else Notify(reason);
            }});
        }
        private void OpenM3Engineering()=>OpenM3Actions("structure","power","navigation","personal-ship","transport","collective-power","supplies");
        private void OpenM3Craft()
        {
            Present(new M1PanelModel{Title=M3Text.Pair("制作与应急能源","Crafting and emergency energy"),Body=()=>Stock()+"\n"+M3Text.Pair("燃料：木材×1；零件：废金属×2。电芯解锁于中继站应急供电，需完好工作台。","Fuel: 1 wood. Parts: 2 scrap. Cells unlock with relay emergency power and require a working bench."),Labels=new[]{M3Text.Pair("燃料","Fuel"),M3Text.Pair("零件","Parts"),M3Text.Pair("电芯：金属2、零件1","Cell: 2 scrap, 1 part"),M3Text.Pair("发电：燃料1","Generator: 1 fuel"),M3Text.Pair("装备制作","Equipment"),M3Text.Pair("返回","Back")},Command=i=>{
                if(i==5){OpenM3Hub();return;}if(i==4){OpenM3Actions("pulse","clothing");return;}
                if(!home){Notify(M3Text.Pair("请回家使用公共仓储。","Return home to use public storage."));return;}
                bool ok=i==0?E.CraftFuel():i==1?E.CraftParts():i==2?S3.emergencyPower&&M3Rules.Usable(C.Grid.Items,Structure.Workbench)&&E.Storage.Exchange(new[]{new Cost(Resource.Scrap,2),new Cost(Resource.Parts,1)},new Cost(Resource.Cell,1)):M3Rules.Usable(C.Grid.Items,Structure.Charger)&&W3.Fuel(E.Storage);
                Notify(ok?M3Text.Pair("已完成。","Completed."):M3Text.Pair("缺少材料、容量、解锁或完好设施。","Missing materials, capacity, unlock or working facility."));}});
        }
        private void OpenM3Robots()
        {
            W3.Reconcile(C,Story);
            Present(new M1PanelModel{Title=M3Text.Pair("岗位与能源","Jobs and energy"),Body=()=>M3Text.Pair("应急供电只维持梁的修复。充电座消耗燃料支持生产。","Emergency supply supports Liang's repairs only. A fueled charging station supports production.")+"\n"+Mathf.CeilToInt(S3.generatorSeconds)+" s\n"+string.Join("\n",Enumerable.Range(0,3).Select(i=>M3Text.Robot(i)+" · "+M3Text.Job(S3.robots[i].job)+" · "+M3Text.Stop(W3.Status[i])))+"\n"+M3Text.Pair("供电优先级：","Power priority: ")+string.Join(" > ",S3.priority.Select(M3Text.Robot)),Labels=new[]{M3Text.Robot(0),M3Text.Robot(1),M3Text.Robot(2),M3Text.Pair("轮换供电优先级","Rotate power priority"),M3Text.Pair("添加燃料","Add fuel"),M3Text.Pair("返回","Back")},Command=i=>{if(i<3)OpenM3Robot(i);if(i==3)S3.priority=S3.priority.Skip(1).Concat(S3.priority.Take(1)).ToArray();if(i==4)OpenM3Craft();if(i==5)OpenM3Hub();}});
        }
        private void OpenM3Robot(int index)
        {
            var robot=S3.robots[index];
            Present(new M1PanelModel{Title=M3Text.Robot(index),Body=()=>M3Text.Job(robot.job)+"\n"+M3Text.Stop(W3.Status[index])+"\n"+M3Text.Pair("切换岗位取消未完成周期，已完成产出保留。协商需要完成人物请求；留守与同行都需要明确答复。","Changing jobs cancels the unfinished cycle but keeps completed output. Complete the personal request before agreement; staying or travelling requires an explicit reply."),Labels=new[]{M3Text.Job((M3Job)(index*2)),M3Text.Job((M3Job)(index*2+1)),M3Text.Pair("开关岗位","Enable / disable"),M3Text.Pair("维修：零件1","Repair: 1 part"),M3Text.Pair("共同规则协商","Agree on common rules"),M3Text.Pair("请求同行","Ask to travel together"),M3Text.Pair("询问留守","Ask to remain"),M3Text.Pair("告知我的远航","Inform of my departure"),M3Text.Pair("人物请求","Personal request"),M3Text.Pair("返回","Back")},Command=i=>{
                if(i==9){OpenM3Robots();return;}if(!robot.awake){Notify(M3Text.Pair("尚未唤醒。","Not awake yet."));return;}
                if(i<2)W3.Switch(index,(M3Job)(index*2+i),C);
                if(i==2){robot.enabled=!robot.enabled;if(index==0){C.CancelRepair();C.LiangWorking=robot.enabled&&robot.job==M3Job.Repair;}}
                if(i==3)Notify(W3.RepairRobot(index,E.Storage,C)?M3Text.Pair("修复完成。","Repaired."):M3Text.Pair("无需维修或缺少零件。","No repair needed, or missing parts."));
                if(i==4){if(robot.requestDone){robot.agreed=true;ShowM3Story("agreement-"+robot.id);}else Notify(M3Text.Pair("先完成其公开请求，再重新协商。","Complete their visible request, then negotiate again."));}
                if(i==5){robot.answer=robot.agreed?1:2;ShowM3Story(robot.agreed?"consent":"refuse");}
                if(i==6){robot.answer=2;ShowM3Story("stay");}
                if(i==7){robot.informed=true;ShowM3Story("informed");}
                if(i==8)OpenM3Actions(new[]{"liang-request","ya-request","shou-request"}[index]);
            }});
        }
        private void OpenM3Traveler()
        {
            Present(new M1PanelModel{Title=M3Text.Pair("渡与出发名单","Du and the departure list"),Body=()=>M3Text.Pair("渡在温室等候。她关注名单中每个人是否有选择权。完成补给请求后可再次邀请；拒绝不会封死主线。","Du waits in the greenhouse. She wants every name on the list to belong to someone who had a choice. After delivering supplies, you may ask again. Refusal never blocks the main story."),Labels=new[]{M3Text.Pair("兑现补给","Deliver supplies"),M3Text.Pair("邀请同行","Invite to travel"),M3Text.Pair("确认留守","Confirm staying"),M3Text.Pair("告知个人远航","Inform of departure"),M3Text.Pair("岑与旧承诺","Cen and old promises"),M3Text.Pair("身份表达","Express identity"),M3Text.Pair("返回","Back")},Command=i=>{
                if(i==6){OpenM3Hub();return;}if(i==4){OpenM3Cen();return;}if(i==5){OpenM3Identity();return;}
                if(!S3.duMet){Notify(M3Text.Pair("先到温室见渡。","Meet Du at the greenhouse first."));return;}
                if(i==0)OpenM3Actions("du-request");if(i==1){S3.duAnswer=S3.duRequest?1:2;ShowM3Story(S3.duRequest?"du-consent":"du-refuse");}if(i==2){S3.duAnswer=2;ShowM3Story("stay");}if(i==3){S3.Claim("departure-told-du");ShowM3Story("informed");}
            }});
        }
        private void OpenM3Cen()=>Present(new M1PanelModel{Title=M3Text.Pair("岑 · 留守联络","Cen · Ground contact"),Body=()=>PromiseText()+"\n"+M3Text.Pair("岑会留在地球做联络者。早期拒绝援助不等于敌对；可以解释、补偿并重新开放合作。","Cen remains on Earth as a contact. Refusing early aid was not hostility. Explanation and compensation can reopen cooperation."),Labels=new[]{M3Text.Pair("修复合作","Repair cooperation"),M3Text.Pair("告知远航","Inform of departure"),M3Text.Pair("交易：金属2换口粮2","Trade: 2 scrap for 2 rations"),M3Text.Pair("返回","Back")},Command=i=>{if(i==0)OpenM3Actions("cen-repair");if(i==1){S3.Claim("departure-told-cen");ShowM3Story("cen-stay");}if(i==2)Notify(E.Storage.Exchange(new[]{new Cost(Resource.Scrap,2)},new Cost(Resource.Ration,2))?M3Text.Pair("交易完成。","Trade completed."):M3Text.Pair("缺少材料或仓储已满。","Missing materials or storage full."));if(i==3)OpenM3Hub();}});
        private void OpenM3Identity()=>Present(new M1PanelModel{Title=M3Text.Pair("如何称呼自己","How to name yourself"),Body=()=>M3Text.Pair("制造记录解释了权限来源，却不能替你决定身份。这个回答改变独白，不改变工程权限。","The maker archive explains your authorization, but cannot decide who you are. This answer changes your reflection, not your engineering rights."),Labels=new[]{M3Text.Pair("承认造物身份","Acknowledge being made"),M3Text.Pair("我仍自认为人类","I still consider myself human"),M3Text.Pair("以后回答","Answer later")},Command=i=>{if(i<2){S3.identity=i+1;ShowM3Story("identity");}else OpenM3Hub();}});
        private void OpenM3Endings()
        {
            if(!S3.navigation){Notify(M3Text.Pair("共同工程完成后公开三条路线。","All three routes become visible after shared engineering is complete."));return;}
            Present(new M1PanelModel{Title=M3Text.Pair("三条未来道路","Three futures"),Body=()=>string.Join("\n\n",new[]{M3Ending.Light,M3Ending.Voyage,M3Ending.Together}.Select(route=>M3Text.Pair(new[]{"","留灯","远航","同行"}[(int)route],route.ToString())+"\n"+M3Rules.EndingMissing(route,S3,C.Grid.Items)+"\n"+Catalog.Describe(M3Rules.FinaleCost(route,S3))))+"\n"+M3Text.Pair("座位：","Seats: ")+S3.seats,Labels=new[]{M3Text.Pair("留灯","Keep the light"),M3Text.Pair("远航","Voyage"),M3Text.Pair("同行","Together"),M3Text.Pair("扩容：金属3、纤维2","Add seat: 3 scrap, 2 fiber"),M3Text.Pair("返回","Back")},Command=i=>{
                if(i==4){OpenM3Hub();return;}if(i==3){if(S3.seats<5&&E.Storage.Pay(new Cost(Resource.Scrap,3),new Cost(Resource.Fiber,2)))S3.seats++;return;}
                var route=(M3Ending)(i+1);var missing=M3Rules.EndingMissing(route,S3,C.Grid.Items);if(missing!=""){Notify(missing);return;}
                Present(new M1PanelModel{Title=M3Text.Pair("即将进入尾声","Enter the epilogue"),Body=()=>M3Text.Pair("将先建立独立保存点，再冻结名单并扣除补给。观看后可以返回这里准备其他道路。","An independent checkpoint will be written before freezing the passenger list and consuming supplies. Return to it later to prepare another future.")+"\n"+Catalog.Describe(M3Rules.FinaleCost(route,S3)),Labels=new[]{M3Text.Pair("确认","Confirm"),M3Text.Pair("继续准备","Keep preparing")},Command=j=>{if(j==1){OpenM3Endings();return;}if(!director.ConfirmM3Ending(route))Notify(M3Text.Pair("尚有警报、缺少补给或保存失败。","An alert, missing supplies or save failure prevents departure.")+director.SaveMessage);}});
            }});
        }
        public void ShowM3Ending()
        {
            var route=S3.ending;ShowM3Story("ending-"+route,()=>Present(new M1PanelModel{Title=M3Text.Pair("旅程的这一页","This page of the journey"),Body=()=>M3Text.Pair("工程能力改变了用途。每一条道路都带着责任，也留下另一种可能。","The engineering has found its purpose. Every road carries responsibility and leaves another possibility behind.")+"\n"+M3Text.Pair("同行名单：","Passengers: ")+string.Join(", ",S3.passengers),Labels=new[]{M3Text.Pair("返回终局前","Return before the ending"),M3Text.Pair("主菜单","Main menu")},Command=i=>{if(i==0)director.Run(director.LoadSlotAsync(4));else director.OpenMainMenu();}}));
        }
        private void OpenM3Journal()=>Present(new M1PanelModel{Title=M3Text.Pair("日志 · 已知事实与工程","Journal · Facts and engineering"),Body=()=>M3Objective()+"\n\n"+PromiseText()+"\n\n"+string.Join("\n\n",N.Journal)+"\n\n"+M3Text.Pair("主动时间 / 采集 / 重复采集（秒）：","Active / gathering / repeat gathering (seconds): ")+S3.activeSeconds.ToString("F0")+" / "+S3.gatherSeconds.ToString("F0")+" / "+S3.repeatGatherSeconds.ToString("F0"),Labels=new[]{M3Text.Pair("回看人物记录","Reread character records"),M3Text.Pair("关闭","Close")},Command=i=>{if(i==1)CloseWindow();else OpenM3Archive();}});
    }
    public sealed partial class M1SurvivalSystem
    {
        public void ApplyM3Exposure(float dt,bool extreme,bool protectedHome,bool clothing)
        {
            if(!extreme)return;Exposure=Mathf.Clamp(Exposure+dt*(protectedHome?-.7f:clothing?.018f:.065f),0,100);
        }
    }
    public sealed class M3Worker:MonoBehaviour
    {
        public int Index;private M1Navigator navigator;private NavMeshPath path;private float pathAt;private Vector3 destination;private bool valid;
        public bool Tick(M1GameSession session,M3Robot robot,System.Collections.Generic.IEnumerable<Building> buildings,float dt)
        {
            if(navigator==null){navigator=GetComponent<M1Navigator>()??gameObject.AddComponent<M1Navigator>();navigator.Initialize(session,1.4f);path=new NavMeshPath();}
            if(!robot.awake||robot.broken||!robot.enabled){navigator.Stop();return false;}
            var target=buildings.FirstOrDefault(b=>!b.Damaged&&(robot.job==M3Job.Gardening?b.type==Structure.Planter:robot.job==M3Job.Processing?b.type==Structure.Workbench:b.type==Structure.Charger));
            var point=target==null?new Vector3(Index==1?-17:17,0,-17):new Vector3(target.x*2,0,target.z*2);
            if(!navigator.Available)return false;
            if(Time.time>=pathAt){pathAt=Time.time+1;valid=false;for(int i=0;i<8;i++){var p=point+Quaternion.Euler(0,i*45,0)*Vector3.forward*1.6f;if(NavMesh.SamplePosition(p,out var hit,.6f,NavMesh.AllAreas)&&NavMesh.CalculatePath(transform.position,hit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete){destination=hit.position;valid=true;break;}}}
            if(!valid){navigator.Stop();return false;}if(Vector3.Distance(transform.position,destination)>.4f){navigator.Go(destination);return false;}navigator.Stop();return true;
        }
        private void OnDisable(){navigator?.Shutdown();navigator=null;}
    }
    public sealed class M3SeasonVisual:MonoBehaviour
    {
        public Renderer ground;public Material[] seasons;
        public void Apply(M3Season season){if(ground!=null&&seasons!=null&&seasons.Length==4)ground.sharedMaterial=seasons[(int)season];}
    }
}
