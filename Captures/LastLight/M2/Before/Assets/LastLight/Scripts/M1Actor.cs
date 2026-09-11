using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace LastLight
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class M1Actor:MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputs;
        [SerializeField] private Animator animator;
        [SerializeField] private Camera view;
        private InputActionAsset actions;private InputAction move,sprint,attack,interact;
        private CharacterController motor;private M1Session session;private float fall,attackTimer,stepTimer;private bool hit;
        private Transform arm;public float Stamina{get;private set;}=100;
        public Vector3 Home{get;private set;}public float Speed{get;private set;}
        public void Configure(InputActionAsset input,Animator a,Camera c){inputs=input;animator=a;view=c;}
        public void Initialize(M1Session owner){session=owner;if(actions==null){if(inputs==null||animator==null||view==null)throw new System.InvalidOperationException(name+": 缺少角色输入、动画或相机");actions=Instantiate(inputs);move=actions.FindAction("Player/Move",true);sprint=actions.FindAction("Player/Sprint",true);attack=actions.FindAction("Player/Attack",true);interact=actions.FindAction("Player/Interact",true);foreach(var t in animator.GetComponentsInChildren<Transform>())if(t.name=="UpperArm.R")arm=t;}actions.FindActionMap("Player").Enable();}
        private void Awake(){motor=GetComponent<CharacterController>();Home=transform.position;}
        private void OnDisable(){actions?.Disable();Speed=0;}
        private void OnDestroy(){if(actions!=null)Destroy(actions);}
        private void Update()
        {
            if(session==null)return;
            if(session.Paused||(session is M1GameSession game&&game.Resting)){animator.SetFloat("Speed",0);Speed=0;return;}
            float dt=Time.deltaTime;Stamina=Mathf.Min(100,Stamina+dt*16);
            var k=Keyboard.current;
            if(k!=null&&k.fKey.wasPressedThisFrame)session.Eat();
            if(interact.WasPressedThisFrame()|| (k!=null&&k.eKey.wasPressedThisFrame))session.InteractNearest();
            bool running=sprint.IsPressed()&&Stamina>4&&move.ReadValue<Vector2>().sqrMagnitude>.01f;
            Vector2 v=Vector2.ClampMagnitude(move.ReadValue<Vector2>(),1);Vector3 right=view.transform.right;right.y=0;Vector3 forward=view.transform.forward;forward.y=0;
            Vector3 direction=right.normalized*v.x+forward.normalized*v.y;
            float speed=running?3.8f:2.1f;if(running)Stamina=Mathf.Max(0,Stamina-dt*24);
            if(attackTimer>0){attackTimer-=dt;speed*=.25f;if(!hit&&attackTimer<.35f){hit=true;session.Melee(transform.position,transform.forward);}}
            if(attack.WasPressedThisFrame()&&attackTimer<=0&&Stamina>=18&&(EventSystem.current==null||!EventSystem.current.IsPointerOverGameObject())){
                if(Mouse.current!=null){var ray=view.ScreenPointToRay(Mouse.current.position.ReadValue());if(new Plane(Vector3.up,transform.position).Raycast(ray,out float distance)){var d=ray.GetPoint(distance)-transform.position;d.y=0;if(d.sqrMagnitude>.1f)transform.rotation=Quaternion.LookRotation(d);}}
                attackTimer=.75f;hit=false;Stamina-=18;session.Sound(2);}
            if(direction.sqrMagnitude>.001f&&attackTimer<=0)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),540*dt);
            fall=motor.isGrounded?-2:Mathf.Max(-20,fall-20*dt);var before=transform.position;
            motor.Move((direction*speed+Vector3.up*fall)*dt);Speed=new Vector2(transform.position.x-before.x,transform.position.z-before.z).magnitude/Mathf.Max(dt,.001f);
            animator.SetFloat("Speed",Speed);animator.speed=Speed>.1f?Mathf.Clamp(Speed/1.05f,.8f,3.6f):1;
            if(Speed>.1f&&motor.isGrounded){stepTimer-=dt;if(stepTimer<=0){stepTimer=.58f/Speed;session.Footprint(transform.position,transform.forward);}}
        }
        private void LateUpdate(){if(session!=null&&!session.Paused&&arm!=null&&attackTimer>0)arm.localRotation*=Quaternion.Euler(-100*Mathf.Sin((.75f-attackTimer)/.75f*Mathf.PI),0,0);}
        public void Teleport(Vector3 p){motor.enabled=false;transform.position=p;motor.enabled=true;fall=0;attackTimer=0;}
    }
}

