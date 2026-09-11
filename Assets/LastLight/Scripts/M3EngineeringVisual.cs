using UnityEngine;
namespace LastLight
{
    public sealed class M3EngineeringVisual:MonoBehaviour
    {
        public GameObject[] models;private M1Director director;private float next;
        private void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.5f;
            if(director==null)director=FindFirstObjectByType<M1Director>();
            if(director==null||!director.IsM3||director.Global==null||director.Current==null||!director.Current.Ready)return;
            var s=director.M3.Data;var visible=new[]{s.structure,s.power,s.navigation,s.personalShip,s.transport,s.ending==M3Ending.Light,s.collectivePower};
            for(int i=0;i<models.Length;i++)models[i].SetActive(visible[i]);
        }
    }
}
