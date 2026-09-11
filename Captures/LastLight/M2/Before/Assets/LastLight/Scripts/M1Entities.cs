using UnityEngine;
using System.Collections.Generic;

namespace LastLight
{
    public enum NodeKind { Pickup,Terminal,Depot,EmergencyStove,EmergencyBench,Pod,Liang,Core,Record,Shortcut,Travel,AidRoute }
    public sealed class M1Node:MonoBehaviour
    {
        [SerializeField] private NodeKind kind;[SerializeField] private Resource resource;[SerializeField] private int amount=3;
        [SerializeField] private string label;[SerializeField] private float respawn=180;
        private float availableAt;private Renderer[] visuals;public NodeKind Kind=>kind;public string Label=>label;
        public Resource Resource=>resource;public int Amount=>amount;public float Respawn=>respawn;
        public string StableId=>gameObject.scene.name+"/"+gameObject.name;
        public bool Available(float time)=>time>=availableAt;
        public void Configure(NodeKind k,string text,Resource r=Resource.Wood,int count=3,float seconds=180){kind=k;label=text;resource=r;amount=count;respawn=seconds;}
        private void Awake(){visuals=GetComponentsInChildren<Renderer>();}
        public void Refresh(float time){if(visuals==null)return;bool visible=Available(time);foreach(var r in visuals)r.enabled=visible;}
        public void SetReadyAt(float time){availableAt=time;}
        public void Use(M1Session s){if(Available(s.ActiveTime))s.UseNode(this);}
    }
}

