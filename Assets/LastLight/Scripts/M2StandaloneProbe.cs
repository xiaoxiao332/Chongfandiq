using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace LastLight
{
    // Explicit command-line diagnostic; never runs during ordinary gameplay.
    public sealed class M2StandaloneProbe:MonoBehaviour
    {
        private static string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--m2-probe");if(index<0||index+1>=args.Length)return;
            output=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(output);M1Director.SaveDirectoryOverride=Path.Combine(output,"Saves");
            var go=new GameObject("M2 explicit build probe");DontDestroyOnLoad(go);go.AddComponent<M2StandaloneProbe>();
        }
        private static async Task Until(Func<bool> condition)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+60;while(!condition()){if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Build probe timed out");await Task.Yield();}
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private async void Start()
        {
            bool reload=Environment.GetCommandLineArgs().Contains("--m2-probe-load");string phase=reload?"reload":"cold-start";
            try
            {
                M1Director d=null;await Until(()=>{d=FindFirstObjectByType<M1Director>();return d!=null&&d.IsM2&&d.Current!=null&&d.Current.Ready&&!d.Busy;});
                if(reload)
                {
                    await d.LoadSlotAsync(1);Check(d.Current.gameObject.scene.name=="Greenhouse","Saved scene did not restore");Check(d.Economy.Backpack[Resource.Wood]==7,"Inventory did not survive process restart");Check(d.World.Data.samples[2],"Crop unlock did not survive restart");
                }
                else
                {
                    await d.RestartAsync();d.Economy.Backpack.Add(Resource.Wood,7);d.World.Data.bridge=true;d.World.Data.spring=true;d.World.Data.samples[2]=true;
                    await d.TravelAsync("Workshop");await d.TravelAsync("Greenhouse");await Until(()=>d.Current.CanSnapshot);Check(d.SaveSlot(1),"Manual save failed");await d.TravelAsync("Home");await d.LoadSlotAsync(1);
                    Check(d.Current.gameObject.scene.name=="Greenhouse","Three-scene load failed");
                }
                d.Current.OpenJournal();await Task.Yield();float time=d.World.Data.springTime;for(int i=0;i<15;i++)await Task.Yield();Check(time==d.World.Data.springTime,"Reading did not pause world time");
                Check(FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).All(t=>t.font!=null),"Missing embedded font");
                d.Current.CloseWindow();ScreenCapture.CaptureScreenshot(Path.Combine(output,phase+".png"));for(int i=0;i<30;i++)await Task.Yield();
                File.WriteAllText(Path.Combine(output,phase+".json"),"{\"passed\":true,\"phase\":\""+phase+"\",\"scene\":\"Greenhouse\",\"wood\":7,\"pause\":true,\"localContent\":true}");
                await d.Global.ShutdownAsync();Application.Quit(0);
            }
            catch(Exception ex){File.WriteAllText(Path.Combine(output,phase+".json"),JsonUtility.ToJson(new Failure{error=ex.ToString()}));Debug.LogException(ex);Application.Quit(1);}
        }
        [Serializable] private sealed class Failure {public bool passed=false;public string error;}
    }
}
