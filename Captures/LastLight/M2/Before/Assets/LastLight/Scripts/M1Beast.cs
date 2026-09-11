using UnityEngine;
using System.Collections.Generic;
namespace LastLight
{
    public sealed class M1Beast:MonoBehaviour
    {
        private M1Session session;private bool raid;private Vector3 origin,direction;private float timer,health=80;private int state;
        private M1Navigator navigation;private Transform visual;private LineRenderer warning;private readonly HashSet<int> hitIds=new HashSet<int>();
        public bool Active=>health>0;public bool Raid=>raid;
        public void Initialize(M1Session s,bool attackHome,Transform model){session=s;raid=attackHome;origin=transform.position;visual=model;health=80;timer=0;state=0;hitIds.Clear();navigation=GetComponent<M1Navigator>()??gameObject.AddComponent<M1Navigator>();navigation.Initialize(s,1.5f);
            if(warning==null){var line=new GameObject("冲撞预警");line.transform.SetParent(transform,false);warning=line.AddComponent<LineRenderer>();}warning.sharedMaterial=s.SignalMaterial;warning.positionCount=5;warning.widthMultiplier=.06f;warning.loop=false;warning.enabled=false;}
        public void Hit(float damage){if(health<=0)return;health-=damage;session.Sound(2);if(health<=0){session.Notify(raid?"枝角兽退却了。":"枝角兽退入林间；工程件不需要战利品。");warning.enabled=false;gameObject.SetActive(false);}}
        private void Update(){if(session==null||!Active)return;if(session.Paused){navigation?.Stop();return;}float dt=Time.deltaTime;timer-=dt;
            Vector3 target=raid?session.RaidTarget:session.Actor.transform.position;target.y=0;Vector3 p=transform.position;p.y=0;
            float distance=Vector3.Distance(p,target);if(!raid&&distance>11){warning.enabled=false;state=0;Move(origin,dt*.6f);return;}
            if(state==0){Move(target,dt);if(distance<7&&timer<=0){state=1;navigation.Stop();timer=1.25f;direction=(target-p).normalized;transform.rotation=Quaternion.LookRotation(direction);warning.enabled=true;Vector3 side=Vector3.Cross(Vector3.up,direction)*.65f;Vector3 start=p+Vector3.up*.15f;warning.SetPositions(new[]{start-side,start+direction*6-side,start+direction*6+side,start+side,start-side});session.Sound(3);}}
            else if(state==1){if(timer<=0){state=2;timer=1.2f;hitIds.Clear();warning.enabled=false;}}
            else if(state==2){
                Vector3 next=transform.position+direction*6*dt;
                if(Physics.SphereCast(transform.position+Vector3.up*.65f,.43f,direction,out var obstruction,6*dt+.1f,~0,QueryTriggerInteraction.Ignore)&&obstruction.collider.GetComponentInParent<M1Actor>()==null&&obstruction.collider.GetComponentInParent<M1Beast>()!=this){var b=obstruction.collider.GetComponentInParent<M1Built>();if(b!=null){b.Damage(session.Story.reinforced?18:40);if(b.Data.health<=0&&b.Data.type==Structure.Barricade)foreach(var c in b.GetComponents<Collider>())c.enabled=false;}timer=0;}else if(navigation.Available)navigation.Agent.Move(next-transform.position);
                if(Vector3.Distance(transform.position,session.Actor.transform.position)<1.4f&&hitIds.Add(0))session.TakeDamage(23);
                if(raid&&Vector3.Distance(transform.position,session.RaidTarget)<1.6f&&hitIds.Add(-1)){session.RecordRaidStrike();session.Notify("物资区遭到冲撞（"+session.RaidStrikes+"/3）。牵制枝角兽或保护入口。");}
                if(timer<=0){state=0;timer=2;}
            }
            if(visual!=null){visual.localPosition=new Vector3(0,Mathf.Sin(session.ActiveTime*(state==2?18:8))*(state==1?.015f:.045f),0);visual.localRotation=Quaternion.Euler(state==1?10:0,0,0);}
        }
        private void Move(Vector3 target,float dt)
        {
            if(navigation==null||!navigation.Available)return;
            navigation.Go(target);
            // Target selection for destructive attacks; pathfinding remains owned by NavMeshAgent.
            if(!raid||timer>0)return;
            var direction=target-transform.position;direction.y=0;
            if(direction.sqrMagnitude<.01f)return;
            if(Physics.SphereCast(transform.position+Vector3.up*.65f,.43f,direction.normalized,out var obstacle,1.3f,~0,QueryTriggerInteraction.Ignore))
            {
                var built=obstacle.collider.GetComponentInParent<M1Built>();
                if(built!=null&&built.Data.health>0){navigation.Stop();built.Damage(25);timer=1.8f;}
            }
        }
    }
}
