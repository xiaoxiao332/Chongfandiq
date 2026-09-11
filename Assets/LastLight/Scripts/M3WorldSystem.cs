using System;
using System.Linq;
using UnityEngine;

namespace LastLight
{
    public sealed class M3WorldSystem : GameSystemBase
    {
        public M3State Data {get;private set;}=new M3State();
        public readonly M3Stop[] Status=new M3Stop[3];
        public bool[] Powered {get;private set;}=new bool[3];
        public M3State Capture()=>JsonUtility.FromJson<M3State>(JsonUtility.ToJson(Data));
        public void Restore(M3State value){value.Validate();Data=JsonUtility.FromJson<M3State>(JsonUtility.ToJson(value));}
        public void Reconcile(M1ConstructionSystem c,StoryState story){Data.robots[0].awake=story.liang;Data.robots[0].broken=c.LiangBroken;}
        public bool Fuel(Inventory stock){if(!stock.Pay(new Cost(Resource.Fuel,1)))return false;Data.generatorSeconds+=300;return true;}
        public bool Switch(int index,M3Job job,M1ConstructionSystem c)
        {
            if(index<0||index>2||!M3Rules.Switch(Data.robots[index],job))return false;
            if(index==0){c.CancelRepair();c.LiangWorking=job==M3Job.Repair&&Data.robots[0].enabled;}return true;
        }
        public bool RepairRobot(int index,Inventory stock,M1ConstructionSystem c)
        {
            if(index<0||index>2||!Data.robots[index].broken||!stock.Pay(new Cost(Resource.Parts,1)))return false;
            Data.robots[index].broken=false;if(index==0)c.LiangBroken=false;return true;
        }
        public void Advance(float dt,M1ConstructionSystem c,M2WorldSystem world,Inventory stock,bool[] reachable,bool raid)
        {
            if(dt<0||float.IsNaN(dt)||float.IsInfinity(dt)||reachable==null||reachable.Length!=3)throw new ArgumentException("Invalid job tick");
            Data.activeSeconds+=dt;Powered=M3Rules.Allocate(Data,c.Grid.Items);
            bool maintenance=Powered[2]&&Data.robots[2].job==M3Job.Energy&&reachable[2];
            Data.generatorSeconds=Mathf.Max(0,Data.generatorSeconds-dt*(maintenance?.75f:1));
            for(int i=0;i<3;i++)
            {
                var robot=Data.robots[i];
                Status[i]=!robot.awake||!robot.enabled?M3Stop.Off:robot.broken?M3Stop.Broken:!Powered[i]?M3Stop.Power:!reachable[i]?M3Stop.Path:M3Stop.Working;
                if(Status[i]!=M3Stop.Working||raid)continue;
                if(robot.job==M3Job.Repair||robot.job==M3Job.Energy||robot.job==M3Job.Watch)continue;
                robot.progress=Mathf.Min(M3Rules.JobSeconds,robot.progress+dt);if(robot.progress<M3Rules.JobSeconds)continue;
                bool done=false;
                if(robot.job==M3Job.Processing)
                {
                    if(!M3Rules.Usable(c.Grid.Items,Structure.Workbench)){Status[i]=M3Stop.NoTarget;continue;}
                    if(!stock.CanPay(new Cost(Resource.Scrap,2))){Status[i]=M3Stop.Materials;continue;}
                    done=stock.Exchange(new[]{new Cost(Resource.Scrap,2)},new Cost(Resource.Parts,2));Status[i]=done?M3Stop.Working:M3Stop.Full;
                }
                else if(robot.job==M3Job.Gathering)
                {
                    var resource=new[]{Resource.Wood,Resource.Stone,Resource.Fiber,Resource.Ration,Resource.Scrap}[robot.produced%5];
                    if(stock[resource]>=40){Status[i]=M3Stop.Full;continue;}done=stock.Add(resource,2);Status[i]=done?M3Stop.Working:M3Stop.Full;
                }
                else if(robot.job==M3Job.Gardening)
                {
                    var mature=c.Grid.Items.Where(b=>b.type==Structure.Planter&&!b.Damaged&&b.crop>=0&&b.growth>=M2WorldSystem.GrowSeconds[b.crop]).ToArray();
                    foreach(var b in mature)done|=world.Harvest(b,stock);
                    if(mature.Length>0&&!done){Status[i]=M3Stop.Full;continue;}
                    var empty=c.Grid.Items.FirstOrDefault(b=>b.type==Structure.Planter&&!b.Damaged&&b.crop<0);
                    if(empty!=null)done|=world.Plant(empty,0,stock);
                    Status[i]=done?M3Stop.Working:empty!=null?M3Stop.Materials:M3Stop.NoTarget;
                }
                if(done){robot.progress=0;robot.produced++;c.Changed();}
            }
        }
    }
}
