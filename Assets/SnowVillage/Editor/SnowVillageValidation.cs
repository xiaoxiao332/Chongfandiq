using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;

namespace SnowVillage.Editor
{
    // Drives the actual input system and game loop; results are recorded outside Assets.
    public static class SnowVillageValidation
    {
        private static SnowTraveler player;
        private static SnowFootprints prints;
        private static Keyboard keyboard;
        private static bool ownKeyboard;
        private static InputSettings.BackgroundBehavior previousInputBackground;
        private static double phaseStart;
        private static int phase;
        private static int directionIndex;
        private static int baseObjects;
        private static int samples;
        private static double totalFrameTime;
        private static float maxFrameTime;
        private static Vector3 before;
        private static int beforePrints;
        private static readonly List<string> results=new List<string>();
        private static readonly Key[][] Directions={new[]{Key.W},new[]{Key.S},new[]{Key.A},new[]{Key.D},new[]{Key.W,Key.A},new[]{Key.W,Key.D},new[]{Key.S,Key.A},new[]{Key.S,Key.D}};
        public static bool Running {get;private set;}
        public static string Status => Running?"Phase "+phase+" elapsed "+(EditorApplication.timeSinceStartup-phaseStart).ToString("F1")+"s":"Finished";
        public static void Start()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play Mode first.");
            if(Running)throw new InvalidOperationException("Validation already running.");
            player=UnityEngine.Object.FindFirstObjectByType<SnowTraveler>();prints=UnityEngine.Object.FindFirstObjectByType<SnowFootprints>();
            previousInputBackground=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();ownKeyboard=true;
            results.Clear();samples=0;totalFrameTime=0;maxFrameTime=0;phase=0;directionIndex=0;
            player.ResetTraveler();baseObjects=SceneObjectCount();before=player.transform.position;
            phaseStart=EditorApplication.timeSinceStartup;Running=true;EditorApplication.update+=Tick;
            results.Add("Started: "+DateTime.Now.ToString("O"));
        }
        private static void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys)); }
        private static int SceneObjectCount() { return player.gameObject.scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Length); }
        private static void Check(string title,bool passed,string evidence)
        {results.Add((passed?"PASS ":"FAIL ")+title+": "+evidence);}
        private static void Next(int next){phase=next;phaseStart=EditorApplication.timeSinceStartup;}
        private static void Tick()
        {
            if(!EditorApplication.isPlaying || player==null){Finish("Interrupted");return;}
            double elapsed=EditorApplication.timeSinceStartup-phaseStart;
            if(phase==0)
            {
                samples++;totalFrameTime+=Time.unscaledDeltaTime;maxFrameTime=Mathf.Max(maxFrameTime,Time.unscaledDeltaTime);
                if(elapsed<120)return;
                Check("120 second automatic walk",player.Automatic && prints.TotalStamps>120,"stamps="+prints.TotalStamps+", position="+player.transform.position);
                int count=SceneObjectCount();
                Check("bounded scene objects",count==baseObjects,"before="+baseObjects+", after="+count+", active dynamic footprints="+prints.ActiveCount);
                results.Add("Editor frame interval samples="+samples+", mean="+(1000*totalFrameTime/Math.Max(1,samples)).ToString("F2")+"ms, max="+(maxFrameTime*1000).ToString("F2")+"ms (Editor, not player benchmark)");
                player.ResetTraveler();player.SetAutomatic(false);Next(1);
            }
            else if(phase==1)
            {
                if(elapsed<.4)return;
                before=player.transform.position;Keys(Directions[directionIndex]);Next(2);
            }
            else if(phase==2)
            {
                if(elapsed<.55)return;
                Vector3 delta=player.transform.position-before;delta.y=0;
                Vector3 expected=Vector3.zero;foreach(var key in Directions[directionIndex])expected+=key==Key.W?Vector3.forward:key==Key.S?Vector3.back:key==Key.A?Vector3.left:Vector3.right;
                Check("keyboard "+string.Join("+",Directions[directionIndex].Select(k=>k.ToString())),delta.magnitude>.35f && delta.magnitude<.9f && Vector3.Dot(delta.normalized,expected.normalized)>.9f && !player.Automatic,"distance="+delta.magnitude.ToString("F3")+", delta="+delta);
                Keys();directionIndex++;if(directionIndex<Directions.Length){player.ResetTraveler();player.SetAutomatic(false);Next(1);}else Next(3);
            }
            else if(phase==3)
            {
                if(elapsed<1)return;before=player.transform.position;beforePrints=prints.TotalStamps;Next(4);
            }
            else if(phase==4)
            {
                if(elapsed<1)return;
                Check("idle stops movement and footprints",Vector3.Distance(player.transform.position,before)<.025f && prints.TotalStamps==beforePrints,"stamps="+prints.TotalStamps+", speed="+player.ActualSpeed);
                Keys(Key.Tab);Next(5);
            }
            else if(phase==5)
            {
                if(elapsed<.2)return;Keys();Check("Tab enables demo",player.Automatic,"automatic="+player.Automatic);Keys(Key.W);Next(6);
            }
            else if(phase==6)
            {
                if(elapsed<.25)return;Keys();Check("WASD takes over demo",!player.Automatic,"automatic="+player.Automatic);Keys(Key.R);Next(7);
            }
            else if(phase==7)
            {
                if(elapsed<.12)return;Keys();Check("R resets tracks and demo",prints.TotalStamps<=1 && player.Automatic && Mathf.Abs(player.transform.position.x)<.3f,"stamps="+prints.TotalStamps+", position="+player.transform.position);
                player.SetAutomatic(false);player.enabled=false;var motor=player.GetComponent<CharacterController>();motor.enabled=false;player.transform.position=new Vector3(0,.3f,1);motor.enabled=true;Physics.SyncTransforms();Next(8);
            }
            else if(phase==8)
            {
                player.MoveWorld(Vector3.right,Mathf.Min(Time.unscaledDeltaTime,.04f));
                if(elapsed<3)return;
                Check("woodshed blocks movement",player.transform.position.x>.5f && player.transform.position.x<1.25f,"position="+player.transform.position);
                // Explicitly exercise pool wraparound without waiting for several route loops.
                for(int i=0;i<300;i++)prints.Stamp(new Vector3(0,.15f,0),Vector3.forward,false);
                Check("footprint pool wraps at 256",prints.ActiveCount==256 && prints.transform.GetComponentsInChildren<MeshFilter>(true).Length==256,"active="+prints.ActiveCount+", total="+prints.TotalStamps);
                player.enabled=true;player.ResetTraveler();Check("reset clears pool",prints.ActiveCount==0,"active="+prints.ActiveCount);Finish("Complete");
            }
        }
        private static void Finish(string reason)
        {
            EditorApplication.update-=Tick;Running=false;
            if(keyboard!=null){Keys();if(ownKeyboard)InputSystem.RemoveDevice(keyboard);}
            InputSystem.settings.backgroundBehavior=previousInputBackground;
            results.Add(reason);Directory.CreateDirectory("Captures/SnowVillage");File.WriteAllLines("Captures/SnowVillage/RuntimeValidation.txt",results);
            Debug.Log("SnowVillage validation "+reason+"; failures="+results.Count(s=>s.StartsWith("FAIL")));
        }
    }
}
