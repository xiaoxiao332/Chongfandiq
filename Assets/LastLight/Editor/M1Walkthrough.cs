using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LastLight.Editor
{
    /// <summary>Normal-speed developer walkthrough. No inventory seeding, teleport or clock acceleration.
    /// Movement goes through the real Player action map; menus use their actual Button commands.
    /// Layout placement uses the same validated command as pointer placement (not a UI usability test).
    /// </summary>
    public static class M1Walkthrough
    {
        public static string Status { get; private set; }="Not started";
        public static Task Running { get; private set; }
        private static M1Director director;
        private static Keyboard keyboard;
        private static readonly StringBuilder report=new StringBuilder();
        private static double started;
        private static InputSettings originalSettings,walkthroughSettings;
        private static HideFlags originalSettingsFlags;
        public static void Begin(){if(Running!=null&&!Running.IsCompleted)throw new InvalidOperationException("Walkthrough already running");Running=Run();}
        private static async Task Until(Func<bool> predicate,int seconds=30)
        {double deadline=Time.realtimeSinceStartupAsDouble+seconds;while(!predicate()){if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException(Status);await Task.Yield();}}
        private static async Task Delay(float seconds)
        {double until=Time.realtimeSinceStartupAsDouble+seconds;while(Time.realtimeSinceStartupAsDouble<until)await Task.Yield();}
        private static void Step(string text){Status=text;report.AppendLine($"{(Time.realtimeSinceStartupAsDouble-started):0.0}s | active {director.Survival.ActiveTime:0.0}s | {text}");}
        private static void Release()=>UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        private static async Task Move(Vector3 target)
        {
            target.y=0;double deadline=Time.realtimeSinceStartupAsDouble+100;
            while(true)
            {
                if(director==null||director.Current==null||!UnityEditor.EditorApplication.isPlaying)throw new OperationCanceledException();
                var player=director.Current.Actor;var delta=target-player.transform.position;delta.y=0;if(delta.magnitude<1.45f)break;
                if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException($"走路受阻 {player.transform.position} -> {target}");
                var camera=Camera.main;var right=camera.transform.right;right.y=0;var forward=camera.transform.forward;forward.y=0;
                float x=Vector3.Dot(delta.normalized,right.normalized),y=Vector3.Dot(delta.normalized,forward.normalized);
                var keys=new System.Collections.Generic.List<Key>();if(x>.35f)keys.Add(Key.D);if(x<-.35f)keys.Add(Key.A);if(y>.35f)keys.Add(Key.W);if(y<-.35f)keys.Add(Key.S);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys.ToArray()));await Task.Yield();
            }
            Release();await Delay(.08f);
        }
        private static M1Node Node(string id)=>Object.FindObjectsByType<M1Node>(FindObjectsSortMode.None).First(n=>n.name==id);
        private static async Task Use(string id)
        {
            var node=Node(id);await Move(node.transform.position);director.Current.UseNode(node);await Delay(.25f);
        }
        private static async Task Click(string label)
        {
            Button button=null;
            await Until(()=>{button=Object.FindObjectsByType<M1Panel>(FindObjectsSortMode.None).SelectMany(p=>p.GetComponentsInChildren<Button>()).FirstOrDefault(b=>b.GetComponentInChildren<Text>().text==label);return button!=null;});
            button.onClick.Invoke();await Delay(.15f);
        }
        private static async Task Store(){await Use("emergency-depot");await Click("全部存入");await Click("关闭");}
        private static async Task GatherWood()
        {foreach(var name in Enumerable.Range(0,8).Select(i=>"wood-"+i)){var node=Node(name);if(node.Available(director.Survival.ActiveTime))await Use(name);}}
        private static async Task FinishWoodBudget(int needed)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+300;
            while(director.Economy.Backpack[Resource.Wood]+director.Economy.Storage[Resource.Wood]<needed)
            {
                if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("安全木材恢复未成立");
                var available=Object.FindObjectsByType<M1Node>(FindObjectsSortMode.None).Where(n=>n.Kind==NodeKind.Pickup&&n.Resource==Resource.Wood&&n.Available(director.Survival.ActiveTime)).OrderBy(n=>(n.transform.position-director.Current.Actor.transform.position).sqrMagnitude).FirstOrDefault();
                if(available!=null){await Use(available.name);continue;}
                var next=Object.FindObjectsByType<M1Node>(FindObjectsSortMode.None).Where(n=>n.Kind==NodeKind.Pickup&&n.Resource==Resource.Wood).OrderBy(n=>director.Economy.NodeReady[n.StableId]).First();
                Step("木材不足：回到枯枝点等待真实刷新（记录节奏瓶颈）");await Move(next.transform.position);await Until(()=>next.Available(director.Survival.ActiveTime),190);
            }
        }
        private static async Task Build(Structure type,int x,int z,int direction=0)
        {if(!director.Current.TryBuild(type,x,z,direction))throw new InvalidOperationException("建造失败 "+type+$" {x},{z},{direction}");await Delay(.45f);}
        private static async Task Run()
        {
            director=Object.FindFirstObjectByType<M1Director>();if(director==null)throw new InvalidOperationException("先在 M1Prototype 进入 Play Mode");
            await Until(()=>director.Current!=null&&director.Current.Ready&&!director.Busy);
            if(director.Narrative.State.awake)throw new InvalidOperationException("请从全新一轮开始");
            originalSettings=UnityEngine.InputSystem.InputSystem.settings;originalSettingsFlags=originalSettings.hideFlags;originalSettings.hideFlags|=HideFlags.DontUnloadUnusedAsset;walkthroughSettings=Object.Instantiate(originalSettings);walkthroughSettings.hideFlags=HideFlags.HideAndDontSave;walkthroughSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;walkthroughSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;UnityEngine.InputSystem.InputSystem.settings=walkthroughSettings;
            keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>("M1WalkthroughKeyboard");keyboard.MakeCurrent();started=Time.realtimeSinceStartupAsDouble;report.Clear();
            try
            {
                Step("苏醒与暖炉");await Use("home-terminal");await Click("知道了");await Use("stone-0");await Use("scrap-0");await Store();await Use("emergency-stove");await Click("修复 / 添燃料");await Click("关闭");
                Step("安全采集与家园建设");await GatherWood();await Use("fiber-0");await Use("food-0");await Use("scrap-1");await Use("scrap-2");await Store();
                // Additional wood comes from the same real renewable nodes; use this interval for food,
                // fuel, reading and route reconnaissance rather than waiting for a timer.
                await Use("emergency-bench");for(int i=0;i<3;i++)await Click("制作燃料");await Click("关闭");
                await Use("emergency-stove");for(int i=0;i<3;i++)await Click("修复 / 添燃料");await Click("关闭");
                director.Current.ToggleBuild();await Delay(.3f);
                for(int x=2;x<=3;x++)for(int z=0;z<=2;z++)await Build(Structure.Floor,x,z);
                for(int x=2;x<=3;x++){await Build(Structure.Wall,x,2,0);await Build(x==3?Structure.Door:Structure.Wall,x,0,2);}
                for(int z=0;z<=2;z++){if(director.Economy.Storage[Resource.Wood]<2)break;await Build(Structure.Wall,2,z,3);if(director.Economy.Storage[Resource.Wood]<2)break;await Build(Structure.Wall,3,z,1);}
                director.Current.CloseWindow();
                await Use("fiber-1");await Use("food-1");await Use("food-2");await Use("stone-1");await Use("stone-2");
                // Build once resources return, with normal walking and no clock acceleration.
                await GatherWood();await FinishWoodBudget(18);await Store();
                director.Current.ToggleBuild();await Delay(.3f);
                for(int z=0;z<=2;z++){if(director.Construction.Grid.EdgeAt(2,z,3)==null)await Build(Structure.Wall,2,z,3);if(director.Construction.Grid.EdgeAt(3,z,1)==null)await Build(Structure.Wall,3,z,1);}
                await Build(Structure.Bed,2,1);await Build(Structure.Storage,3,2);await Build(Structure.Barricade,2,-5);director.Current.CloseWindow();
                if(!director.Construction.HasHouse)throw new InvalidOperationException("房间未闭合");
                Step("旧工坊：无战斗取件与撤离记录");await Use("workshop-sign");await Until(()=>director.Current!=null&&!director.Current.IsHome&&!director.Busy);
                await Move(new Vector3(-18,0,-28));for(int z=-20;z<=20;z+=10)await Move(new Vector3(-18,0,z));await Use("departure-record");await Click("知道了");await Move(new Vector3(-18,0,38));await Move(new Vector3(7,0,39));await Use("workshop-core");await Click("知道了");await Use("return-shortcut");await Until(()=>director.Current!=null&&director.Current.IsHome&&!director.Busy);
                Step("梁启动与援助选择");await Use("liang");await Click("安装启动件 / 领取奖励");await Click("关闭");await Use("home-terminal");await Click("援助：给两份零件");
                await Use("aid-drain");await Store();
                Step("教学预告与失败恢复");await Use("home-terminal");await Click("防御说明");await Click("知道了");await Use("home-terminal");await Click("我准备好了");await Until(()=>director.Threat.Active);
                await Move(new Vector3(-12,0,-12));await Until(()=>director.Narrative.State.raidFinished,130);
                await Until(()=>director.Construction.Grid.Items.All(b=>!b.Damaged),80);
                Step("来访后果与收束");await Use("home-terminal");await Click("阅读后果 / 收束");await Until(()=>director.Narrative.State.completed);
                Step("PASS：正常速度、零初始库存流程走通");
                report.AppendLine($"WallSeconds={(Time.realtimeSinceStartupAsDouble-started):0.00}; ActiveSeconds={director.Survival.ActiveTime:0.00}; TimeScale={Time.timeScale}; Pickups={director.Economy.Pickups}; RaidFailed={director.Narrative.State.raidFailed}; LiangRepairs={director.Construction.LiangRepairs}");
            }
            catch(Exception ex){Status="FAIL: "+ex.Message;report.AppendLine(Status+"\n"+ex);Debug.LogException(ex);}
            finally{if(keyboard!=null){Release();UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);keyboard=null;}UnityEngine.InputSystem.InputSystem.settings=originalSettings;originalSettings.hideFlags=originalSettingsFlags;if(walkthroughSettings!=null)Object.Destroy(walkthroughSettings);Directory.CreateDirectory("Captures/LastLight/M1");File.WriteAllText("Captures/LastLight/M1/NormalSpeedWalkthrough-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".txt",report.ToString());File.WriteAllText("Captures/LastLight/M1/NormalSpeedWalkthrough.txt",report.ToString());}
        }
    }
}
