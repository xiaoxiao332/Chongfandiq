using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.Editor
{
    public static partial class M3ChapterBuilder
    {
        private static void M3Node(List<M1Node> nodes,string id,string model,Vector3 position,int action,string zh,string en)
        {Node(nodes,id,model,position,NodeKind.M3,M3Text.Pair(zh,en),Resource.Wood,action);}
        private static void MakeRelay(List<M1Node> nodes,List<M1Beast> beasts)
        {
            Node(nodes,"relay-return","Terminal",new Vector3(0,0,-28),NodeKind.Travel,M3Text.Pair("返回家园","Return home"));
            M3Node(nodes,"relay-shou","Shou",new Vector3(-5,0,-13),4,"守与手动应急供电","Shou and emergency power");
            M3Node(nodes,"relay-archive","Terminal",new Vector3(-8,0,8),5,"导航与制造记录","Navigation and maker archives");
            M3Node(nodes,"relay-engineering","Terminal",new Vector3(0,0,24),6,"共同工程控制台","Shared engineering console");
            var tower=Model("RelayTower");tower.transform.position=new Vector3(9,0,12);
            var stage=new GameObject("Engineering stages").AddComponent<M3EngineeringVisual>();
            stage.models=new GameObject[7];var names=new[]{"LaunchStructure","CommonPower","Navigation","PersonalShip","Transport","GroundNetwork","CommonPower"};
            for(int i=0;i<names.Length;i++){stage.models[i]=Model(names[i],stage.transform);stage.models[i].transform.position=new Vector3(i==3?-10:i==4?12:0,0,32+(i<3?i*5:0));}
            for(int i=0;i<6;i++)Node(nodes,"relay-salvage-"+i,i%2==0?"Scrap":"Parts",new Vector3(16,0,-15+i*6),NodeKind.Pickup,M3Text.Pair("风险支路残件","Branch salvage"),i%2==0?Resource.Scrap:Resource.Parts,4);
            for(int i=0;i<6;i++)Cube("Relay route marker",new Vector3(-12,.03f,-20+i*7),new Vector3(.25f,.06f,.8f),materials["Warm"],false);
            Node(nodes,"relay-shortcut","Terminal",new Vector3(-13,0,29),NodeKind.Shortcut,M3Text.Pair("山脊回程捷径","Ridge return shortcut"));
        }
        private static M1Beast AddEnemy(List<M1Beast> beasts,string id,Vector3 position,M3EnemyKind kind)
        {
            var go=new GameObject(id);go.transform.position=position;
            if(kind==M3EnemyKind.Moths)
            {
                var group=new GameObject("Moth swarm");group.transform.SetParent(go.transform,false);var particles=group.AddComponent<ParticleSystem>();var main=particles.main;main.startLifetime=1.5f;main.startSpeed=.35f;main.startSize=.11f;main.maxParticles=35;main.startColor=new Color(.8f,.8f,.5f);var shape=particles.shape;shape.radius=.65f;particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=materials["Warm"];
            }
            else
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SnowVillage/Prefabs/WinterTravelerVisual.prefab"),go.transform);
                var pack=Cube("Equipment",position+new Vector3(0,1,.3f),new Vector3(.6f,.6f,.3f),materials[kind==M3EnemyKind.Shooter?"Metal":"Rust"],false,model.transform);
                if(kind==M3EnemyKind.Shooter)Cube("Launcher",position+new Vector3(.35f,1,-.3f),new Vector3(.15f,.15f,.8f),materials["Metal"],false,model.transform);
            }
            var enemy=go.AddComponent<M1Beast>();enemy.ConfigureKind(kind);var collider=go.AddComponent<CapsuleCollider>();collider.height=1.8f;collider.radius=.4f;collider.center=Vector3.up*.9f;beasts.Add(enemy);return enemy;
        }
        private static void AddM3Environment(Scene scene,bool home,bool greenhouse,bool relay,List<M1Node> nodes,List<M1Beast> beasts,Transform navigationRoot)
        {
            var seasonal=new GameObject("M3 seasons").AddComponent<M3SeasonVisual>();
            seasonal.ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Renderer>()).First(r=>r.name=="Winter snow ground");
            seasonal.seasons=new[]{Material("M3Spring",new Color(.39f,.48f,.39f)),Material("M3Summer",new Color(.46f,.5f,.32f)),Material("M3Autumn",new Color(.51f,.4f,.26f)),materials["Snow"]};
            if(home)
            {
                M3Node(nodes,"m3-console","Terminal",new Vector3(5,0,-10),0,"家园与共同工程","Home and shared engineering");
                for(int i=1;i<3;i++)
                {
                    var worker=new GameObject(i==1?"Ya worker":"Shou worker");worker.transform.position=new Vector3(i==1?-10:10,0,-15);Model(i==1?"Ya":"Shou",worker.transform);worker.AddComponent<M3Worker>().Index=i;
                }
                M3Node(nodes,"m3-jobs","Terminal",new Vector3(-9,0,-15),10,"岗位与能源","Jobs and energy");
                M3Node(nodes,"relay-road-sign","Terminal",new Vector3(23,0,-5),3,"前往中继站","Travel to relay");
            }
            else if(greenhouse)
            {
                M3Node(nodes,"ya","Ya",new Vector3(-4,0,-2),1,"芽","Ya");
                M3Node(nodes,"insulation","Core",new Vector3(4,0,9),2,"绝缘样本与线路修复","Insulation and access repair");
                M3Node(nodes,"du","Terminal",new Vector3(3,0,-10),8,"渡","Du");
                M3Node(nodes,"greenhouse-relay","Terminal",new Vector3(12,0,13),3,"中继站通路","Relay access");
                Node(nodes,"greenhouse-shortcut","Terminal",new Vector3(-11,0,11),NodeKind.Shortcut,M3Text.Pair("温室回程通道","Greenhouse return passage"));
                AddEnemy(beasts,"greenhouse-moths",new Vector3(13,0,4),M3EnemyKind.Moths);
                for(int i=0;i<4;i++)Node(nodes,"greenhouse-branch-"+i,"Fiber",new Vector3(14,0,-7+i*4),NodeKind.Pickup,M3Text.Pair("支路纤维","Branch fibre"),Resource.Fiber,4);
            }
            else if(!relay)
            {
                M3Node(nodes,"launch-plans","Terminal",new Vector3(-12,0,38),7,"发射结构资料","Launch structure plans");
                M3Node(nodes,"cen","Terminal",new Vector3(-15,0,-23),9,"岑","Cen");
                AddEnemy(beasts,"workshop-raider",new Vector3(17,0,19),M3EnemyKind.Raider);
            }
            else AddEnemy(beasts,"relay-shooter",new Vector3(15,0,9),M3EnemyKind.Shooter);
            // Late-added static props belong to this scene's surface; dynamic actors never do.
            foreach(var root in scene.GetRootGameObjects())
                if(root.GetComponent<M1Node>()!=null&&root.transform.parent==null)root.transform.SetParent(navigationRoot,true);
        }
    }
}
