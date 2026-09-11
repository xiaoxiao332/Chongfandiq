using System;
using System.Linq;
using UnityEngine;

namespace LastLight
{
    public sealed partial class M1EconomySystem
    {
        public void Capture(M2SaveData d){d.backpack=Enum.GetValues(typeof(Resource)).Cast<Resource>().Select(r=>Backpack[r]).ToArray();d.storage=Enum.GetValues(typeof(Resource)).Cast<Resource>().Select(r=>Storage[r]).ToArray();d.pickups=Pickups;d.nodes=NodeReady.Select(p=>new M2NodeSave{id=p.Key,ready=p.Value}).ToList();}
        public void Restore(M2SaveData d){Backpack.Clear();Storage.Clear();for(int i=0;i<8;i++){Backpack.Add((Resource)i,d.backpack[i]);Storage.Add((Resource)i,d.storage[i]);}Pickups=d.pickups;NodeReady.Clear();foreach(var n in d.nodes)NodeReady.Add(n.id,n.ready);}
    }
    public sealed partial class M1ConstructionSystem
    {
        public void Capture(M2SaveData d){d.buildings=Grid.Items.Select(b=>JsonUtility.FromJson<Building>(JsonUtility.ToJson(b))).ToList();d.liangWorking=LiangWorking;d.liangBroken=LiangBroken;d.liangRepairs=LiangRepairs;}
        public void Restore(M2SaveData d){CancelRepair();Grid.RestoreBuildings(d.buildings.Select(b=>JsonUtility.FromJson<Building>(JsonUtility.ToJson(b))));LiangWorking=d.liangWorking;LiangBroken=d.liangBroken;LiangRepairs=d.liangRepairs;Changed();}
    }
    public sealed partial class M1SurvivalSystem
    {
        public void Capture(M2SaveData d){d.activeTime=ActiveTime;d.health=Health;d.hunger=Hunger;d.exposure=Exposure;d.stoveUntil=StoveUntil;d.rescues=Rescues;}
        public void Restore(M2SaveData d){ActiveTime=d.activeTime;Health=d.health;Hunger=d.hunger;Exposure=d.exposure;StoveUntil=d.stoveUntil;Rescues=d.rescues;}
    }
    public sealed partial class M1StorySystem
    {
        public void Capture(M2SaveData d){d.story=JsonUtility.FromJson<StoryState>(JsonUtility.ToJson(State));d.journal=Journal.ToList();d.workshopVisited=WorkshopVisited;}
        public void Restore(M2SaveData d){JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(d.story),State);Journal.Clear();Journal.AddRange(d.journal);WorkshopVisited=d.workshopVisited;}
    }
    public sealed partial class M1ThreatSystem
    {
        public void Capture(M2SaveData d){d.raidActive=Active;d.strikes=Strikes;d.raidStarted=StartedAt;d.bufferUntil=BufferUntil;d.damagedEquipment=DamagedEquipment.ToArray();}
        public void Restore(M2SaveData d){Active=d.raidActive;Strikes=d.strikes;StartedAt=d.raidStarted;BufferUntil=d.bufferUntil;DamagedEquipment.Clear();foreach(var id in d.damagedEquipment)DamagedEquipment.Add(id);}
        public bool StartSeasonal(float now){if(Active||now<BufferUntil)return false;Active=true;Strikes=0;StartedAt=now;DamagedEquipment.Clear();return true;}
        public bool FinishSeasonal(bool failed,float now,Inventory stock){if(!Active)return false;Active=false;BufferUntil=now+1800;if(failed)stock.Lose(.1f,5);return true;}
    }
    public sealed partial class M1Beast
    {
        public M2EnemySave Capture(string id)=>new M2EnemySave{id=id,raid=raid,position=transform.position,origin=origin,direction=direction,timer=timer,health=health,state=state,hitIds=hitIds.ToArray()};
        public void Restore(M2EnemySave d)
        {
            origin=d.origin;direction=d.direction;timer=d.timer;health=d.health;state=d.state;hitIds.Clear();foreach(var id in d.hitIds)hitIds.Add(id);
            navigation.Agent.Warp(d.position);gameObject.SetActive(health>0);
            warning.enabled=health>0&&state==1;
            if(state==1){var side=Vector3.Cross(Vector3.up,direction)*.65f;var start=d.position+Vector3.up*.15f;warning.SetPositions(new[]{start-side,start+direction*6-side,start+direction*6+side,start+side,start-side});}
        }
    }
    public sealed partial class M1Actor
    {
        public void RestoreStamina(float value){Stamina=value;}
    }
}
