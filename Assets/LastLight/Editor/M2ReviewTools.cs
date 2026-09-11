using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LastLight.Editor
{
    public static class M2ReviewTools
    {
        public static Task Running { get; private set; }
        public static string Status { get; private set; }="Not started";
        public static void Begin(){if(Running!=null&&!Running.IsCompleted)throw new InvalidOperationException("Capture already running");Running=CaptureMatrix();}
        private static async Task Frames(int count=4){for(int i=0;i<count;i++)await Task.Yield();}
        private static async Task CaptureMatrix()
        {
            var d=Object.FindFirstObjectByType<M1Director>();if(d?.Current==null)throw new InvalidOperationException("需要运行中的 M1");
            var assembly=typeof(UnityEditor.Editor).Assembly;var viewType=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            var groupId=viewType.GetProperty("currentSizeGroupType",flags).GetValue(view);
            var group=sizesType.GetMethod("GetGroup",flags).Invoke(singleton,new[]{groupId});var groupType=group.GetType();
            var selected=viewType.GetProperty("selectedSizeIndex",flags);int previous=(int)selected.GetValue(view);
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");var mode=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
            string folder="Captures/LastLight/M2/Layout-"+DateTime.Now.ToString("yyyyMMdd-HHmmss");Directory.CreateDirectory(folder);var report=new StringBuilder();bool language=L.English;int textSize=M2Settings.TextSize;while(M2Settings.TextSize!=2)M2Settings.Size();
            try
            {
                foreach(bool english in new[]{false,true})
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1920,1200),new Vector2Int(2560,1080),new Vector2Int(1280,720)})
                {
                    L.English=english;int index=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null);int custom=(int)groupType.GetMethod("GetCustomCount").Invoke(group,null);
                    var value=Activator.CreateInstance(sizeType,new object[]{mode,size.x,size.y,"M2 review temporary"});groupType.GetMethod("AddCustomSize").Invoke(group,new[]{value});selected.SetValue(view,index);view.Repaint();await Frames(8);
                    try
                    {
                        foreach(string state in new[]{"HUD","Inventory","Build","Journal","Settings","Bindings","Save","Main"})
                        {
                            if(d.AtMainMenu)await d.RestartAsync();d.Current.CloseWindow();await Frames();
                            if(state=="Inventory")d.Current.OpenInventory();if(state=="Build")d.Current.ToggleBuild();if(state=="Journal")d.Current.OpenJournal();if(state=="Settings")d.Current.OpenM2Settings();if(state=="Bindings")d.Current.OpenBindings();if(state=="Save")d.Current.OpenSaveSlots(true);if(state=="Main")d.OpenMainMenu();
                            await Frames(12);Canvas.ForceUpdateCanvases();
                            if(Screen.width!=size.x||Screen.height!=size.y)throw new InvalidOperationException($"Game View 实际尺寸 {Screen.width}×{Screen.height}，目标 {size}");
                            foreach(var panel in Object.FindObjectsByType<M1Panel>(FindObjectsSortMode.None))
                            {
                                var content=panel.transform.Find("Content") as RectTransform;var corners=new Vector3[4];content.GetWorldCorners(corners);
                                foreach(var corner in corners)if(corner.x<-.5f||corner.y<-.5f||corner.x>Screen.width+.5f||corner.y>Screen.height+.5f)throw new InvalidOperationException(panel.name+" 内容超出屏幕 "+size);
                                foreach(var button in panel.GetComponentsInChildren<Button>()){var text=button.GetComponentInChildren<Text>();if(text.preferredHeight>text.rectTransform.rect.height+1)throw new InvalidOperationException("按钮文字溢出："+text.text);}
                            }
                            string path=folder+"/"+(english?"en-":"zh-")+state+"-"+size.x+"x"+size.y+".png";ScreenCapture.CaptureScreenshot(path);await Frames(8);double deadline=Time.realtimeSinceStartupAsDouble+10;while(!File.Exists(path)&&Time.realtimeSinceStartupAsDouble<deadline)await Task.Yield();if(!File.Exists(path))throw new IOException("截图未落盘 "+path);
                            report.AppendLine("PASS "+(english?"en ":"zh ")+state+" "+size.x+"x"+size.y+" : content bounds, button text, actual screenshot");Status=state+" "+size;
                        }
                    }
                    finally
                    {
                        selected.SetValue(view,previous);var remove=groupType.GetMethod("RemoveCustomSize");
                        try{remove.Invoke(group,new object[]{index});}catch(TargetInvocationException ex) when(ex.InnerException is ArgumentOutOfRangeException){remove.Invoke(group,new object[]{custom});}
                    }
                }
                Status="PASS: 64 bilingual maximum-text-size Game View checks";
            }
            catch(Exception ex){Status="FAIL: "+ex.Message;report.AppendLine(ex.ToString());Debug.LogException(ex);}
            finally{L.English=language;while(M2Settings.TextSize!=textSize)M2Settings.Size();d.Current.CloseWindow();selected.SetValue(view,previous);view.Repaint();File.WriteAllText(folder+"/Validation.txt",report.ToString());}
        }
    }
}
