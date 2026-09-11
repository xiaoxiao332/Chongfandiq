using UnityEngine;
using UnityEngine.InputSystem;
namespace LastLight
{
    public sealed class M1Camera:MonoBehaviour
    {
        [SerializeField] private Transform target;private M1Session session;private Camera cam;private Vector3 center;
        private readonly Vector3 offset=new Vector3(15,26,-23);
        public void Configure(Transform t){target=t;center=t.position;}
        public void Initialize(M1Session s){session=s;cam=GetComponent<Camera>();}
        private void LateUpdate(){if(target==null||session==null)return;
            if(session.BuildingMode){var k=Keyboard.current;Vector3 delta=Vector3.zero;if(k!=null){if(k.leftArrowKey.isPressed)delta.x--;if(k.rightArrowKey.isPressed)delta.x++;if(k.upArrowKey.isPressed)delta.z++;if(k.downArrowKey.isPressed)delta.z--;}
                center+=delta*12*Time.unscaledDeltaTime;center.x=Mathf.Clamp(center.x,-14,14);center.z=Mathf.Clamp(center.z,-14,14);
                if(Mouse.current!=null)cam.orthographicSize=Mathf.Clamp(cam.orthographicSize-Mouse.current.scroll.ReadValue().y*.01f,7,19);
            }else{center=Vector3.Lerp(center,target.position,1-Mathf.Exp(-6*Time.unscaledDeltaTime));cam.orthographicSize=Mathf.Lerp(cam.orthographicSize,9,Time.unscaledDeltaTime*3);}
            transform.position=center+offset;transform.rotation=Quaternion.LookRotation(-offset);
        }
    }
}

