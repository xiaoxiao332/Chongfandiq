using UnityEngine;
namespace SnowVillage
{
    public sealed class SnowFootprints : MonoBehaviour
    {
        [SerializeField] private Mesh footprintMesh;
        [SerializeField] private Material footprintMaterial;
        [SerializeField] private ParticleSystem powder;
        [SerializeField] private LayerMask groundMask = 1;
        private const int Capacity = 256;
        private readonly Transform[] pool = new Transform[Capacity];
        private int cursor;
        private int totalStamps;
        public int TotalStamps => totalStamps;
        public int ActiveCount => Mathf.Min(totalStamps, Capacity);
        public void Configure(Mesh mesh, Material material, ParticleSystem puff)
        { footprintMesh = mesh; footprintMaterial = material; powder = puff; }
        private void Awake()
        {
            for (int i = 0; i < Capacity; i++)
            {
                var go = new GameObject("Footprint " + i); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = footprintMesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = footprintMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(false); pool[i] = go.transform;
            }
        }
        public void Stamp(Vector3 position, Vector3 forward, bool emitPowder)
        {
            if (!Physics.Raycast(position + Vector3.up * .65f, Vector3.down, out RaycastHit hit, 1.6f, groundMask, QueryTriggerInteraction.Ignore)) return;
            if (hit.normal.y < .7f || hit.collider.gameObject.name != "SnowGround") return;
            Transform stamp = pool[cursor]; cursor = (cursor + 1) % Capacity; totalStamps++;
            stamp.SetPositionAndRotation(hit.point + hit.normal * .012f, Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, hit.normal), hit.normal));
            stamp.gameObject.SetActive(true);
            if (emitPowder && powder != null)
            {
                var emission = new ParticleSystem.EmitParams { position = hit.point + Vector3.up * .035f };
                powder.Emit(emission, 5);
            }
        }
        public void ClearDynamic()
        { foreach (var p in pool) if (p != null) p.gameObject.SetActive(false); cursor = 0; totalStamps = 0; if (powder != null) powder.Clear(); }
    }
}
