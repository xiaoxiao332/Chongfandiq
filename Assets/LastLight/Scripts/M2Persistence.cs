using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace LastLight
{
    public sealed partial class M1Director
    {
        [SerializeField] private bool m2;
        public bool IsM2=>m2;
        public bool AtMainMenu {get;private set;}
        private bool pendingAuto;
        public void RequestAutoSave(){if(m2&&!AtMainMenu&&!Restoring)pendingAuto=true;}
        private void PumpAutoSave(){if(pendingAuto&&!Busy&&!Restoring&&!AtMainMenu&&Current!=null&&Current.CanSnapshot){pendingAuto=false;AutoSave();}}
        public string Prefix=>IsM3?"LastLight/M3/":m2?"LastLight/M2/":"LastLight/M1/";
        public M2WorldSystem World=>Global.Session.Get<M2WorldSystem>();
        public M2SaveStore Saves {get;private set;}
        public bool Restoring {get;private set;}
        public string SaveMessage {get;private set;}="";
        internal readonly List<M2EnemySave> savedEnemies=new List<M2EnemySave>();
        private M1SceneState greenhouse;
        public static string SaveDirectoryOverride;
        public void ConfigureM2(GlobalManager root){Configure(root);m2=true;}
        private void InitializeM2()
        {
            Saves=new M2SaveStore(SaveDirectoryOverride??System.IO.Path.Combine(Application.persistentDataPath,IsM3?"M3Saves":"M2Saves"),IsM3);
            M2Settings.Load();if(IsM3){relay=new M1SceneState("Relay",this);Global.Systems.Get<SceneFlowSystem>().Register(relay);}
            greenhouse=new M1SceneState("Greenhouse",this);Global.Systems.Get<SceneFlowSystem>().Register(greenhouse);
        }
        public M2SaveData CaptureSave()
        {
            if(Current==null||!Current.Ready)throw new InvalidOperationException("Scene is not ready to save");
            var d=new M2SaveData{scene=Current.gameObject.scene.name,player=Current.Actor.transform.position,stamina=Current.Actor.Stamina,world=World.Capture()};
            if(IsM3){d.version=3;d.m3=M3.Capture();}
            Economy.Capture(d);Construction.Capture(d);Survival.Capture(d);Narrative.Capture(d);Threat.Capture(d);
            Current.RememberEnemies();d.enemies=savedEnemies.Select(e=>JsonUtility.FromJson<M2EnemySave>(JsonUtility.ToJson(e))).ToList();d.Validate();return d;
        }
        public bool SaveSlot(int slot)
        {
            if(!m2||Busy||Restoring||Current==null||!Current.CanSnapshot)return false;
            try{Saves.Save(slot,CaptureSave());SaveMessage=L.K("t8126501678");return true;}
            catch(Exception ex){SaveMessage=L.K("tb12163000c")+ex.Message;Debug.LogException(ex);return false;}
        }
        internal void AutoSave(){if(m2&&!AtMainMenu&&!Restoring&&!SaveSlot(0)&&!string.IsNullOrEmpty(SaveMessage))Current?.Notify(SaveMessage);}
        private void RestoreSystems(M2SaveData data)
        {
            Economy.Restore(data);Construction.Restore(data);Survival.Restore(data);Narrative.Restore(data);Threat.Restore(data);World.Restore(data.world);if(IsM3)M3.Restore(data.m3);
            savedEnemies.Clear();savedEnemies.AddRange(data.enemies.Select(e=>JsonUtility.FromJson<M2EnemySave>(JsonUtility.ToJson(e))));
        }
        public async Task LoadSlotAsync(int slot)
        {
            if(Busy||Restoring)return;
            M2SaveData data;bool backup;
            try{data=Saves.Load(slot,out backup);if(IsM3!=(data.version==3))throw new InvalidOperationException("Use explicit M2 import for legacy saves");}catch(Exception ex){SaveMessage=L.K("t63370a8fa5")+ex.Message;Current?.Notify(SaveMessage);return;}
            var previous=CaptureSave();bool priorMenu=AtMainMenu;Restoring=true;
            try
            {
                await Current.QuiesceAsync();RestoreSystems(data);await TravelAsync(data.scene);Current.Actor.Teleport(data.player);Current.Actor.RestoreStamina(data.stamina);
                AtMainMenu=false;SaveMessage=backup?L.K("t64d094368d"):L.K("t59ff7364ce");Current.Notify(SaveMessage);
            }
            catch(Exception loadError)
            {
                try{if(Current!=null)await Current.QuiesceAsync();RestoreSystems(previous);await TravelAsync(previous.scene);Current.Actor.Teleport(previous.player);Current.Actor.RestoreStamina(previous.stamina);}
                catch(Exception rollbackError){throw new AggregateException("Load and rollback failed",loadError,rollbackError);}
                AtMainMenu=priorMenu;SaveMessage=L.K("t28107783f8")+loadError.Message;Current.Notify(SaveMessage);Debug.LogException(loadError);
            }
            finally{Restoring=false;}
        }
        public void OpenMainMenu()
        {
            if(IsM3){OpenM3MainMenu();return;}
            AtMainMenu=true;
            Current.Present(new M1PanelModel{Title=L.K("td3f9844799"),Body=()=>L.K("t265134cd0c")+SaveMessage,Labels=new[]{L.K("t0f3f4d5b3c"),L.K("t9a4bf34e39"),L.K("tc7631b6c50"),L.K("t7debf9cb03"),L.K("t2db86dc725")},Command=i=>{
                if(i==0){if(Saves.Exists(0))Run(LoadSlotAsync(0));else Current.Notify(L.K("tcc36e5bf66"));}
                if(i==1)Current.Present(new M1PanelModel{Title=L.K("tbb25842c0c"),Body=()=>L.K("tada30fb481"),Labels=new[]{L.K("t79cc871ca9"),L.K("t11d0241540")},Command=j=>{if(j==0)Run(RestartAsync());else OpenMainMenu();}});
                if(i==2)Current.OpenSaveSlots(false);if(i==3)Current.OpenM2Settings();if(i==4)Application.Quit();}});
        }
    }
    public sealed partial class M1GameSession
    {
        public bool M2Enabled=>director!=null&&director.IsM2;
        public bool CanSnapshot=>active&&!syncing&&!seasonalStarting;
        public void Present(M1PanelModel model)=>Show("M1Window",model);
        internal void RememberEnemies()
        {
            if(!director.IsM2)return;
            string prefix=gameObject.scene.name+"/";director.savedEnemies.RemoveAll(e=>e.id.StartsWith(prefix,StringComparison.Ordinal));
            foreach(var b in beasts)director.savedEnemies.Add(b.Capture(prefix+b.name));
            if(raider!=null)director.savedEnemies.Add(raider.Instance.GetComponent<M1Beast>().Capture(prefix+"Raid"));
        }
        private async Task RestoreEnemiesAsync()
        {
            foreach(var b in beasts){var d=director.savedEnemies.FirstOrDefault(e=>e.id==gameObject.scene.name+"/"+b.name);if(d!=null)b.Restore(d);}
            if(home&&T.Active)
            {
                var d=director.savedEnemies.Single(e=>e.raid);
                raider=await director.Global.Systems.Get<ResourceSystem>().RentAsync(director.Prefix+"Prefabs/Raider",objects,scope,1,sceneLifetime.Token);
                var go=raider.Instance;go.SetActive(true);go.GetComponent<M1Beast>().Initialize(this,true,go.transform.GetChild(0));go.GetComponent<M1Beast>().Restore(d);
            }
        }
        public void OpenSaveSlots(bool saving)
        {
            Present(new M1PanelModel{Title=saving?L.K("t88aadc3fdd"):L.K("t4b2e713717"),Body=()=>L.K("t0a67413413")+director.SaveMessage,Labels=saving?new[]{L.K("taea785fbf0"),L.K("tb06703821a"),L.K("t3fb81260b5"),L.K("t11d0241540")}:new[]{L.K("t00a9a6b2ee"),L.K("taea785fbf0"),L.K("tb06703821a"),L.K("t3fb81260b5"),L.K("t11d0241540")},Command=i=>{
                int slot=saving?i+1:i;if(i==(saving?3:4)){if(director.AtMainMenu)director.OpenMainMenu();else OpenM2Pause();return;}
                if(!saving){director.Run(director.LoadSlotAsync(slot));return;}
                if(director.Saves.Exists(slot))Present(new M1PanelModel{Title=L.K("t096c2b286f"),Body=()=>L.K("tc56ea76c8e"),Labels=new[]{L.K("tfadf24dbc5"),L.K("t4d0b4688c7")},Command=j=>{if(j==0)director.SaveSlot(slot);OpenSaveSlots(true);}});
                else {director.SaveSlot(slot);OpenSaveSlots(true);}}});
        }
        private void OpenM2Pause()=>Present(new M1PanelModel{Title=L.K("t130448bce6"),Body=()=>L.K("teee7bf4d23")+director.SaveMessage,Labels=new[]{L.K("t1fc1afc5c5"),L.K("t88aadc3fdd"),L.K("t4b2e713717"),L.K("t7debf9cb03"),L.K("tcad0a16196"),L.K("t8112901ecd")},Command=i=>{if(i==0)CloseWindow();if(i==1)OpenSaveSlots(true);if(i==2)OpenSaveSlots(false);if(i==3)OpenM2Settings();if(i==4){CloseWindow();director.Run(RescueAsync());}if(i==5){director.AutoSave();director.OpenMainMenu();}}});
    }
}
