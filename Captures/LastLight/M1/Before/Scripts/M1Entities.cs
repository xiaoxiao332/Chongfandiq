using UnityEngine;
using System.Collections.Generic;

namespace LastLight
{
    public enum NodeKind { Pickup,Terminal,Depot,EmergencyStove,EmergencyBench,Pod,Liang,Core,Record,Shortcut }
    public sealed class M1Node:MonoBehaviour
    {
        [SerializeField] private NodeKind kind;[SerializeField] private Resource resource;[SerializeField] private int amount=3;
        [SerializeField] private string label;[SerializeField] private float respawn=180;
        private float availableAt;private Renderer[] visuals;public NodeKind Kind=>kind;public string Label=>label;
        public bool Available(float time)=>time>=availableAt;
        public void Configure(NodeKind k,string text,Resource r=Resource.Wood,int count=3,float seconds=180){kind=k;label=text;resource=r;amount=count;respawn=seconds;}
        private void Awake(){visuals=GetComponentsInChildren<Renderer>();}
        public void Refresh(float time){if(visuals==null)return;bool visible=Available(time);foreach(var r in visuals)r.enabled=visible;}
        public void Use(M1Session s){if(!Available(s.ActiveTime))return;
            if(kind==NodeKind.Pickup){if(!s.Backpack.Add(resource,amount)){s.Notify("背包已满：回公共仓储存放，或留下空间再拾取。");return;}availableAt=s.ActiveTime+respawn;Refresh(s.ActiveTime);s.Notify(Catalog.ResourceNames[(int)resource]+" +"+amount);s.Sound(0);s.Pickups++;}
            else s.UseNode(this);
        }
    }
    public sealed class M1Built:MonoBehaviour
    {
        public Building Data{get;private set;}private M1Session session;private Renderer[] renderers;
        public void Initialize(Building b,M1Session s){Data=b;session=s;renderers=GetComponentsInChildren<Renderer>();}
        public void Damage(float n){if(Data==null||Data.type==Structure.Floor||Catalog.Edge(Data.type))return;
            if(session.RaidActive&&!session.TryRecordDamage(Data.id))return;Data.health=Mathf.Max(0,Data.health-n);UpdateVisual();}
        public void UpdateVisual(){foreach(var r in renderers){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",Data.health<=0?new Color(.14f,.18f,.2f):Data.Damaged?new Color(.49f,.34f,.24f):Color.white);if(Data.Damaged)r.SetPropertyBlock(block);else r.SetPropertyBlock(null);}}
    }
    public sealed class M1Beast:MonoBehaviour
    {
        private M1Session session;private bool raid;private Vector3 origin,direction;private float timer,health=80;private int state;
        private Transform visual;private LineRenderer warning;private readonly HashSet<int> hitIds=new HashSet<int>();
        public bool Active=>health>0;public bool Raid=>raid;
        public void Initialize(M1Session s,bool attackHome,Transform model){session=s;raid=attackHome;origin=transform.position;visual=model;
            var line=new GameObject("冲撞预警");line.transform.SetParent(transform,false);warning=line.AddComponent<LineRenderer>();warning.sharedMaterial=s.SignalMaterial;warning.positionCount=5;warning.widthMultiplier=.06f;warning.loop=false;warning.enabled=false;}
        public void Hit(float damage){if(health<=0)return;health-=damage;session.Sound(2);if(health<=0){session.Notify(raid?"枝角兽退却了。":"枝角兽退入林间；工程件不需要战利品。");warning.enabled=false;gameObject.SetActive(false);}}
        private void Update(){if(session==null||session.Paused||!Active)return;float dt=Time.deltaTime;timer-=dt;
            Vector3 target=raid?session.RaidTarget:session.Actor.transform.position;target.y=0;Vector3 p=transform.position;p.y=0;
            float distance=Vector3.Distance(p,target);if(!raid&&distance>11){warning.enabled=false;state=0;Move(origin,dt*.6f);return;}
            if(state==0){Move(target,dt);if(distance<7&&timer<=0){state=1;timer=1.25f;direction=(target-p).normalized;transform.rotation=Quaternion.LookRotation(direction);warning.enabled=true;Vector3 side=Vector3.Cross(Vector3.up,direction)*.65f;Vector3 start=p+Vector3.up*.15f;warning.SetPositions(new[]{start-side,start+direction*6-side,start+direction*6+side,start+side,start-side});session.Sound(3);}}
            else if(state==1){if(timer<=0){state=2;timer=1.2f;hitIds.Clear();warning.enabled=false;}}
            else if(state==2){
                Vector3 next=transform.position+direction*6*dt;
                if(Physics.SphereCast(transform.position+Vector3.up*.65f,.43f,direction,out var obstruction,6*dt+.1f,~0,QueryTriggerInteraction.Ignore)&&obstruction.collider.GetComponentInParent<M1Actor>()==null&&obstruction.collider.GetComponentInParent<M1Beast>()!=this){var b=obstruction.collider.GetComponentInParent<M1Built>();if(b!=null){b.Damage(session.Story.reinforced?18:40);if(b.Data.health<=0&&b.Data.type==Structure.Barricade)foreach(var c in b.GetComponents<Collider>())c.enabled=false;}timer=0;}else transform.position=next;
                if(Vector3.Distance(transform.position,session.Actor.transform.position)<1.4f&&hitIds.Add(0))session.TakeDamage(23);
                if(raid&&Vector3.Distance(transform.position,session.RaidTarget)<1.6f&&hitIds.Add(-1)){session.RaidStrikes++;session.Notify("物资区遭到冲撞（"+session.RaidStrikes+"/3）。牵制枝角兽或保护入口。");}
                if(timer<=0){state=0;timer=2;}
            }
            if(visual!=null){visual.localPosition=new Vector3(0,Mathf.Sin(session.ActiveTime*(state==2?18:8))*(state==1?.015f:.045f),0);visual.localRotation=Quaternion.Euler(state==1?10:0,0,0);}
        }
        private void Move(Vector3 target,float dt){var d=target-transform.position;d.y=0;if(d.magnitude<1.1f)return;d.Normalize();
            if(Physics.SphereCast(transform.position+Vector3.up*.7f,.42f,d,out var obstacle,1.1f,~0,QueryTriggerInteraction.Ignore)&&obstacle.collider.GetComponentInParent<M1Actor>()==null&&obstacle.collider.GetComponentInParent<M1Beast>()!=this){
                var built=obstacle.collider.GetComponentInParent<M1Built>();if(raid&&built!=null){if(timer<=0){built.Damage(25);timer=1.8f;if(built.Data.health<=0)foreach(var c in built.GetComponents<Collider>())c.enabled=false;}return;}
                var side=Vector3.Cross(Vector3.up,d);if(!Physics.SphereCast(transform.position+Vector3.up*.7f,.42f,side,out _,1,~0,QueryTriggerInteraction.Ignore))d=side;else return;}
            transform.position+=d*1.5f*dt;transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),220*dt);}
    }
}
