using UnityEngine;
using UnityEngine.AI;
namespace LastLight
{
    public sealed class M1Built:MonoBehaviour
    {
        public Building Data{get;private set;}private M1Session session;private Renderer[] renderers;private NavMeshObstacle[] obstacles;
        public void Initialize(Building b,M1Session s){Data=b;session=s;renderers=GetComponentsInChildren<Renderer>();
            obstacles=GetComponentsInChildren<NavMeshObstacle>();
            if(obstacles.Length==0&&b.type!=Structure.Floor){foreach(var box in GetComponents<BoxCollider>()){
                var child=new GameObject("Navigation obstacle");child.transform.SetParent(transform,false);
                var obstacle=child.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=box.center;obstacle.size=box.size;
                obstacle.carving=true;obstacle.carveOnlyStationary=false;
            }obstacles=GetComponentsInChildren<NavMeshObstacle>();}
        }
        public void Damage(float n){if(Data!=null)session.DamageBuilding(Data.id,n);}
        public void UpdateVisual(){foreach(var r in renderers){var block=new MaterialPropertyBlock();block.SetColor("_BaseColor",Data.health<=0?new Color(.14f,.18f,.2f):new Color(.49f,.34f,.24f));if(Data.Damaged)r.SetPropertyBlock(block);else r.SetPropertyBlock(null);}foreach(var c in GetComponentsInChildren<Collider>())c.enabled=Data.health>0;foreach(var obstacle in obstacles)obstacle.enabled=Data.health>0;}
    }
}

