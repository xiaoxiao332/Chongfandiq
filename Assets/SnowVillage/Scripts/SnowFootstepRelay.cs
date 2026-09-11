using UnityEngine;
namespace SnowVillage
{
    public sealed class SnowFootstepRelay : MonoBehaviour
    {
        [SerializeField] private SnowTraveler traveler;
        [SerializeField] private Transform leftFoot;
        [SerializeField] private Transform rightFoot;
        public void Configure(SnowTraveler owner, Transform left, Transform right)
        { traveler = owner; leftFoot = left; rightFoot = right; }
        public void Footstep(int side) { if (traveler != null) traveler.Footstep(side, side == 0 ? leftFoot : rightFoot); }
    }
}
