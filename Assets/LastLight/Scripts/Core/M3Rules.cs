using System;
using System.Collections.Generic;
using System.Linq;

namespace LastLight
{
    public enum M3Season { Spring, Summer, Autumn, Winter }
    public enum M3Job { Repair, Processing, Gardening, Gathering, Energy, Watch }
    public enum M3Stop { Working, Off, Broken, Power, Path, Materials, Full, NoTarget }
    public enum M3Ending { None, Light, Voyage, Together }
    [Serializable] public sealed class M3Robot
    {
        public string id; public bool awake,broken,enabled=true,agreed,requestDone,informed;
        public M3Job job; public int answer; public float progress; public int produced;
        public M3Robot(string value,M3Job role){id=value;job=role;}
    }
    [Serializable] public sealed class M3State
    {
        public M3Robot[] robots={new M3Robot("liang",M3Job.Repair),new M3Robot("ya",M3Job.Gardening),new M3Robot("shou",M3Job.Energy)};
        public List<string> facts=new List<string>();
        public List<string> events=new List<string>();
        public List<string> read=new List<string>();
        public bool insulation,relayRoad,emergencyPower,structure,power,navigation,personalShip,transport,collectivePower;
        public bool supplyDelivered,duMet,duRequest,cenRepaired,pulse,clothing;
        public int identity,duAnswer,seats=1; public M3Ending ending;
        public float generatorSeconds,jobClock,activeSeconds,gatherSeconds,repeatGatherSeconds;
        public List<string> visitedNodes=new List<string>();
        public int[] priority={0,1,2};
        public string[] passengers=Array.Empty<string>();
        public bool Has(string id)=>facts.Contains(id);
        public bool Claim(string id){if(Has(id))return false;facts.Add(id);return true;}
        public void Validate()
        {
            if(robots==null||robots.Length!=3||robots.Any(r=>r==null)||priority==null||priority.Length!=3||priority.OrderBy(i=>i).Where((v,i)=>v!=i).Any())throw new ArgumentException("Invalid robot or power roster");
            var ids=new[]{"liang","ya","shou"};
            for(int i=0;i<3;i++){var r=robots[i];if(r.id!=ids[i]||(int)r.job/2!=i||r.answer<0||r.answer>2||r.produced<0||!Finite(r.progress))throw new ArgumentException("Invalid robot state");}
            foreach(var values in new[]{facts,events,read,visitedNodes})if(values==null||values.Any(string.IsNullOrEmpty)||values.Count!=values.Distinct().Count())throw new ArgumentException("Invalid event ledger");
            if(!Finite(generatorSeconds)||!Finite(jobClock)||!Finite(activeSeconds)||!Finite(gatherSeconds)||!Finite(repeatGatherSeconds)||identity<0||identity>2||duAnswer<0||duAnswer>2||seats<1||seats>5||!Enum.IsDefined(typeof(M3Ending),ending)||passengers==null||passengers.Distinct().Count()!=passengers.Length)throw new ArgumentException("Invalid M3 state");
            if(passengers.Any(p=>p!="du"&&!ids.Contains(p)))throw new ArgumentException("Unknown passenger");
        }
        private static bool Finite(float n)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&n>=0&&n<=1e9;
    }
    public static class M3Rules
    {
        public const float DaySeconds=900,SeasonSeconds=5400,JobSeconds=30;
        public static M3Season Season(float elapsed)=>(M3Season)((long)(elapsed/SeasonSeconds)%4);
        public static int Comfort(IEnumerable<Building> items)=>items.Where(b=>!b.Damaged&&(int)b.type>=13).Select(b=>b.type).Distinct().Take(4).Count();
        public static float Growth(M3Season season,bool protection)=>season==M3Season.Winter?(protection?.8f:.25f):season==M3Season.Summer?(protection?1:.7f):season==M3Season.Autumn?1.25f:1;
        public static bool Usable(IEnumerable<Building> items,Structure type)=>items.Any(b=>b.type==type&&!b.Damaged);
        public static bool Switch(M3Robot robot,M3Job job)
        {
            if(robot==null||!robot.awake||!Enum.IsDefined(typeof(M3Job),job)||(int)robot.job/2!=(int)job/2)return false;
            if(robot.job!=job){robot.job=job;robot.progress=0;}return true;
        }
        public static bool[] Allocate(M3State state,IEnumerable<Building> buildings)
        {
            var result=new bool[3];int capacity=1;
            bool industrial=state.generatorSeconds>0&&Usable(buildings,Structure.Charger);
            if(industrial)capacity+=3;
            foreach(int i in state.priority)if(state.robots[i].awake&&state.robots[i].enabled&&!state.robots[i].broken&&capacity>0)
            {
                // Emergency supply runs Liang's recovery work; never industrial production.
                if(!industrial&&state.robots[i].job!=M3Job.Repair)continue;
                result[i]=true;capacity--;
            }
            return result;
        }
        public static string EndingMissing(M3Ending route,M3State s,IEnumerable<Building> buildings)
        {
            var missing=new List<string>();var items=buildings.ToArray();
            if(!s.structure||!s.power||!s.navigation)missing.Add("共同工程 / Shared engineering");
            if(s.robots.Any(r=>!r.awake))missing.Add("三名机器苏醒 / Wake all three machines");
            if(!Usable(items,Structure.Charger))missing.Add("完好充电座 / Working charging station");
            if(route==M3Ending.Light)
            {
                if(!items.Any(b=>b.type==Structure.Planter&&!b.Damaged&&b.crop==0))missing.Add("口粮生产 / Ration crop planted");
                if(!items.Any(b=>b.type==Structure.Planter&&!b.Damaged&&b.protectedCrop)||!Usable(items,Structure.Stove))missing.Add("保护种植与暖炉 / Protected planter and stove");
                if(s.robots.Any(r=>!r.agreed))missing.Add("三人共同规则 / Shared rules with all machines");
            }
            else if(route==M3Ending.Voyage)
            {
                if(!s.personalShip)missing.Add("个人航天器 / Personal craft");
                if(!s.supplyDelivered)missing.Add("交付留守补给 / Deliver ground supplies");
                if(s.robots.Any(r=>!r.informed)||!s.Has("departure-told-cen")||!s.Has("departure-told-du"))missing.Add("逐一告知伙伴 / Inform every companion");
            }
            else if(route==M3Ending.Together)
            {
                if(!s.transport||!s.collectivePower)missing.Add("运输船与集体能源 / Transport and collective energy");
                if(s.robots.All(r=>r.answer!=1))missing.Add("至少一名机器同意 / One consenting machine");
                if(s.robots.Any(r=>r.answer==0)||s.duAnswer==0)missing.Add("记录全部去留答复 / Record all four replies");
                if(s.seats<1+s.robots.Count(r=>r.answer==1)+(s.duAnswer==1?1:0))missing.Add("乘员容量 / Passenger capacity");
                if(!s.supplyDelivered||!Usable(items,Structure.Stove)||!Usable(items,Structure.Planter))missing.Add("留守设施与补给 / Ground facilities and supplies");
            }
            else missing.Add("选择结局 / Choose a route");
            if(s.ending!=M3Ending.None)missing.Add("已进入尾声 / Epilogue already committed");
            return string.Join("\n",missing);
        }
        public static Cost[] FinaleCost(M3Ending route,M3State state)
        {
            int travelers=route==M3Ending.Together?1+state.robots.Count(r=>r.answer==1)+(state.duAnswer==1?1:0):1;
            return new[]{new Cost(Resource.Ration,travelers*3),new Cost(Resource.Fuel,travelers*2)};
        }
        public static bool CommitEnding(M3Ending route,M3State s,IEnumerable<Building> buildings,Inventory stock)
        {
            if(EndingMissing(route,s,buildings)!=""||!stock.Pay(FinaleCost(route,s)))return false;
            s.passengers=route==M3Ending.Together?s.robots.Where(r=>r.answer==1).Select(r=>r.id).Concat(s.duAnswer==1?new[]{"du"}:Array.Empty<string>()).ToArray():Array.Empty<string>();
            s.ending=route;return true;
        }
    }
}
