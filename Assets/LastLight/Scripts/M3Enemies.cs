using UnityEngine;
using System.Linq;

namespace LastLight
{
    public enum M3EnemyKind { Beast, Moths, Raider, Shooter }
    public sealed partial class M1Beast
    {
        [SerializeField] private M3EnemyKind kind;
        public M3EnemyKind Kind=>kind;
        public void ConfigureKind(M3EnemyKind value){kind=value;}
        private void TickM3Enemy(float dt)
        {
            timer-=dt;
            var target=raid?session.RaidTarget:session.Actor.transform.position;
            var difference=target-transform.position;difference.y=0;float distance=difference.magnitude;
            if(!raid&&distance>14){state=0;warning.enabled=false;Move(origin,dt);return;}
            float range=kind==M3EnemyKind.Shooter?10:kind==M3EnemyKind.Moths?5:2;
            if(state==0)
            {
                if(kind==M3EnemyKind.Shooter&&distance<4){var retreat=transform.position-difference.normalized*3;if(UnityEngine.AI.NavMesh.SamplePosition(retreat,out var pos,2,UnityEngine.AI.NavMesh.AllAreas))navigation.Go(pos.position);}
                else if(distance>range)Move(target,dt);else navigation.Stop();
                if(distance<=range&&timer<=0)
                {
                    state=1;timer=kind==M3EnemyKind.Shooter?1.4f:1;direction=difference.normalized;
                    if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);
                    warning.enabled=true;var p=transform.position+Vector3.up*.15f;var side=Vector3.Cross(Vector3.up,direction)*.4f;
                    warning.SetPositions(new[]{p-side,p+direction*range-side,p+direction*range+side,p+side,p-side});session.Sound(3);
                }
            }
            else if(state==1)
            {
                navigation.Stop();if(timer>0)return;warning.enabled=false;state=2;timer=kind==M3EnemyKind.Moths?.6f:.2f;hitIds.Clear();
                if(kind==M3EnemyKind.Shooter)
                {
                    var from=transform.position+Vector3.up*.9f;
                    foreach(var hit in Physics.RaycastAll(from,direction,12,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
                    {
                        if(hit.collider.GetComponentInParent<M1Beast>()==this)continue;
                        var player=hit.collider.GetComponentInParent<M1Actor>();var built=hit.collider.GetComponentInParent<M1Built>();
                        if(player!=null)session.TakeDamage(17);else if(built!=null)built.Damage(18);break;
                    }
                }
                else if(kind==M3EnemyKind.Raider)
                {
                    if(Vector3.Distance(transform.position,session.Actor.transform.position)<2.1f)session.TakeDamage(16);
                    if(raid&&distance<2.1f)session.RecordRaidStrike();
                }
            }
            else
            {
                if(kind==M3EnemyKind.Moths)
                {
                    if(navigation.Available)navigation.Agent.Move(direction*4*dt);
                    if(Vector3.Distance(transform.position,session.Actor.transform.position)<1.5f&&hitIds.Add(0))session.TakeDamage(12);
                    if(raid&&Vector3.Distance(transform.position,session.RaidTarget)<1.6f&&hitIds.Add(-1))session.RecordRaidStrike();
                }
                if(timer<=0){state=0;timer=2;}
            }
            var animator=visual!=null?visual.GetComponentInChildren<Animator>():null;if(animator!=null)animator.SetFloat("Speed",state==0&&distance>range?1:0);
        }
    }
}
