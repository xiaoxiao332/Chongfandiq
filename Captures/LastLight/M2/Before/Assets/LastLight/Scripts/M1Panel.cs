using System;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight
{
    public sealed class M1PanelModel
    {
        public string Title;
        public Func<string> Body;
        public string[] Labels=Array.Empty<string>();
        public Action<int> Command;
        public Action Closed;
    }
    public sealed class M1Panel : PanelBase
    {
        [SerializeField] private Text title;
        [SerializeField] private Text body;
        [SerializeField] private Button[] buttons;
        private M1PanelModel model;
        private float refreshAt;
        private static Font chinese;
        public string BodyText=>body.text;
        public void Configure(Text heading,Text content,Button[] actions){title=heading;body=content;buttons=actions;}
        protected override void OnInitialize()
        {
            // M1 is a Windows editor prototype. M2 will ship a licensed embedded font.
            if(chinese==null)chinese=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},24);
            foreach(var text in GetComponentsInChildren<Text>(true))text.font=chinese;
            for(int i=0;i<buttons.Length;i++){int command=i;buttons[i].onClick.AddListener(()=>model?.Command?.Invoke(command));}
        }
        protected override void OnOpen(object context)
        {
            model=context as M1PanelModel??throw new ArgumentException("M1 面板需要展示模型");
            title.text=model.Title;
            for(int i=0;i<buttons.Length;i++){buttons[i].gameObject.SetActive(i<model.Labels.Length);if(i<model.Labels.Length)buttons[i].GetComponentInChildren<Text>().text=model.Labels[i];}
            Refresh();
        }
        private void Update(){if(model!=null&&Time.unscaledTime>=refreshAt)Refresh();}
        private void Refresh(){body.text=model.Body?.Invoke()??"";refreshAt=Time.unscaledTime+.2f;}
        protected override void OnClose(){var callback=model?.Closed;model=null;callback?.Invoke();}
        protected override void OnRecycle(){body.text="";}
    }
}
