using UnityEngine;
using UnityEngine.InputSystem;

namespace SnowVillage
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SnowTraveler : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private SnowFootprints footprints;
        [SerializeField] private Vector3[] route;
        [SerializeField] private float walkSpeed = 1.05f;
        private CharacterController motor;
        private InputActionAsset ownedActions;
        private InputAction move;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private int waypoint;
        private float verticalSpeed;
        private float actualSpeed;
        private float smoothedSpeed;
        private bool automatic = true;
        private bool previousBackgroundMode;
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        public bool Automatic => automatic;
        public float ActualSpeed => actualSpeed;
        public bool Grounded => motor != null && motor.isGrounded;
        public void Configure(Animator a, Camera c, InputActionAsset input, SnowFootprints f, Vector3[] points)
        { animator = a; sceneCamera = c; inputActions = input; footprints = f; route = points; }
        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            previousBackgroundMode = Application.runInBackground;
            Application.runInBackground = true;
            if (sceneCamera == null) sceneCamera = Camera.main;
            if (!footprints.gameObject.scene.IsValid()) footprints = Instantiate(footprints);
            initialPosition = transform.position; initialRotation = transform.rotation;
            ownedActions = Instantiate(inputActions); move = ownedActions.FindAction("Player/Move", true);
        }
        private void OnEnable() { if (move != null) move.Enable(); }
        private void OnDisable() { if (move != null) move.Disable(); }
        private void OnDestroy() { if (ownedActions != null) Destroy(ownedActions); Application.runInBackground = previousBackgroundMode; }
        public void SetAutomatic(bool value)
        {
            automatic = value;
            if (value && route != null && route.Length > 0)
            {
                float best = float.MaxValue;
                for (int i = 0; i < route.Length; i++)
                { float d = (route[i] - transform.position).sqrMagnitude; if (d < best) { best = d; waypoint = i; } }
            }
        }
        public void ResetTraveler()
        {
            motor.enabled = false; transform.SetPositionAndRotation(initialPosition, initialRotation); motor.enabled = true;
            verticalSpeed = 0; actualSpeed = 0; smoothedSpeed = 0; waypoint = 0; automatic = true;
            footprints.ClearDynamic(); animator.Rebind(); animator.Update(0);
        }
        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) { ResetTraveler(); return; }
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) SetAutomatic(!automatic);
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1);
            if (input.sqrMagnitude > .01f) automatic = false;
            Vector3 direction;
            if (automatic && route != null && route.Length > 0)
            {
                Vector3 delta = route[waypoint] - transform.position; delta.y = 0;
                if (delta.magnitude < .25f) { waypoint = (waypoint + 1) % route.Length; delta = route[waypoint] - transform.position; delta.y = 0; }
                direction = delta.normalized;
            }
            else
            {
                Vector3 right = sceneCamera.transform.right; right.y = 0;
                Vector3 forward = sceneCamera.transform.forward; forward.y = 0;
                direction = right.normalized * input.x + forward.normalized * input.y;
            }
            MoveWorld(direction, Time.deltaTime);
        }
        public void MoveWorld(Vector3 direction, float deltaTime)
        {
            if (deltaTime <= 0 || !motor.enabled) return;
            direction.y = 0; direction = Vector3.ClampMagnitude(direction, 1);
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 300 * deltaTime);
            verticalSpeed = motor.isGrounded ? -2 : Mathf.Max(-20, verticalSpeed - 15 * deltaTime);
            Vector3 before = transform.position;
            motor.Move((direction * walkSpeed + Vector3.up * verticalSpeed) * deltaTime);
            Vector3 traveled = transform.position - before; traveled.y = 0;
            actualSpeed = traveled.magnitude / deltaTime;
            smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, actualSpeed, 5 * deltaTime);
            animator.SetFloat(SpeedId, smoothedSpeed);
        }
        public void Footstep(int side, Transform foot)
        {
            if (actualSpeed < .12f || !Grounded || foot == null) return;
            footprints.Stamp(foot.position, transform.forward, true);
        }
    }
}
