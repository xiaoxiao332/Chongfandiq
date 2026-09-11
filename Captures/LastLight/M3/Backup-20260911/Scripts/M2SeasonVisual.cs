using UnityEngine;
namespace LastLight
{
    public sealed class M2SeasonVisual:MonoBehaviour
    {
        public GameObject winter,spring,brokenBridge,repairedBridge;
        public Renderer ground;public Material springGround;
        private Material winterGround;
        public void Apply(bool isSpring,bool repaired)
        {
            if(winter!=null)winter.SetActive(!isSpring);if(spring!=null)spring.SetActive(isSpring);
            if(brokenBridge!=null)brokenBridge.SetActive(!repaired);if(repairedBridge!=null)repairedBridge.SetActive(repaired);
            if(ground!=null){if(winterGround==null)winterGround=ground.sharedMaterial;ground.sharedMaterial=isSpring?springGround:winterGround;}
        }
    }
}
