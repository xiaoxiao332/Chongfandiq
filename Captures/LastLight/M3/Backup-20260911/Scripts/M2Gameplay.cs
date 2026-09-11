using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace LastLight
{
    public sealed partial class M1GameSession
    {
        private bool seasonalStarting,appliedSpring;
        private float m2VisualAt;
        private string M2HudHeader()=> (director.World.Data.spring?L.K("t30df307bbb")+director.World.Day+L.K("tf8bb0516cc"):L.K("t4adebcef49"))+"\n"+(director.World.Data.warning?L.K("t95102da725"):"")+"\n";
        private string M2ControlHints()=>"\n\n"+actor.Binding("Interact")+" "+L.K("t1b2c73865e")+" · "+actor.Binding("Build")+" "+L.K("t858f36a4ba")+"\n"+actor.Binding("Bag")+" "+L.K("tff3d08fd8c")+" · "+actor.Binding("Journal")+" "+L.K("t4de50894b8")+" · Esc "+L.K("t130448bce6");
        private string M2Objective()
        {
            var w=director.World.Data;
            if(Story.stove&&C.HasHouse&&C.Grid.Items.Any(b=>b.type==Structure.Bed)&&C.Grid.Items.Any(b=>b.type==Structure.Storage)&&!C.Grid.Items.Any(b=>b.type==Structure.Workbench&&!b.Damaged))return L.K("t9cc67bb2b4");
            if(!Story.completed)return N.Objective(C);
            if(!C.Grid.Items.Any(b=>b.type==Structure.Workbench&&!b.Damaged))return L.K("t9cc67bb2b4");
            if(!w.bridge)return w.bridgeOrdered?L.K("tb200393319"):L.K("t96b4627c1f");
            if(w.samples.Any(s=>!s))return L.K("tbca9bd0b54");
            if(w.harvests==0)return L.K("t53eb937628");
            return L.K("t88b68a73dc");
        }
        private void OpenBridge()
        {
            Present(new M1PanelModel{Title=L.K("t6b8e522923"),Body=()=>M2Objective()+L.K("t032be92606")+(director.World.Data.bridge?L.K("t22da7f2dca"):L.K("td24df3f21d")),Labels=new[]{L.K("tc2b376b7bb"),L.K("t32055f6afc"),L.K("t784644d883"),L.K("t6c14bd7f6f")},Command=i=>{
                if(i==0){bool ok=director.World.OrderBridge(C,N,E.Storage);Notify(ok?L.K("t14a18b4503"):L.K("t0a7ab8edc5"));if(ok)CloseWindow();}
                if(i==1){if(director.World.Data.bridge&&!T.Active){CloseWindow();director.Run(director.TravelAsync("Greenhouse"));}else Notify(L.K("t6ca1816c90"));}
                if(i==2)OpenSaveSlots(true);if(i==3)CloseWindow();}});
        }
        private void OpenPlanter(Building b)
        {
            Present(new M1PanelModel{Title=L.K("tda090bdce1"),Body=()=>L.K("tc7407905df")+(b.crop<0?L.K("t38f3689168"):Catalog.ResourceNames[(int)M2WorldSystem.Crops[b.crop]]+"  "+Mathf.FloorToInt(100*b.growth/M2WorldSystem.GrowSeconds[b.crop])+"%")+"\n"+(b.protectedCrop?L.K("t5407206a90"):L.K("t2a2e71069e"))+L.K("t74dbca247b"),Labels=new[]{L.K("t788b06b4df"),L.K("t29eed7cb64"),L.K("t92e4bf2c09"),L.K("t5248a55e96"),L.K("t1f8b1924a0"),L.K("t6c14bd7f6f")},Command=i=>{
                if(i==5){CloseWindow();return;}
                bool ok=i<3?director.World.Plant(b,i,E.Storage):i==3?director.World.Harvest(b,E.Storage):director.World.Protect(b,E.Storage);
                Notify(ok?L.K("t35ea30da73"):L.K("td219993b30"));
                if(ok){actor.WorkFeedback();C.Changed();director.AutoSave();}
            }});
        }
        private void TickM2(float dt)
        {
            var w=director.World.Data;director.World.Advance(dt,C);
            if(home&&w.bridgeOrdered&&!T.Active)
            {
                // AI Navigation owns reachability and movement, including user-built obstacles.
                var target=new Vector3(-20,0,-8);
                bool reachable=false;
                if(C.LiangBroken)C.LiangStatus=L.K("tc3182d0a08");
                else if(!C.LiangWorking)C.LiangStatus=L.K("t20e5a6dff8");
                else if(!E.Storage.CanPay(M2WorldSystem.BridgeCost))C.LiangStatus=L.K("t73775b9dd0")+Catalog.Describe(M2WorldSystem.BridgeCost);
                else reachable=WalkLiang(target,dt);
                if(!C.LiangWorking||C.LiangBroken)liangNavigation?.Stop();
                if(C.LiangWorking&&!C.LiangBroken&&reachable)C.LiangStatus=L.K("t4dfc9de082");
                if(director.World.WorkBridge(dt,reachable,C,E.Storage)){ApplySeasonVisuals();N.Log(L.K("t4974369ded"));director.AutoSave();}
            }
            if(w.spring&&!w.warning&&!T.Active&&w.springTime>=w.nextThreat){w.warning=true;w.warningAt=-1;Notify(L.K("t93e1727c0d"));}
            if(home&&w.warning&&!T.Active)
            {
                if(w.warningAt<0)w.warningAt=w.springTime;
                if(w.springTime-w.warningAt>=45&&!seasonalStarting)director.Run(StartSeasonalRaid());
            }
            if(Time.unscaledTime>=m2VisualAt){m2VisualAt=Time.unscaledTime+1;UpdateFarmVisuals();}
        }
        private async Task StartSeasonalRaid()
        {
            seasonalStarting=true;
            try
            {
                var incoming=await director.Global.Systems.Get<ResourceSystem>().RentAsync(director.Prefix+"Prefabs/Raider",objects,scope,1,sceneLifetime.Token);
                if(!active||Paused||!T.StartSeasonal(ActiveTime)){incoming.Dispose();return;}
                director.World.Data.warning=false;director.World.Data.warningAt=-1;raider=incoming;
                var go=raider.Instance;go.transform.position=new Vector3(0,0,-24);go.SetActive(true);go.GetComponent<M1Beast>().Initialize(this,true,go.transform.GetChild(0));
                Notify(L.K("t4688a0a884"));director.AutoSave();
            }
            finally{seasonalStarting=false;}
        }
        private Vector3 SpringRaidTarget()
        {
            var storage=C.Grid.Items.FirstOrDefault(b=>b.type==Structure.Storage&&b.health>0);
            // Approach the interaction side, not the collider centre.
            return storage==null?raidTarget:Position(storage)+Vector3.back*1.4f;
        }
        public bool SleepUntilMorning(Building bed=null)
        {
            if(!home||T.Active||director.World.Data.warning||Story.choice>0&&!Story.raidFinished)return false;
            if(bed!=null&&(bed.type!=Structure.Bed||bed.Damaged||bed.occupant=="visitor"))return false;
            if(bed!=null){foreach(var b in C.Grid.Items)if(b.occupant=="player")b.occupant="";bed.occupant="player";}
            float time=director.World.Data.spring?director.World.Data.springTime:ActiveTime;
            float seconds=M2WorldSystem.DaySeconds-time%M2WorldSystem.DaySeconds;
            // Bounded one-second simulation uses the same rules as active time.
            for(float elapsed=0;elapsed<seconds;)
            {
                float dt=Mathf.Min(1,seconds-elapsed);elapsed+=dt;director.World.Advance(dt,C);
                V.Tick(dt,Story.stove,V.Warm,director.World.Data.spring);V.Rest(dt);
            }
            if(director.World.Data.spring&&director.World.Data.springTime>=director.World.Data.nextThreat){director.World.Data.warning=true;director.World.Data.warningAt=-1;}
            C.CancelRepair();UpdateFarmVisuals();director.AutoSave();return true;
        }
        private void OpenM2Rest(Building bed)=>Present(new M1PanelModel{Title=L.K("tcb7d58ac2e"),Body=()=>L.K("t1f6d2ac592"),Labels=new[]{L.K("tda11d57634"),L.K("t6c14bd7f6f")},Command=i=>{CloseWindow();if(i==0)Notify(SleepUntilMorning(bed)?L.K("t5d7f004448"):L.K("tb6f07bbf34"));}});
        private void ApplySeasonVisuals()
        {
            appliedSpring=director.World.Data.spring;
            foreach(var root in gameObject.scene.GetRootGameObjects())foreach(var visual in root.GetComponentsInChildren<M2SeasonVisual>(true))visual.Apply(appliedSpring,director.World.Data.bridge);
        }
        private void UpdateFarmVisuals()
        {
            if(appliedSpring!=director.World.Data.spring)ApplySeasonVisuals();
            foreach(var pair in buildings){var visual=pair.Value.Instance.GetComponent<M2FarmVisual>();if(visual!=null)visual.Apply(C.Find(pair.Key));}
        }
    }
    public sealed partial class M1Actor
    {
        private float workUntil;
        public void WorkFeedback(){workUntil=Time.unscaledTime+.7f;}
        private void WorkPose(){if(arm!=null&&Time.unscaledTime<workUntil)arm.localRotation*=Quaternion.Euler(-55*Mathf.Sin((workUntil-Time.unscaledTime)*8),0,0);}
    }
}
