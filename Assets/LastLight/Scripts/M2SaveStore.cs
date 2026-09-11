using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace LastLight
{
    [Serializable] public sealed class M2NodeSave { public string id;public float ready; }
    [Serializable] public sealed class M2EnemySave
    {
        public string id;public bool raid;public Vector3 position,origin,direction;public float timer,health;public int state;public int[] hitIds;
    }
    [Serializable] public sealed class M2SaveData
    {
        public M3State m3;
        public int version=1;public string savedUtc,scene="Home";
        public int[] backpack,storage;public int pickups;
        public List<M2NodeSave> nodes=new List<M2NodeSave>();
        public List<Building> buildings=new List<Building>();
        public bool liangWorking,liangBroken,workshopVisited;public int liangRepairs;
        public float activeTime,health,hunger,exposure,stoveUntil;public int rescues;
        public StoryState story;public List<string> journal;
        public bool raidActive;public int strikes;public float raidStarted,bufferUntil;public int[] damagedEquipment;
        public Vector3 player;public float stamina=100;
        public List<M2EnemySave> enemies=new List<M2EnemySave>();
        public M2WorldData world;
        public void Validate()
        {
            if(version!=1&&version!=3)throw new InvalidDataException("Unsupported save version: "+version);
            if(version==3){if(m3==null)throw new InvalidDataException("Missing M3 state");m3.Validate();}
            if(scene!="Home"&&scene!="Workshop"&&scene!="Greenhouse"&&!(version==3&&scene=="Relay"))throw new InvalidDataException("Unknown scene");
            CheckInventory(backpack,12);CheckInventory(storage,80);
            if(story==null||world==null||world.samples==null||world.samples.Length!=3||journal==null||nodes==null||buildings==null||enemies==null||damagedEquipment==null)throw new InvalidDataException("Incomplete save");
            if(buildings.Any(b=>b==null)||buildings.Count>1600||buildings.Select(b=>b.id).Distinct().Count()!=buildings.Count)throw new InvalidDataException("Duplicate buildings");
            foreach(var b in buildings)
                if(b.id<=0||!Enum.IsDefined(typeof(Structure),b.type)||b.x< -7||b.x>7||b.z< -7||b.z>7||b.rotation<0||b.rotation>3||!Range(b.health,0,100)||b.crop< -1||b.crop>2||!Range(b.growth,0,210)||(b.crop>=0&&b.type!=Structure.Planter)||(b.occupant!=""&&b.occupant!="player"&&b.occupant!="visitor"))throw new InvalidDataException("Invalid building");
            if(!Range(health,0,100)||!Range(hunger,0,100)||!Range(exposure,0,100)||!Range(stamina,0,100)||!Range(activeTime,0,1e9f)||!Range(stoveUntil,0,1e9f)||!Range(bufferUntil,0,1e9f)||!Range(raidStarted,0,activeTime)||!Range(world.springTime,0,1e9f)||!Range(world.bridgeProgress,0,M2WorldSystem.BridgeSeconds)||!Range(world.nextThreat,0,1e9f)||!Range(world.warningAt,-1,1e9f))throw new InvalidDataException("Invalid time or survival state");
            if(!Finite(player)||Mathf.Abs(player.x)>100||Mathf.Abs(player.z)>150||Mathf.Abs(player.y)>50||story.choice<0||story.choice>3||story.promise<0||story.promise>4||strikes<0||strikes>3||rescues<0||pickups<0||liangRepairs<0||world.harvests<0||world.seasonalRaids<0)throw new InvalidDataException("Invalid progression");
            if(nodes.Any(n=>n==null||string.IsNullOrEmpty(n.id)||!Range(n.ready,0,1e9f))||nodes.Select(n=>n.id).Distinct().Count()!=nodes.Count)throw new InvalidDataException("Invalid pickup state");
            if(journal.Any(j=>j==null)||damagedEquipment.Distinct().Count()!=damagedEquipment.Length||damagedEquipment.Any(id=>!buildings.Any(b=>b.id==id)))throw new InvalidDataException("Invalid ledger references");
            if(story.promise==3&&!buildings.Any(b=>b.id==story.visitorBedId&&b.type==Structure.Bed&&b.occupant=="visitor"))throw new InvalidDataException("Missing promised bed");
            foreach(var e in enemies)if(e==null||string.IsNullOrEmpty(e.id)||!Finite(e.position)||!Finite(e.origin)||!Finite(e.direction)||!Range(e.health,-100,80)||!Range(e.timer,-100,10)||e.state<0||e.state>2||e.hitIds==null)throw new InvalidDataException("Invalid enemy");
            if(enemies.Select(e=>e.id).Distinct().Count()!=enemies.Count||raidActive&&(scene!="Home"||enemies.Count(e=>e.raid)!=1))throw new InvalidDataException("Incomplete raid");
        }
        static bool Finite(Vector3 v)=>Range(v.x,-10000,10000)&&Range(v.y,-10000,10000)&&Range(v.z,-10000,10000);
        static bool Range(float n,float min,float max)=>!float.IsNaN(n)&&!float.IsInfinity(n)&&n>=min&&n<=max;
        static void CheckInventory(int[] values,int slots){if(values==null||values.Length!=8||values.Any(n=>n<0||n>slots*Inventory.Stack)||values.Sum(n=>(n+19)/20)>slots)throw new InvalidDataException("Invalid inventory");}
    }
    [Serializable] sealed class M2SaveEnvelope {public string payload,checksum;}
    public sealed class M2SaveStore
    {
        public readonly string DirectoryPath;
        private readonly bool allowFinale;
        public M2SaveStore(string directory,bool finale=false){DirectoryPath=directory;allowFinale=finale;}
        public string PathFor(int slot){if(slot<0||slot>(allowFinale?4:3))throw new ArgumentOutOfRangeException(nameof(slot));return Path.Combine(DirectoryPath,slot==4?"before-ending.json":slot==0?"auto.json":"slot"+slot+".json");}
        static string Hash(string text){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(text)));}
        static M2SaveData Read(string path)
        {
            var envelope=JsonUtility.FromJson<M2SaveEnvelope>(File.ReadAllText(path));
            if(envelope==null||envelope.payload==null||Hash(envelope.payload)!=envelope.checksum)throw new InvalidDataException("Save checksum mismatch");
            var data=JsonUtility.FromJson<M2SaveData>(envelope.payload);if(data==null)throw new InvalidDataException("Empty save");data.Validate();return data;
        }
        public M2SaveData Load(int slot,out bool backup)
        {
            var path=PathFor(slot);backup=false;
            try{return Read(path);}catch(Exception primary) when(primary is IOException||primary is ArgumentException||primary is InvalidOperationException)
            {
                try{var data=Read(path+".bak");backup=true;return data;}
                catch(Exception secondary) when(secondary is IOException||secondary is ArgumentException||secondary is InvalidOperationException){throw new IOException("Save and backup could not be loaded. Files were preserved.",new AggregateException(primary,secondary));}
            }
        }
        public void Save(int slot,M2SaveData data)
        {
            data.Validate();Directory.CreateDirectory(DirectoryPath);string path=PathFor(slot),tmp=path+".tmp";
            data.savedUtc=DateTime.UtcNow.ToString("O");string payload=JsonUtility.ToJson(data);
            byte[] bytes=Encoding.UTF8.GetBytes(JsonUtility.ToJson(new M2SaveEnvelope{payload=payload,checksum=Hash(payload)}));
            using(var stream=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            Read(tmp);
            if(File.Exists(path))
            {
                bool valid=false;try{Read(path);valid=true;}catch(Exception ex) when(ex is IOException||ex is ArgumentException||ex is InvalidOperationException){}
                // Never promote a corrupt primary over its last valid backup.
                if(valid)File.Replace(tmp,path,path+".bak");
                else {File.Copy(path,path+".corrupt-"+DateTime.UtcNow.Ticks);File.Replace(tmp,path,null);}
            }
            else {File.Move(tmp,path);File.Copy(path,path+".bak",true);}
        }
        public bool Exists(int slot)=>File.Exists(PathFor(slot))||File.Exists(PathFor(slot)+".bak");
    }
}
