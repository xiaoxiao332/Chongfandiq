using System;
using System.Linq;
using NUnit.Framework;

namespace LastLight.Tests
{
    public sealed class M3RulesTests
    {
        private static Inventory Stock(int amount=100){var stock=new Inventory(80);foreach(Resource r in Enum.GetValues(typeof(Resource)))stock.Add(r,amount);return stock;}
        [Test] public void BuildingIdsAndCountStayCompatible(){Assert.AreEqual(17,Enum.GetValues(typeof(Structure)).Length);Assert.AreEqual(9,(int)Structure.Planter);Assert.AreEqual(10,(int)Structure.Charger);foreach(Structure kind in Enum.GetValues(typeof(Structure))){Assert.IsNotEmpty(Catalog.Recipe(kind));Assert.IsNotEmpty(L.Resolve(Catalog.StructureNames[(int)kind]));}}
        [Test] public void ComfortCountsTypesNotCopies(){var items=Enumerable.Range(1,20).Select(i=>new Building(i,Structure.Table,i,0,0)).ToList();Assert.AreEqual(1,M3Rules.Comfort(items));items.Add(new Building(30,Structure.Lamp,0,0,0));Assert.AreEqual(2,M3Rules.Comfort(items));items.Last().health=0;Assert.AreEqual(1,M3Rules.Comfort(items));}
        [Test] public void SeasonsWrapAndProtectionHasValue(){Assert.AreEqual(M3Season.Spring,M3Rules.Season(0));Assert.AreEqual(M3Season.Summer,M3Rules.Season(5400));Assert.AreEqual(M3Season.Winter,M3Rules.Season(16200));Assert.AreEqual(M3Season.Spring,M3Rules.Season(21600));Assert.Greater(M3Rules.Growth(M3Season.Winter,true),M3Rules.Growth(M3Season.Winter,false));}
        [Test] public void EmergencyPowerCannotProduceFreeIndustrialGoods(){var state=new M3State();foreach(var r in state.robots)r.awake=true;var items=new[]{new Building(1,Structure.Charger,1,1,0)};CollectionAssert.AreEqual(new[]{true,false,false},M3Rules.Allocate(state,items));state.robots[0].job=M3Job.Processing;Assert.False(M3Rules.Allocate(state,items).Any(p=>p));state.generatorSeconds=30;Assert.True(M3Rules.Allocate(state,items).All(p=>p));items[0].health=0;Assert.AreEqual(1,M3Rules.Allocate(state,items).Count(p=>p));}
        [Test] public void JobSwitchIsBoundedAndDoesNotDuplicateOutput(){var r=new M3Robot("ya",M3Job.Gardening){awake=true,progress=25,produced=8};Assert.False(M3Rules.Switch(r,M3Job.Watch));Assert.True(M3Rules.Switch(r,M3Job.Gathering));Assert.AreEqual(0,r.progress);Assert.AreEqual(8,r.produced);Assert.True(M3Rules.Switch(r,M3Job.Gathering));}
        [Test] public void ProcessingDoesNotConsumeOnFullOutputOrBlockedPath()
        {
            var w=new M3WorldSystem();w.Data.robots[0].awake=true;w.Data.robots[0].job=M3Job.Processing;w.Data.generatorSeconds=300;
            var c=new M1ConstructionSystem();c.Grid.Items.Add(new Building(1,Structure.Charger,1,1,0));c.Grid.Items.Add(new Building(2,Structure.Workbench,2,2,0));
            var stock=new Inventory(1);stock.Add(Resource.Scrap,20);var farm=new M2WorldSystem();w.Advance(30,c,farm,stock,new[]{true,true,true},false);Assert.AreEqual(20,stock[Resource.Scrap]);Assert.AreEqual(M3Stop.Full,w.Status[0]);
            stock.Clear();stock.Add(Resource.Scrap,2);w.Advance(30,c,farm,stock,new[]{false,true,true},false);Assert.AreEqual(2,stock[Resource.Scrap]);Assert.AreEqual(M3Stop.Path,w.Status[0]);
            w.Advance(1,c,farm,stock,new[]{true,true,true},false);Assert.AreEqual(0,stock[Resource.Scrap]);Assert.AreEqual(2,stock[Resource.Parts]);
        }
        [Test] public void ProgressionCannotSkipPrerequisitesOrDuplicateCosts()
        {
            var s=new M3State();var c=new M1ConstructionSystem();var world=new M2WorldData{bridge=true,samples=new[]{true,true,true}};var stock=Stock();
            Assert.False(M3Progression.Execute("navigation",s,c,world,stock,out _));Assert.AreEqual(100,stock[Resource.Parts]);
            Assert.True(M3Progression.Execute("wake-ya",s,c,world,stock,out _));Assert.AreEqual(98,stock[Resource.Parts]);Assert.False(M3Progression.Execute("wake-ya",s,c,world,stock,out _));Assert.AreEqual(98,stock[Resource.Parts]);
            Assert.False(M3Progression.Execute("relay-road",s,c,world,stock,out _));Assert.True(M3Progression.Execute("insulation",s,c,world,stock,out _));Assert.True(M3Progression.Execute("relay-road",s,c,world,stock,out _));
            Assert.True(M3Progression.Execute("emergency-power",s,c,world,stock,out _));Assert.True(M3Progression.Execute("wake-shou",s,c,world,stock,out _));
        }
        private static M3State Prepared(out Building[] buildings)
        {
            var s=new M3State{structure=true,power=true,navigation=true,personalShip=true,transport=true,collectivePower=true,supplyDelivered=true,duAnswer=2,seats=2};foreach(var r in s.robots){r.awake=true;r.agreed=true;r.informed=true;r.answer=2;}
            s.Claim("departure-told-cen");s.Claim("departure-told-du");buildings=new[]{new Building(1,Structure.Charger,1,1,0),new Building(2,Structure.Stove,2,1,0),new Building(3,Structure.Planter,3,1,0){crop=0,protectedCrop=true}};return s;
        }
        [Test] public void TogetherRequiresConsentAndNeverCountsRefusal(){var s=Prepared(out var items);Assert.IsNotEmpty(M3Rules.EndingMissing(M3Ending.Together,s,items));s.robots[1].answer=1;Assert.IsEmpty(M3Rules.EndingMissing(M3Ending.Together,s,items));var stock=Stock();Assert.True(M3Rules.CommitEnding(M3Ending.Together,s,items,stock));CollectionAssert.AreEqual(new[]{"ya"},s.passengers);int rations=stock[Resource.Ration];Assert.False(M3Rules.CommitEnding(M3Ending.Together,s,items,stock));Assert.AreEqual(rations,stock[Resource.Ration]);}
        [Test] public void AllRoutesAreReachableAndMissingSuppliesDoNotCommit(){foreach(var route in new[]{M3Ending.Light,M3Ending.Voyage,M3Ending.Together}){var s=Prepared(out var items);s.robots[0].answer=1;Assert.IsEmpty(M3Rules.EndingMissing(route,s,items));Assert.False(M3Rules.CommitEnding(route,s,items,new Inventory(80)));Assert.AreEqual(M3Ending.None,s.ending);Assert.True(M3Rules.CommitEnding(route,s,items,Stock()));}}
        [Test] public void InvalidPersistentRosterIsRejected(){var s=new M3State();s.Validate();s.priority=new[]{0,0,2};Assert.Throws<ArgumentException>(()=>s.Validate());s.priority=new[]{0,1,2};s.robots[0].answer=3;Assert.Throws<ArgumentException>(()=>s.Validate());}
        [Test] public void NarrativeHasBilingualContentForEveryActionAndEnding(){foreach(var action in M3Progression.Actions)Assert.True(M3Narrative.Records.ContainsKey(action.Id),action.Id);foreach(bool english in new[]{false,true}){L.English=english;foreach(var text in M3Narrative.Records.Values){var resolved=L.Resolve(text);Assert.IsFalse(resolved.Contains("\uE010"));Assert.Greater(resolved.Length,40);}}L.English=false;}
    }
}
