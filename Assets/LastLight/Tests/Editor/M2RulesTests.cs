using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LastLight.Tests
{
    public sealed class M2RulesTests
    {
        private string directory;
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"LastLight-M2Tests-"+Guid.NewGuid());}
        [TearDown] public void Cleanup(){if(Directory.Exists(directory))Directory.Delete(directory,true);}
        private static M2SaveData Save()
        {
            var d=new M2SaveData{world=new M2WorldData(),player=Vector3.zero};
            new M1EconomySystem().Capture(d);new M1ConstructionSystem().Capture(d);new M1SurvivalSystem().Capture(d);new M1StorySystem().Capture(d);new M1ThreatSystem().Capture(d);return d;
        }
        [Test] public void SaveRoundtripPreservesIndependentState()
        {
            var d=Save();d.backpack[0]=8;d.world.samples[1]=true;d.world.starterUsed=true;d.buildings.Add(new Building(19,Structure.Planter,2,2,0){crop=1,growth=43,health=60,protectedCrop=true});
            var store=new M2SaveStore(directory);store.Save(1,d);var loaded=store.Load(1,out bool backup);
            Assert.False(backup);Assert.AreEqual(8,loaded.backpack[0]);Assert.AreEqual(43,loaded.buildings[0].growth);Assert.True(loaded.world.samples[1]);
            var c=new M1ConstructionSystem();c.Restore(loaded);var stock=new Inventory(80);stock.Add(Resource.Wood,100);Assert.Greater(c.Grid.Place(Structure.Floor,3,3,0,stock,out _).id,19);
        }
        [Test] public void CorruptPrimaryRecoversValidBackupAndPreservesItOnSave()
        {
            var store=new M2SaveStore(directory);var d=Save();d.pickups=4;store.Save(0,d);d.pickups=9;store.Save(0,d);
            File.WriteAllText(store.PathFor(0),"broken");Assert.AreEqual(4,store.Load(0,out bool backup).pickups);Assert.True(backup);
            d.pickups=12;store.Save(0,d);File.WriteAllText(store.PathFor(0),"broken again");Assert.AreEqual(4,store.Load(0,out backup).pickups);Assert.True(backup);
        }
        [Test] public void BothCorruptSavesFailWithoutDeletingFiles(){var store=new M2SaveStore(directory);store.Save(1,Save());File.WriteAllText(store.PathFor(1),"bad");File.WriteAllText(store.PathFor(1)+".bak","bad");Assert.Throws<IOException>(()=>store.Load(1,out _));Assert.True(File.Exists(store.PathFor(1)));}
        [Test] public void UnsupportedVersionAndInvalidDataAreRejected(){var d=Save();d.version=2;Assert.Throws<InvalidDataException>(()=>d.Validate());d.version=1;d.hunger=float.NaN;Assert.Throws<InvalidDataException>(()=>d.Validate());}
        [Test] public void FailedWriteDoesNotReplacePriorSave(){var store=new M2SaveStore(directory);store.Save(2,Save());using(var held=new FileStream(store.PathFor(2)+".tmp",FileMode.Create,FileAccess.Write,FileShare.None))Assert.Throws<IOException>(()=>store.Save(2,Save()));Assert.AreEqual(1,store.Load(2,out _).version);}
        [Test] public void RaidSnapshotRequiresEnemyAndPreservesHitLedger(){var d=Save();d.raidActive=true;Assert.Throws<InvalidDataException>(()=>d.Validate());d.enemies.Add(new M2EnemySave{id="Home/Raid",raid=true,health=52,state=2,timer=.5f,hitIds=new[]{0,-1}});d.Validate();var store=new M2SaveStore(directory);store.Save(3,d);Assert.AreEqual(2,store.Load(3,out _).enemies[0].hitIds.Length);}
        [Test] public void StarterAndHarvestAreAtomic()
        {
            var world=new M2WorldSystem();var stock=new Inventory(1);var c=new M1ConstructionSystem();var b=new Building(1,Structure.Planter,2,2,0);c.Grid.Items.Add(b);
            Assert.True(world.Plant(b,0,stock));Assert.False(world.Plant(b,0,stock));world.Advance(600,c);Assert.True(stock.Add(Resource.Wood,20));Assert.False(world.Harvest(b,stock));Assert.AreEqual(0,b.crop);
            stock.Clear();Assert.True(world.Harvest(b,stock));Assert.False(world.Harvest(b,stock));Assert.AreEqual(6,stock[Resource.Ration]);Assert.False(world.Plant(b,0,stock));world.Data.samples[0]=true;Assert.True(world.Plant(b,0,stock));Assert.AreEqual(5,stock[Resource.Ration]);
        }
        [Test] public void DamagedCropsPauseAndProtectionHelpsWinter()
        {
            var w=new M2WorldSystem();var c=new M1ConstructionSystem();var a=new Building(1,Structure.Planter,2,2,0){crop=0};var b=new Building(2,Structure.Planter,3,2,0){crop=0,protectedCrop=true};c.Grid.Items.Add(a);c.Grid.Items.Add(b);
            w.Advance(10,c);Assert.Greater(b.growth,a.growth);a.health=0;float prior=a.growth;w.Advance(10,c);Assert.AreEqual(prior,a.growth);w.Data.spring=true;a.health=100;w.Advance(10,c);Assert.AreEqual(prior+10,a.growth);
        }
        [Test] public void CropMovePreservesGrowthAndRemovalRequiresHarvest(){var c=new M1ConstructionSystem();var b=new Building(1,Structure.Planter,2,2,0){crop=1,growth=52};c.Grid.Items.Add(b);Assert.True(c.Grid.Move(b,3,3,0,out _));Assert.AreEqual(52,b.growth);Assert.False(c.Grid.Remove(b,new Inventory(80),out _));}
        [Test] public void SpringDoesNotWaitForCalendarAndNeverLoopsBack(){var w=new M2WorldSystem();w.Data.spring=true;w.Advance(900*30,new M1ConstructionSystem());Assert.True(w.Data.spring);Assert.AreEqual(31,w.Day);}
        [Test] public void CompletedRewardsCannotRepeatAfterSave()
        {
            var story=new M1StorySystem();var economy=new M1EconomySystem();story.State.liang=true;Assert.True(story.State.ClaimReward(economy.Storage));Assert.True(story.State.Choose(1,economy.Storage));
            var d=Save();story.Capture(d);economy.Capture(d);var store=new M2SaveStore(directory);store.Save(1,d);d=store.Load(1,out _);story.Restore(d);economy.Restore(d);
            Assert.False(story.State.ClaimReward(economy.Storage));Assert.False(story.State.Choose(2,economy.Storage));Assert.AreEqual(2,economy.Storage[Resource.Parts]);
        }
        [Test] public void BridgeCostsOnlyOnceAfterWork()
        {
            var w=new M2WorldSystem();w.Data.bridgeOrdered=true;var c=new M1ConstructionSystem{LiangWorking=true};var stock=new Inventory(80);stock.Add(Resource.Wood,8);stock.Add(Resource.Parts,3);
            Assert.False(w.WorkBridge(12,false,c,stock));Assert.AreEqual(8,stock[Resource.Wood]);Assert.True(w.WorkBridge(12,true,c,stock));Assert.False(w.WorkBridge(12,true,c,stock));Assert.True(w.Data.spring);Assert.AreEqual(0,stock[Resource.Parts]);
        }
    }
}
