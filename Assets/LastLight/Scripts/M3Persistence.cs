using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace LastLight
{
    public sealed partial class M1Director
    {
        [SerializeField] private bool m3;
        public bool IsM3=>m3;
        public M3WorldSystem M3=>Global.Session.Get<M3WorldSystem>();
        private M1SceneState relay;
        public void ConfigureM3(GlobalManager root){ConfigureM2(root);m3=true;}
        public static M2SaveData MigrateM2(M2SaveData source)
        {
            source.Validate();if(source.version!=1)throw new InvalidDataException("Only M2 saves can be imported");
            var result=JsonUtility.FromJson<M2SaveData>(JsonUtility.ToJson(source));result.version=3;result.m3=new M3State();
            result.m3.robots[0].awake=result.story.liang;result.m3.robots[0].broken=result.liangBroken;result.m3.robots[0].enabled=result.liangWorking;
            // M2 can remain in spring indefinitely. Preserve day within its first spring, not skipped seasons.
            result.world.springTime=result.world.springTime%M3Rules.SeasonSeconds;
            float warningRemaining=Mathf.Max(0,source.world.nextThreat-source.world.springTime);
            result.world.nextThreat=result.world.springTime+warningRemaining;
            if(result.world.warning)result.world.warningAt=-1;
            result.Validate();return result;
        }
        public async Task ImportM2Async(int sourceSlot,int destinationSlot)
        {
            if(!IsM3||Busy||Restoring||destinationSlot<1||destinationSlot>3)return;
            try
            {
                var legacy=new M2SaveStore(Path.Combine(Application.persistentDataPath,"M2Saves"));
                var data=MigrateM2(legacy.Load(sourceSlot,out bool backup));
                Saves.Save(destinationSlot,data);await LoadSlotAsync(destinationSlot);
                SaveMessage=M3Text.Pair("M2 已导入；原文件保留。","M2 imported; original files preserved.")+(backup?M3Text.Pair("使用了有效备份。","Recovered from valid backup."):"");
            }
            catch(Exception ex){SaveMessage=M3Text.Pair("导入失败：","Import failed: ")+ex.Message;Current?.Notify(SaveMessage);}
        }
        public bool ConfirmM3Ending(M3Ending route)
        {
            if(!IsM3||Busy||Restoring||Current==null||!Current.CanSnapshot||Threat.Active||World.Data.warning)return false;
            var state=M3.Data;
            if(M3Rules.EndingMissing(route,state,Construction.Grid.Items)!=""||!Economy.Storage.CanPay(M3Rules.FinaleCost(route,state)))return false;
            if(!SaveSlot(4))return false;
            if(!M3Rules.CommitEnding(route,state,Construction.Grid.Items,Economy.Storage))return false;
            Current.ShowM3Ending();return true;
        }
        private void OpenM3MainMenu()
        {
            AtMainMenu=true;
            Current.Present(new M1PanelModel{Title=M3Text.Pair("留灯地球 · 完整旅程","Last Light · The journey"),Body=()=>M3Text.Pair("继续旅程，或从维护舱开始。导入 M2 不修改原存档。","Continue your journey or wake in the maintenance pod. M2 import preserves original saves.")+"\n"+SaveMessage,
                Labels=new[]{M3Text.Pair("继续","Continue"),M3Text.Pair("新游戏","New game"),M3Text.Pair("读取存档","Load game"),M3Text.Pair("导入 M2","Import M2"),M3Text.Pair("终局前保存点","Before the ending"),M3Text.Pair("设置","Settings"),M3Text.Pair("退出","Quit")},Command=i=>{
                    if(i==0){if(Saves.Exists(0))Run(LoadSlotAsync(0));else Current.Notify(M3Text.Pair("没有自动存档。","No autosave yet."));}
                    if(i==1)Current.Present(new M1PanelModel{Title=M3Text.Pair("开始新旅程","Begin a new journey"),Body=()=>M3Text.Pair("新游戏将重新开始；旧手动存档保留。","Start over. Existing manual saves remain available."),Labels=new[]{M3Text.Pair("开始","Start"),M3Text.Pair("返回","Back")},Command=j=>{if(j==0)Run(RestartAsync());else OpenM3MainMenu();}});
                    if(i==2)Current.OpenSaveSlots(false);if(i==3)OpenM2Import();if(i==4){if(Saves.Exists(4))Run(LoadSlotAsync(4));else Current.Notify(M3Text.Pair("还没有终局保存点。","No ending checkpoint yet."));}
                    if(i==5)Current.OpenM2Settings();if(i==6)Application.Quit();}});
        }
        private void OpenM2Import()
        {
            Current.Present(new M1PanelModel{Title=M3Text.Pair("导入 M2","Import M2"),Body=()=>M3Text.Pair("选择 M2 来源槽，再选择 M3 目标槽。","Choose a source M2 slot, then a destination M3 slot."),Labels=new[]{"Auto","1","2","3",M3Text.Pair("返回","Back")},Command=source=>{
                if(source==4){OpenM3MainMenu();return;}
                Current.Present(new M1PanelModel{Title=M3Text.Pair("目标 M3 槽","Destination M3 slot"),Body=()=>M3Text.Pair("确认后写入目标槽；原 M2 文件不改变。已存在的 M3 目标会备份后替换。","Confirm to write the destination. Original M2 files remain unchanged. Existing M3 destination is backed up before replacement."),Labels=new[]{"1","2","3",M3Text.Pair("返回","Back")},Command=slot=>{if(slot==3){OpenM2Import();return;}Run(ImportM2Async(source,slot+1));}});
            }});
        }
    }
}
