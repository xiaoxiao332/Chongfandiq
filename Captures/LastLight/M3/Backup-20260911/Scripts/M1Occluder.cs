using UnityEngine;
using UnityEngine.Rendering;

namespace LastLight
{
    // Each instance owns its translucent materials; source assets are never modified.
    public sealed class M1Occluder : MonoBehaviour
    {
        private Transform target;
        private Camera view;
        private Renderer[] renderers;
        private Material[][] original,transparent;
        private float alpha=1;
        private bool swapped;
        public void Bind(Transform player,Camera camera){target=player;view=camera;}
        private void Awake()
        {
            renderers=GetComponentsInChildren<Renderer>();original=new Material[renderers.Length][];transparent=new Material[renderers.Length][];
            for(int i=0;i<renderers.Length;i++)
            {
                original[i]=renderers[i].sharedMaterials;transparent[i]=new Material[original[i].Length];
                for(int j=0;j<original[i].Length;j++)
                {
                    var material=new Material(original[i][j]);transparent[i][j]=material;
                    material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=(int)RenderQueue.Transparent;
                }
            }
        }
        private void LateUpdate()
        {
            if(target==null||view==null)return;
            var to=target.position+Vector3.up;var origin=view.transform.position;var direction=to-origin;var ray=new Ray(origin,direction.normalized);bool obscured=false;
            foreach(var renderer in renderers)if(renderer.bounds.IntersectRay(ray,out var distance)&&distance<direction.magnitude){obscured=true;break;}
            alpha=Mathf.MoveTowards(alpha,obscured?.16f:1,Time.unscaledDeltaTime*4);
            bool fade=alpha<.999f;
            if(fade!=swapped){for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=fade?transparent[i]:original[i];swapped=fade;}
            if(fade)for(int i=0;i<transparent.Length;i++)for(int j=0;j<transparent[i].Length;j++){var color=original[i][j].GetColor("_BaseColor");color.a=alpha;transparent[i][j].SetColor("_BaseColor",color);}
        }
        private void OnDisable(){target=null;view=null;alpha=1;if(renderers!=null)for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=original[i];swapped=false;}
        private void OnDestroy(){if(transparent!=null)foreach(var row in transparent)foreach(var material in row)Destroy(material);}
    }
}
