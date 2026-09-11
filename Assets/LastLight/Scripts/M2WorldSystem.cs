using System;
using System.Linq;
using UnityEngine;

namespace LastLight
{
    [Serializable] public sealed class M2WorldData
    {
        public bool spring, bridge, bridgeOrdered, starterUsed, greenhouseRecord;
        public bool[] samples=new bool[3];
        public float springTime, bridgeProgress, nextThreat=1800, warningAt=-1;
        public bool warning;
        public int harvests, seasonalRaids;
    }
    public sealed class M2WorldSystem : GameSystemBase
    {
        public M2WorldData Data { get; private set; }=new M2WorldData();
        public const float DaySeconds=900, BridgeSeconds=12;
        public static readonly float[] GrowSeconds={150,180,210};
        public static readonly Resource[] Crops={Resource.Ration,Resource.Fiber,Resource.Fuel};
        public static readonly Cost[] BridgeCost={new Cost(Resource.Wood,8),new Cost(Resource.Parts,3)};
        public static readonly Cost[] ProtectionCost={new Cost(Resource.Scrap,3),new Cost(Resource.Fiber,2)};
        public int Day=>1+(int)(Data.springTime/DaySeconds);
        public bool OrderBridge(M1ConstructionSystem c,M1StorySystem story,Inventory stock)
        {
            if(Data.bridge||Data.bridgeOrdered||!story.State.completed||!story.State.liang||c.LiangBroken||!c.HasHouse||!c.Grid.Items.Any(b=>b.type==Structure.Workbench&&!b.Damaged))return false;
            if(!stock.CanPay(BridgeCost))return false;
            c.CancelRepair();Data.bridgeOrdered=true;return true;
        }
        public bool WorkBridge(float dt,bool reachable,M1ConstructionSystem c,Inventory stock)
        {
            if(!Data.bridgeOrdered||Data.bridge||!reachable||!c.LiangWorking||c.LiangBroken)return false;
            Data.bridgeProgress=Mathf.Min(BridgeSeconds,Data.bridgeProgress+dt);
            if(Data.bridgeProgress<BridgeSeconds||!stock.Pay(BridgeCost))return false;
            Data.bridge=true;Data.spring=true;Data.bridgeOrdered=false;return true;
        }
        public bool Plant(Building b,int crop,Inventory stock)
        {
            if(b==null||b.type!=Structure.Planter||b.Damaged||b.crop>=0||crop<0||crop>2)return false;
            bool starter=crop==0&&!Data.starterUsed;
            if(!starter&&!Data.samples[crop])return false;
            if(!starter&&!stock.Pay(new Cost(Crops[crop],1)))return false;
            if(starter)Data.starterUsed=true;
            b.crop=crop;b.growth=0;return true;
        }
        public bool Harvest(Building b,Inventory stock)
        {
            if(b==null||b.Damaged||b.crop<0||b.growth<GrowSeconds[b.crop])return false;
            if(!stock.Add(Crops[b.crop],6))return false;
            b.crop=-1;b.growth=0;Data.harvests++;return true;
        }
        public bool Protect(Building b,Inventory stock)
        {
            if(b==null||b.type!=Structure.Planter||b.Damaged||b.protectedCrop||!stock.Pay(ProtectionCost))return false;
            b.protectedCrop=true;return true;
        }
        public void Advance(float dt,M1ConstructionSystem c,bool fullSeasons=false)
        {
            if(dt<0||float.IsNaN(dt)||float.IsInfinity(dt))throw new ArgumentOutOfRangeException(nameof(dt));
            if(Data.spring)Data.springTime+=dt;
            foreach(var b in c.Grid.Items)
                if(b.type==Structure.Planter&&b.crop>=0&&!b.Damaged)
                    b.growth=Mathf.Min(GrowSeconds[b.crop],b.growth+dt*(Data.spring?(fullSeasons?M3Rules.Growth(M3Rules.Season(Data.springTime),b.protectedCrop):1):b.protectedCrop?.8f:.25f));
        }
        public M2WorldData Capture()=>JsonUtility.FromJson<M2WorldData>(JsonUtility.ToJson(Data));
        public void Restore(M2WorldData data){Data=JsonUtility.FromJson<M2WorldData>(JsonUtility.ToJson(data));}
    }
}
