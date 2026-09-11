using System;
using System.Linq;

namespace LastLight
{
    public sealed partial class M1GameSession
    {
        private void ShowM3Story(string id,Action after=null)
        {
            if(!M3Narrative.Records.TryGetValue(id,out var text))throw new InvalidOperationException("Missing authored narrative: "+id);
            if(!S3.events.Contains(id))S3.events.Add(id);
            var pages=L.Resolve(text).Split(new[]{"\n\n"},StringSplitOptions.RemoveEmptyEntries);int page=0;
            Action render=null;render=()=>Present(new M1PanelModel{Title=M3Text.Pair("人物记录","Character record")+" · "+(page+1)+" / "+pages.Length,Body=()=>pages[page],Labels=new[]{M3Text.Pair("继续","Continue"),S3.read.Contains(id)?M3Text.Pair("跳过已读","Skip read record"):M3Text.Pair("稍后再读","Read later")},Command=i=>{
                if(i==1){CloseWindow();after?.Invoke();return;}
                page++;if(page<pages.Length){render();return;}if(!S3.read.Contains(id))S3.read.Add(id);N.Log(M3Narrative.Records[id]);CloseWindow();director.RequestAutoSave();after?.Invoke();
            }});render();
        }
        private void OpenM3Archive(int page=0)
        {
            var ids=S3.events.Skip(page*9).Take(9).ToArray();
            Present(new M1PanelModel{Title=M3Text.Pair("已发现记录","Discovered records"),Body=()=>M3Text.Pair("仅显示已发现内容。未读记录带星号。","Only discovered records appear. Unread entries have a star."),Labels=ids.Select(id=>(S3.read.Contains(id)?"":"★ ")+id).Concat(new[]{M3Text.Pair("上一页","Previous"),M3Text.Pair("下一页","Next"),M3Text.Pair("返回","Back")}).ToArray(),Command=i=>{if(i<ids.Length)ShowM3Story(ids[i]);else if(i==ids.Length)OpenM3Archive(Math.Max(0,page-1));else if(i==ids.Length+1)OpenM3Archive((page+1)*9<S3.events.Count?page+1:page);else OpenM3Journal();}});
        }
    }
}
