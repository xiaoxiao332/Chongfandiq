using System;
using System.Collections.Generic;
using System.Linq;

namespace LastLight
{
    public enum Resource { Wood, Stone, Scrap, Fiber, Ration, Fuel, Parts, Cell }
    public enum Structure { Floor, Wall, Door, Window, Stove, Bed, Workbench, Storage, Barricade, Planter }
    [Serializable] public struct Cost
    {
        public Resource resource; public int amount;
        public Cost(Resource r,int n){resource=r;amount=n;}
    }
    public static class Catalog
    {
        public static readonly string[] ResourceNames={L.K("tb6da047695"),L.K("t3373687deb"),L.K("tbe9c929962"),L.K("t04c7f4ad60"),L.K("tdcb0a9b6f2"),L.K("t028448f9ba"),L.K("td56f268733"),L.K("tcab07a5ce4")};
        public static readonly string[] StructureNames={L.K("tfde815d4fc"),L.K("t6512c72274"),L.K("t57b3e13562"),L.K("t1a3d1ffd19"),L.K("t71d1438255"),L.K("ta30d8f6da6"),L.K("t8483cd2b08"),L.K("tc07e756255"),L.K("t03f9bf1203"),L.K("tda090bdce1")};
        public static Cost[] Recipe(Structure s)
        {
            switch(s){
                case Structure.Floor:return new[]{new Cost(Resource.Wood,2)};
                case Structure.Wall:case Structure.Door:case Structure.Window:return new[]{new Cost(Resource.Wood,2)};
                case Structure.Stove:return new[]{new Cost(Resource.Stone,4),new Cost(Resource.Scrap,2)};
                case Structure.Bed:return new[]{new Cost(Resource.Wood,3),new Cost(Resource.Fiber,2)};
                case Structure.Workbench:return new[]{new Cost(Resource.Wood,4),new Cost(Resource.Scrap,3)};
                case Structure.Planter:return new[]{new Cost(Resource.Wood,4),new Cost(Resource.Scrap,2)};
                case Structure.Storage:return new[]{new Cost(Resource.Wood,4)};
                default:return new[]{new Cost(Resource.Wood,3)};
            }
        }
        public static string Describe(IEnumerable<Cost> costs)=>string.Join(" / ",costs.Select(c=>ResourceNames[(int)c.resource]+" ×"+c.amount));
        public static bool Edge(Structure s)=>s==Structure.Wall||s==Structure.Door||s==Structure.Window;
        public static Cost[] FuelRecipe => new[]{new Cost(Resource.Wood,1)};
        public static Cost[] PartsRecipe => new[]{new Cost(Resource.Scrap,2)};
        public static Cost[] StoveRepair => new[]{new Cost(Resource.Stone,2),new Cost(Resource.Scrap,2)};
        public static Cost[] Reinforcement => new[]{new Cost(Resource.Parts,4)};
        public static Cost[] Repair(Structure s) => Edge(s)||s==Structure.Barricade
            ? new[]{new Cost(Resource.Wood,1)} : new[]{new Cost(Resource.Parts,1)};
        public const float RepairSeconds=8;
    }
    public sealed class Inventory
    {
        private readonly int[] amounts=new int[8];
        public readonly int Slots; public const int Stack=20;
        public Inventory(int slots){Slots=slots;}
        public int this[Resource r]=>amounts[(int)r];
        public int UsedSlots=>amounts.Sum(n=>(n+Stack-1)/Stack);
        public bool Add(Resource r,int n)
        {
            if(n<0)return false;
            int i=(int)r,old=(amounts[i]+Stack-1)/Stack,updated=(amounts[i]+n+Stack-1)/Stack;
            if(UsedSlots-old+updated>Slots)return false;
            amounts[i]+=n;return true;
        }
        public bool CanPay(params Cost[] costs)
        {
            int[] needs=new int[8];foreach(var c in costs){if(c.amount<0)return false;needs[(int)c.resource]+=c.amount;}
            for(int i=0;i<8;i++)if(amounts[i]<needs[i])return false;return true;
        }
        public bool Pay(params Cost[] costs){if(!CanPay(costs))return false;foreach(var c in costs)amounts[(int)c.resource]-=c.amount;return true;}
        public bool Transfer(Inventory destination,Resource r,int n)
        {
            if(destination==this||n<=0||this[r]<n)return false;
            if(!destination.Add(r,n))return false;amounts[(int)r]-=n;return true;
        }
        public int Deposit(Inventory other)
        {
            int total=0;for(int i=0;i<8;i++){var r=(Resource)i;int n=this[r];if(n>0&&Transfer(other,r,n))total+=n;}return total;
        }
        public void Lose(float fraction,int cap=int.MaxValue){for(int i=0;i<8;i++)amounts[i]-=Math.Min(cap,(int)Math.Floor(amounts[i]*fraction));}
        public void Clear(){Array.Clear(amounts,0,amounts.Length);}
        public bool Exchange(Cost[] costs, params Cost[] gains)
        {
            if(!CanPay(costs))return false;
            var copy=new Inventory(Slots);
            foreach(Resource r in Enum.GetValues(typeof(Resource)))copy.Add(r,this[r]);
            copy.Pay(costs);
            foreach(var gain in gains)if(!copy.Add(gain.resource,gain.amount))return false;
            Array.Copy(copy.amounts,amounts,amounts.Length);return true;
        }
    }
    [Serializable] public sealed class Building
    {
        public int id,x,z,rotation;public Structure type;public float health=100;
        public string occupant="";
        public int crop=-1;public float growth;public bool protectedCrop;
        public bool Damaged=>health<100;
        public Building(int id,Structure t,int x,int z,int r){this.id=id;type=t;this.x=x;this.z=z;rotation=r;}
    }
    public sealed class BuildGrid
    {
        public readonly List<Building> Items=new List<Building>();
        public readonly HashSet<(int,int)> Reserved=new HashSet<(int,int)>();
        private int nextId=1;
        public void RestoreBuildings(IEnumerable<Building> values){Items.Clear();Items.AddRange(values);nextId=Items.Count==0?1:Items.Max(b=>b.id)+1;}
        public static readonly int[] DX={0,1,0,-1},DZ={1,0,-1,0};
        public bool IsFloor(int x,int z)=>Items.Any(b=>b.x==x&&b.z==z&&b.type==Structure.Floor);
        public bool Occupied(int x,int z)=>Items.Any(b=>b.x==x&&b.z==z&&!Catalog.Edge(b.type)&&b.type!=Structure.Floor);
        public Building EdgeAt(int x,int z,int dir)=>Items.FirstOrDefault(b=>Catalog.Edge(b.type)&&
            ((b.x==x&&b.z==z&&b.rotation==dir)||(b.x==x+DX[dir]&&b.z==z+DZ[dir]&&b.rotation==(dir+2)%4)));
        public string Validate(Structure t,int x,int z,int dir)
        {
            if(dir<0||dir>3)return L.K("tbe1e7fc613");
            if(x < -7||x>7||z < -7||z>7)return L.K("t42d1e26671");
            if(Reserved.Contains((x,z)))return L.K("t5e73625726");
            if(t==Structure.Floor)return IsFloor(x,z)?L.K("t949aa6244d"):"";
            if(Catalog.Edge(t)){
                if(!IsFloor(x,z))return L.K("t94b4eef7d1");
                if(EdgeAt(x,z,dir)!=null)return L.K("t40afc03f1d");
                if(Reserved.Contains((x+DX[dir],z+DZ[dir])))return L.K("tae4f210823");
                // A closed enclosure must include at least one usable door.
                var candidate=new Building(-1,t,x,z,dir);Items.Add(candidate);
                bool invalid=false;
                foreach(var floor in Items.Where(b=>b.type==Structure.Floor).ToArray())
                    if(Room(floor.x,floor.z,out bool hasDoor).Count>0&&!hasDoor){invalid=true;break;}
                bool edgeReachable=AllFacilitiesReachable();
                Items.Remove(candidate);return invalid?L.K("tfb34c4cdc1"):!edgeReachable?L.K("t3d77f22d49"):"";
            }
            if(t!=Structure.Barricade&&t!=Structure.Planter&&!IsFloor(x,z))return L.K("t2ec8a9a3d5");
            if(Occupied(x,z))return L.K("tcc69a98f2c");
            foreach(var door in Items.Where(b=>b.type==Structure.Door))
                if((door.x==x&&door.z==z)||(door.x+DX[door.rotation]==x&&door.z+DZ[door.rotation]==z))return L.K("t62681f299f");
            // Keep a walkable neighboring interaction tile for every facility.
            var item=new Building(-1,t,x,z,dir);Items.Add(item);
            bool reachable=AllFacilitiesReachable();Items.Remove(item);
            return reachable?"":L.K("t888636a5b9");
        }
        public Building Place(Structure t,int x,int z,int dir,Inventory storage,out string error)
        {
            error=Validate(t,x,z,dir);if(error!="")return null;
            if(!storage.Pay(Catalog.Recipe(t))){error=L.K("t3a6c8d2244")+Catalog.Describe(Catalog.Recipe(t));return null;}
            var b=new Building(nextId++,t,x,z,dir);Items.Add(b);return b;
        }
        public bool Remove(Building b,Inventory storage,out string error)
        {
            error="";if(!Items.Contains(b)){error=L.K("ta17c57ff86");return false;}
            if(!string.IsNullOrEmpty(b.occupant)){error=L.K("tb52638e8ae");return false;}
            if(b.Damaged){error=L.K("t590cd72149");return false;}
            if(b.crop>=0){error=L.K("t58a59c8598");return false;}
            if(b.type==Structure.Floor&&Items.Any(v=>v!=b&&v.x==b.x&&v.z==b.z)){error=L.K("tb8ae8e74f9");return false;}
            var costs=b.type==Structure.Planter&&b.protectedCrop?Catalog.Recipe(b.type).Concat(new[]{new Cost(Resource.Scrap,3),new Cost(Resource.Fiber,2)}).ToArray():Catalog.Recipe(b.type);var test=new Inventory(storage.Slots);
            foreach(Resource r in Enum.GetValues(typeof(Resource)))test.Add(r,storage[r]);
            foreach(var c in costs)if(!test.Add(c.resource,c.amount)){error=L.K("tea5d36da43");return false;}
            Items.Remove(b);foreach(var c in costs)storage.Add(c.resource,c.amount);return true;
        }
        public bool Move(Building b,int x,int z,int r,out string error)
        {
            if(!Items.Contains(b)){error=L.K("ta17c57ff86");return false;}
            if(b.Damaged){error=L.K("tb7e673b7ba");return false;}
            if(!string.IsNullOrEmpty(b.occupant)){error=L.K("tf9bffc448e");return false;}
            if(b.type==Structure.Floor||Catalog.Edge(b.type)){error=L.K("t04cc480be5");return false;}
            Items.Remove(b);error=Validate(b.type,x,z,r);Items.Add(b);
            if(error!="")return false;b.x=x;b.z=z;b.rotation=r;return true;
        }
        public HashSet<(int,int)> Room(int x,int z,out bool door)
        {
            door=false;var visited=new HashSet<(int,int)>();var q=new Queue<(int,int)>();q.Enqueue((x,z));bool open=false;
            while(q.Count>0){var c=q.Dequeue();if(!visited.Add(c))continue;if(visited.Count>225){open=true;break;}
                for(int d=0;d<4;d++){var e=EdgeAt(c.Item1,c.Item2,d);if(e!=null&&e.health>0){door|=e.type==Structure.Door;continue;}
                    int nx=c.Item1+DX[d],nz=c.Item2+DZ[d];if(!IsFloor(nx,nz)){open=true;continue;}if(!visited.Contains((nx,nz)))q.Enqueue((nx,nz));}}
            return open?new HashSet<(int,int)>():visited;
        }
        public bool AllFacilitiesReachable()
        {
            var visited=new HashSet<(int,int)>();var q=new Queue<(int,int)>();q.Enqueue((0,-8));
            while(q.Count>0){var c=q.Dequeue();if(!visited.Add(c))continue;
                for(int d=0;d<4;d++){int x=c.Item1+DX[d],z=c.Item2+DZ[d];if(x< -8||x>8||z< -8||z>8||Occupied(x,z)||visited.Contains((x,z)))continue;
                    var e=EdgeAt(c.Item1,c.Item2,d);if(e!=null&&e.health>0&&e.type!=Structure.Door)continue;q.Enqueue((x,z));}}
            return Reserved.All(c=>visited.Contains(c))&&Items.Where(b=>b.type!=Structure.Floor&&!Catalog.Edge(b.type)).All(b=>Enumerable.Range(0,4).Any(d=>visited.Contains((b.x+DX[d],b.z+DZ[d]))));
        }
    }
    [Serializable] public sealed class StoryState
    {
        public bool core,liang,reward,readRecord,shortcut,raidFinished,raidFailed,reinforced;
        public bool aidRoute,stove,awake,defenseRead,consequence,completed;
        public int visitorBedId;
        public int choice; // 0 unanswered, 1 aid, 2 trade, 3 refuse
        public int promise; // 0 none, 1 pending, 2 extended, 3 fulfilled, 4 withdrawn
        public bool ClaimReward(Inventory storage){if(reward||!liang||!storage.Add(Resource.Parts,4))return false;reward=true;return true;}
        public bool Choose(int option,Inventory storage)
        {
            if(!liang||!reward||choice!=0||option<1||option>3)return false;
            int cost=option==1?2:option==2?1:0;
            if(!storage.Pay(new Cost(Resource.Parts,cost)))return false;
            choice=option;promise=option==2?1:0;aidRoute=option==1;return true;
        }
        public bool Fulfill(Inventory storage,bool freeBed)
        {
            if((promise!=1&&promise!=2)||!freeBed||!storage.CanPay(new Cost(Resource.Ration,2)))return false;
            // Reward fits before any mutation.
            var test=new Inventory(storage.Slots);foreach(Resource r in Enum.GetValues(typeof(Resource)))test.Add(r,storage[r]);
            test.Pay(new Cost(Resource.Ration,2));if(!test.Add(Resource.Wood,8)||!test.Add(Resource.Scrap,4))return false;
            storage.Pay(new Cost(Resource.Ration,2));storage.Add(Resource.Wood,8);storage.Add(Resource.Scrap,4);promise=3;return true;
        }
        public bool Fulfill(Inventory storage,Building bed)
        {
            if(bed==null||bed.type!=Structure.Bed||bed.Damaged||!string.IsNullOrEmpty(bed.occupant))return false;
            if(!Fulfill(storage,true))return false;
            bed.occupant="visitor";visitorBedId=bed.id;return true;
        }
        public bool ExtendPromise(){if(promise!=1)return false;promise=2;return true;}
        public bool WithdrawPromise(){if(promise!=1&&promise!=2)return false;promise=4;return true;}
    }
}
