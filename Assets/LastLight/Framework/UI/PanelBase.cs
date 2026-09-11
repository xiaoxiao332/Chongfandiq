using UnityEngine;

namespace LastLight
{
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public abstract class PanelBase : MonoBehaviour
    {
        private bool initialized;
        private bool opened;
        public bool IsOpen => opened;
        protected internal void InitializePanel()
        {
            if (initialized) return;
            OnInitialize(); initialized = true;
        }
        protected internal void OpenPanel(object context)
        {
            opened = true;
            OnOpen(context);
            gameObject.SetActive(true);
        }
        protected internal void ClosePanel()
        {
            if (!opened) return;
            opened = false;
            try { OnClose(); }
            finally { gameObject.SetActive(false); }
        }
        internal void RecyclePanel() => OnRecycle();
        internal void SetFocus(bool enabled)
        {
            var group = GetComponent<CanvasGroup>();
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
        protected virtual void OnInitialize() { }
        protected abstract void OnOpen(object context);
        protected virtual void OnClose() { }
        protected virtual void OnRecycle() { }
        protected virtual void OnDestroy() { if (opened) ClosePanel(); }
    }
}
