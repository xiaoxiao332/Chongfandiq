using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastLight
{
    public static class M2Settings
    {
        public static bool English {get;private set;}
        public static int TextSize {get;private set;}
        public static float Volume {get;private set;}=1;
        public static string Overrides=>PlayerPrefs.GetString("LastLight.M2.Bindings","");
        public static void Load(){English=PlayerPrefs.GetInt("LastLight.M2.English",0)!=0;TextSize=Mathf.Clamp(PlayerPrefs.GetInt("LastLight.M2.TextSize",0),0,2);Volume=Mathf.Clamp01(PlayerPrefs.GetFloat("LastLight.M2.Volume",1));L.English=English;AudioListener.volume=Volume;QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt("LastLight.M2.Quality",QualitySettings.GetQualityLevel()),0,QualitySettings.names.Length-1));}
        public static void Language(){English=!English;L.English=English;PlayerPrefs.SetInt("LastLight.M2.English",English?1:0);PlayerPrefs.Save();}
        public static void Size(){TextSize=(TextSize+1)%3;PlayerPrefs.SetInt("LastLight.M2.TextSize",TextSize);PlayerPrefs.Save();}
        public static void Sound(){Volume=Volume<.01f?1:Mathf.Max(0,Volume-.25f);AudioListener.volume=Volume;PlayerPrefs.SetFloat("LastLight.M2.Volume",Volume);PlayerPrefs.Save();}
        public static void Quality(){int next=(QualitySettings.GetQualityLevel()+1)%QualitySettings.names.Length;QualitySettings.SetQualityLevel(next);PlayerPrefs.SetInt("LastLight.M2.Quality",next);PlayerPrefs.Save();}
        public static void SaveBindings(InputActionAsset asset){PlayerPrefs.SetString("LastLight.M2.Bindings",asset.SaveBindingOverridesAsJson());PlayerPrefs.Save();}
    }
    public sealed partial class M1Actor
    {
        public InputActionAsset Actions=>actions;
        private InputActionRebindingExtensions.RebindingOperation rebinding;
        public bool IsRebinding=>rebinding!=null;
        private void ConfigureM2Input()
        {
            actions.Disable();var map=actions.FindActionMap("Player");
            foreach(var pair in new[]{("Build","b"),("Bag","tab"),("Journal","j"),("Eat","f"),("Rotate","r"),("Pulse","q")})
                if(map.FindAction(pair.Item1)==null)map.AddAction(pair.Item1,InputActionType.Button,"<Keyboard>/"+pair.Item2);
            if(!string.IsNullOrEmpty(M2Settings.Overrides))actions.LoadBindingOverridesFromJson(M2Settings.Overrides);
        }
        public bool Pressed(string action)=>actions!=null&&actions.FindAction("Player/"+action,true).WasPressedThisFrame();
        public string Binding(string action){var a=actions.FindAction("Player/"+action,true);return a.GetBindingDisplayString(action=="Attack"?1:action=="Move"?2:0);}
        public void Rebind(string actionName,int index,Action finished)
        {
            CancelRebind();var action=actions.FindAction("Player/"+actionName,true);actions.Disable();
            rebinding=action.PerformInteractiveRebinding(index).WithCancelingThrough("<Keyboard>/escape").WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta")
                .OnCancel(_=>EndRebind(finished)).OnComplete(_=>{M2Settings.SaveBindings(actions);EndRebind(finished);});rebinding.Start();
        }
        private void EndRebind(Action finished){rebinding?.Dispose();rebinding=null;actions.Enable();finished?.Invoke();}
        public void CancelRebind(){if(rebinding!=null){rebinding.Cancel();rebinding?.Dispose();rebinding=null;}}
    }
    public sealed partial class M1GameSession
    {
        public void OpenM2Settings()=>Present(new M1PanelModel{Title=L.K("t8e9b0e8d95"),Body=()=>"Language: "+(M2Settings.English?"English":L.K("t93659150d0"))+L.K("ta0b045b174")+(100+M2Settings.TextSize*15)+L.K("t039dd81caf")+Mathf.RoundToInt(M2Settings.Volume*100)+L.K("t6dabfbb92d")+QualitySettings.names[QualitySettings.GetQualityLevel()]+L.K("t3e57604a40"),Labels=new[]{L.K("t12799b2814"),L.K("t2e4d62d842"),L.K("t36d4db138a"),L.K("t6615720829"),L.K("t9be757c1e1"),L.K("t11d0241540")},Command=i=>{if(i==0)M2Settings.Language();if(i==1)M2Settings.Size();if(i==2)M2Settings.Sound();if(i==3)M2Settings.Quality();if(i==4)OpenBindings();if(i==5){if(director.AtMainMenu)director.OpenMainMenu();else OpenM2Pause();}}});
        public void OpenBindings()
        {
            var entries=new[]{("Move",2,L.K("ta09a74754b")),("Move",4,L.K("t7f156d6ec3")),("Move",6,L.K("t4a2815ba04")),("Move",8,L.K("t269b34903d")),("Sprint",0,L.K("t3fee5313a6")),("Attack",1,L.K("t8669d29269")),("Interact",0,L.K("t1b2c73865e")),("Build",0,L.K("t858f36a4ba")),("Bag",0,L.K("tff3d08fd8c")),("Journal",0,L.K("t4de50894b8")),("Eat",0,L.K("t7b245844ab")),("Rotate",0,L.K("ta69e51e503"))};
            Present(new M1PanelModel{Title=L.K("t9be757c1e1"),Body=()=>L.K("tada84af5f4"),Labels=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Concat(System.Linq.Enumerable.Select(entries,e=>e.Item3+" "+actor.Actions.FindAction("Player/"+e.Item1).GetBindingDisplayString(e.Item2)),new[]{L.K("t11d0241540")})),Command=i=>{
                if(i==entries.Length){OpenM2Settings();return;}var entry=entries[i];
                Present(new M1PanelModel{Title=L.K("t9a0d6f85ac"),Body=()=>entry.Item3+L.K("te0bdf89627"),Labels=Array.Empty<string>()});
                actor.Rebind(entry.Item1,entry.Item2,OpenBindings);
            }});
        }
    }
}
