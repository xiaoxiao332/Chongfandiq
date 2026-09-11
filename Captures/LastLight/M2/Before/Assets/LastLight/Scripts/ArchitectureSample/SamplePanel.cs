using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight
{
    public sealed class PanelViewModel
    {
        public string Title;
        public string Body;
        public string[] Labels = Array.Empty<string>();
        public Func<int, Task> Command;
        public event Action Changed;
        public void Refresh() => Changed?.Invoke();
    }

    public sealed class SamplePanel : PanelBase
    {
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private Button[] buttons;
        private PanelViewModel model;
        public void Configure(Text titleText, Text bodyText, Button[] actions)
        { title = titleText; body = bodyText; buttons = actions; }
        protected override void OnInitialize()
        {
            for (int i = 0; i < buttons.Length; ++i)
            {
                int index = i;
                buttons[i].onClick.AddListener(() => Invoke(index));
            }
        }
        protected override void OnOpen(object context)
        {
            model = context as PanelViewModel ?? throw new ArgumentException("SamplePanel 需要 PanelViewModel。");
            model.Changed += Render; Render();
        }
        private void Render()
        {
            title.text = model.Title; body.text = model.Body;
            for (int i = 0; i < buttons.Length; ++i)
            {
                buttons[i].gameObject.SetActive(i < model.Labels.Length);
                if (i < model.Labels.Length) buttons[i].GetComponentInChildren<Text>().text = model.Labels[i];
            }
        }
        private async void Invoke(int index)
        {
            var current = model;
            try { if (current?.Command != null) await current.Command(index); }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (model == current && model != null) { model.Body = ex.Message; model.Refresh(); }
            }
        }
        protected override void OnClose() { if (model != null) model.Changed -= Render; model = null; }
        protected override void OnRecycle() { title.text = ""; body.text = ""; }
    }
}
