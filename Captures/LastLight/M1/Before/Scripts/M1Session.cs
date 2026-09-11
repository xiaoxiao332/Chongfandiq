using UnityEngine;

namespace LastLight
{
    /// <summary>
    /// Required integration contract for the M1 actor, camera and world entities.
    /// The concrete session and scene composition are still pending development.
    /// Keep operations abstract so incomplete gameplay cannot silently succeed.
    /// </summary>
    public abstract class M1Session : MonoBehaviour
    {
        public abstract bool Paused { get; }
        public abstract bool BuildingMode { get; }
        public abstract float ActiveTime { get; }
        public abstract Inventory Backpack { get; }
        public abstract StoryState Story { get; }
        public abstract M1Actor Actor { get; }
        public abstract IM1SessionUI UI { get; }
        public abstract Material SignalMaterial { get; }
        public abstract bool RaidActive { get; }
        public abstract Vector3 RaidTarget { get; }
        public abstract int Pickups { get; set; }
        public abstract int RaidStrikes { get; set; }

        public abstract void ToggleBuild();
        public abstract void Eat();
        public abstract void InteractNearest();
        public abstract void UseNode(M1Node node);
        public abstract void Melee(Vector3 position, Vector3 forward);
        public abstract void TakeDamage(float damage);
        public abstract bool TryRecordDamage(int buildingId);
        public abstract void Notify(string message);
        public abstract void Sound(int cue);
        public abstract void Footprint(Vector3 position, Vector3 forward);
    }

    /// <summary>UI operations required by M1Actor; implemented by the future session UI.</summary>
    public interface IM1SessionUI
    {
        void OpenInventory();
        void OpenJournal();
        void OpenPause();
    }
}
