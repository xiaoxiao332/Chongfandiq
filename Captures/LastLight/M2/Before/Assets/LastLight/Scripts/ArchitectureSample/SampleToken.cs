using UnityEngine;

namespace LastLight
{
    public sealed class SampleToken : MonoBehaviour, IPoolable
    {
        public void OnRent() { transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; transform.localScale = Vector3.one; }
        public void OnReturn() { transform.localPosition = Vector3.zero; transform.localRotation = Quaternion.identity; }
        private void Update() { transform.Rotate(0, 60 * Time.deltaTime, 0); }
    }
}
