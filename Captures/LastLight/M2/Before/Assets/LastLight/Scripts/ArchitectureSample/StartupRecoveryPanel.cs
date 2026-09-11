using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastLight
{
    /// <summary>唯一的引导面板：资源系统不可用时仍可显示。Prefab 由启动场景直接引用。</summary>
    public sealed class StartupRecoveryPanel : PanelBase
    {
        [SerializeField] private Text message;
        [SerializeField] private Button retry;
        [SerializeField] private GameObject fallbackInput;
        private Action retryAction;
        public void Configure(Text text, Button button, GameObject input) { message = text; retry = button; fallbackInput = input; }
        public void Present(string text, Action action)
        {
            retryAction = action; InitializePanel(); OpenPanel(text);
        }
        public void Hide() => ClosePanel();
        protected override void OnInitialize() => retry.onClick.AddListener(() => retryAction?.Invoke());
        protected override void OnOpen(object context)
        {
            message.text = (string)context;
            fallbackInput.SetActive(EventSystem.current == null);
        }
        protected override void OnClose() { fallbackInput.SetActive(false); retryAction = null; }
    }
}
