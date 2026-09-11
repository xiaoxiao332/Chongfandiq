using UnityEngine;
namespace LastLight
{
    public sealed class M2FarmVisual:MonoBehaviour
    {
        public GameObject cover;public GameObject[] stages;
        public void Apply(Building b){if(b==null)return;if(cover!=null)cover.SetActive(b.protectedCrop);for(int i=0;i<stages.Length;i++)stages[i].SetActive(b.crop>=0&&i==b.crop*3+Mathf.Min(2,Mathf.FloorToInt(b.growth/M2WorldSystem.GrowSeconds[b.crop]*3)));}
    }
}
