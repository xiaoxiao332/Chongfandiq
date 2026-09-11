using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LastLight
{
    // Session lifetime: scene changes never recreate inventory, clocks or progression.
    public sealed partial class M1EconomySystem : GameSystemBase
    {
        public Inventory Backpack { get; } = new Inventory(12);
        public Inventory Storage { get; } = new Inventory(80);
        public Dictionary<string,float> NodeReady { get; } = new Dictionary<string,float>();
        public int Pickups;
        public bool CraftFuel() => Storage.Exchange(Catalog.FuelRecipe,new Cost(Resource.Fuel,1));
        public bool CraftParts() => Storage.Exchange(Catalog.PartsRecipe,new Cost(Resource.Parts,1));
        public bool Pickup(string id,Resource resource,int amount,float respawn,float time)
        {
            if(NodeReady.TryGetValue(id,out var ready)&&time<ready)return false;
            if(!Backpack.Add(resource,amount))return false;
            NodeReady[id]=time+respawn;Pickups++;return true;
        }
    }
    public sealed partial class M1ConstructionSystem : GameSystemBase
    {
        public BuildGrid Grid { get; } = new BuildGrid();
        public int Revision { get; private set; }
        public int RepairTarget { get; private set; }
        public float RepairProgress { get; private set; }
        public bool LiangWorking { get; set; }
        public bool LiangBroken { get; set; }
        public string LiangStatus { get; set; } = L.K("t2ae339494f");
        public int LiangRepairs { get; private set; }
        public void Changed()=>Revision++;
        public M1ConstructionSystem()
        {
            for(int z=-7;z<=7;z++)Grid.Reserved.Add((0,z));
            for(int x=-7;x<=7;x++)Grid.Reserved.Add((x,-7));
        }
        public bool HasHouse => Grid.Items.Any(b=>b.type==Structure.Floor&&Grid.Room(b.x,b.z,out var door).Count>=4&&door);
        public Building Find(int id)=>Grid.Items.FirstOrDefault(b=>b.id==id);
        public bool BeginRepair(Building b,Inventory storage)
        {
            if(RepairTarget!=0||b==null||!Grid.Items.Contains(b)||!b.Damaged||!storage.CanPay(Catalog.Repair(b.type)))return false;
            RepairTarget=b.id;RepairProgress=0;return true;
        }
        public void CancelRepair(){RepairTarget=0;RepairProgress=0;}
        public bool TickRepair(float dt,Inventory storage,bool reachable,bool liang)
        {
            var b=Find(RepairTarget);
            if(b==null||!b.Damaged){CancelRepair();return false;}
            if(!reachable)return false;
            RepairProgress+=dt;
            if(RepairProgress<Catalog.RepairSeconds)return false;
            if(!storage.Pay(Catalog.Repair(b.type))){CancelRepair();return false;}
            b.health=100;if(liang)LiangRepairs++;CancelRepair();Changed();return true;
        }
        public bool Damage(int id,float damage,M1ThreatSystem threat)
        {
            var b=Find(id);if(b==null||b.type==Structure.Floor||b.health<=0||damage<=0)return false;
            if(threat.Active&&!Catalog.Edge(b.type)&&b.type!=Structure.Barricade&&!threat.RecordEquipment(id))return false;
            b.health=Mathf.Max(0,b.health-damage);Changed();return true;
        }
    }
    public sealed partial class M1SurvivalSystem : GameSystemBase
    {
        public float ActiveTime { get; private set; }
        public float Health { get; private set; }=100;
        public float Hunger { get; private set; }=100;
        public float Exposure { get; private set; }
        public float StoveUntil { get; private set; }
        public int Rescues { get; private set; }
        public bool Warm=>ActiveTime<StoveUntil;
        public void Tick(float dt,bool started,bool sheltered,bool spring=false)
        {
            ActiveTime+=dt;if(!started)return;
            Hunger=Mathf.Max(0,Hunger-dt*.045f);
            Exposure=Mathf.Clamp(Exposure+dt*(sheltered?-.7f:spring?-.08f:.035f),0,100);
            if(Hunger<=0||Exposure>=100)Health=Mathf.Max(0,Health-dt*.16f);
            else if(sheltered&&Hunger>20)Health=Mathf.Min(100,Health+dt*.5f);
        }
        public void Damage(float value)=>Health=Mathf.Max(0,Health-Mathf.Max(0,value));
        public bool Eat(Inventory source){if(Hunger>=99||!source.Pay(new Cost(Resource.Ration,1)))return false;Hunger=Mathf.Min(100,Hunger+35);return true;}
        public bool Fuel(Inventory source){if(!source.Pay(new Cost(Resource.Fuel,1)))return false;StoveUntil=Mathf.Max(ActiveTime,StoveUntil)+300;return true;}
        public void Rest(float dt){Health=Mathf.Min(100,Health+dt*2);Exposure=Mathf.Max(0,Exposure-dt*2);}
        public void Rescue(Inventory backpack){backpack.Lose(.2f);Health=65;Hunger=Mathf.Max(35,Hunger);Exposure=10;Rescues++;}
    }
    public sealed partial class M1StorySystem : GameSystemBase
    {
        public StoryState State { get; }=new StoryState();
        public List<string> Journal { get; }=new List<string>();
        public bool WorkshopVisited;
        public void Log(string message){if(!Journal.Contains(message))Journal.Add(message);}
        public string Objective(M1ConstructionSystem buildings)
        {
            var s=State;
            if(!s.awake)return L.K("t647aa2588f");
            if(!s.stove)return L.K("t9b4358d89b");
            if(!buildings.HasHouse)return L.K("t0c6b1ea8c4");
            if(!buildings.Grid.Items.Any(b=>b.type==Structure.Bed))return L.K("t00384b5737");
            if(!buildings.Grid.Items.Any(b=>b.type==Structure.Storage))return L.K("t4ee147d315");
            if(!s.core)return L.K("td236bf0451");
            if(!s.liang)return L.K("te9a7e8e489");
            if(!s.reward)return L.K("t0106d98cf2");
            if(s.choice==0)return L.K("t07d7a83ff9");
            if(!s.raidFinished)return L.K("t660be52b90");
            if(buildings.Grid.Items.Any(b=>b.Damaged)||buildings.LiangBroken)return L.K("t7467ca0e31");
            if(s.promise==1||s.promise==2)return L.K("td52823b1a3");
            if(!s.consequence)return L.K("tf21c9de4ca");
            return L.K("tc60fb0da14");
        }
    }
    public sealed partial class M1ThreatSystem : GameSystemBase
    {
        public bool Active { get; private set; }
        public int Strikes { get; private set; }
        public float StartedAt { get; private set; }
        public float BufferUntil { get; private set; }
        public HashSet<int> DamagedEquipment { get; }=new HashSet<int>();
        public bool Start(float now,StoryState story,bool ready)
        {
            if(Active||story.raidFinished||now<BufferUntil||story.choice==0||!story.defenseRead||!ready)return false;
            Active=true;Strikes=0;StartedAt=now;DamagedEquipment.Clear();return true;
        }
        public void Strike(){if(Active)Strikes++;}
        public bool RecordEquipment(int id){if(DamagedEquipment.Contains(id))return true;if(DamagedEquipment.Count>=2)return false;DamagedEquipment.Add(id);return true;}
        public bool Finish(bool failed,float now,StoryState story,Inventory storage)
        {
            if(!Active||story.raidFinished)return false;
            Active=false;story.raidFinished=true;story.raidFailed=failed;BufferUntil=now+1800;
            if(failed)storage.Lose(.1f,5);return true;
        }
    }
}
