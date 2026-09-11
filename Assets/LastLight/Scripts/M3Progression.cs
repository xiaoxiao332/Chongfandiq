using System;
using System.Linq;

namespace LastLight
{
    public sealed class M3Action
    {
        public readonly string Id,Name; public readonly Cost[] Costs;
        public M3Action(string id,string zh,string en,params Cost[] costs){Id=id;Name=M3Text.Pair(zh,en);Costs=costs;}
    }
    public static class M3Progression
    {
        public static readonly M3Action[] Actions={
            new M3Action("wake-ya","修复芽","Repair Ya",new Cost(Resource.Parts,2)),
            new M3Action("insulation","取得绝缘样本","Collect insulation sample"),
            new M3Action("relay-road","修复中继站通路","Repair relay access",new Cost(Resource.Wood,8),new Cost(Resource.Parts,4),new Cost(Resource.Fiber,4)),
            new M3Action("emergency-power","启动应急供电","Start emergency power",new Cost(Resource.Fuel,1)),
            new M3Action("wake-shou","修复守","Repair Shou",new Cost(Resource.Parts,3)),
            new M3Action("structure-plan","取得发射结构资料","Recover launch plans"),
            new M3Action("navigation-record","读取导航记录","Read navigation archive"),
            new M3Action("maker-record","读取制造记录","Read maker archive"),
            new M3Action("structure","修复发射设施","Restore launch structure",new Cost(Resource.Scrap,12),new Cost(Resource.Parts,6)),
            new M3Action("power","组装共用动力","Assemble common power",new Cost(Resource.Cell,4),new Cost(Resource.Parts,6)),
            new M3Action("navigation","完成导航","Complete navigation",new Cost(Resource.Parts,4),new Cost(Resource.Cell,2)),
            new M3Action("personal-ship","准备个人航天器","Prepare personal craft",new Cost(Resource.Scrap,10),new Cost(Resource.Parts,5)),
            new M3Action("transport","修复旧运输船","Restore transport",new Cost(Resource.Scrap,16),new Cost(Resource.Parts,8)),
            new M3Action("collective-power","建造集体能源工程","Build collective power",new Cost(Resource.Cell,6),new Cost(Resource.Parts,4)),
            new M3Action("supplies","交付留守补给","Deliver ground supplies",new Cost(Resource.Ration,8),new Cost(Resource.Fuel,6)),
            new M3Action("pulse","制作脉冲发射器","Craft pulse launcher",new Cost(Resource.Scrap,5),new Cost(Resource.Parts,3)),
            new M3Action("clothing","制作环境防护衣","Craft protective clothing",new Cost(Resource.Fiber,6),new Cost(Resource.Parts,1)),
            new M3Action("ya-request","为芽保留非生产植物","Keep Ya's unproductive plant",new Cost(Resource.Wood,2)),
            new M3Action("liang-request","完成梁的公共桌请求","Complete Liang's public table request"),
            new M3Action("shou-request","为守建立共同警戒规则","Agree on Shou's watch rules"),
            new M3Action("du-request","兑现渡的旅途补给承诺","Deliver Du's travel supplies",new Cost(Resource.Ration,4),new Cost(Resource.Fiber,3)),
            new M3Action("cen-repair","向岑解释并补偿","Repair trust with Cen",new Cost(Resource.Parts,2)),
        };
        public static string Missing(string id,M3State s,M1ConstructionSystem c,M2WorldData world)
        {
            if(s.Has("action:"+id))return M3Text.Pair("已完成，不能重复交付。","Already completed; no duplicate delivery.");
            if(!world.bridge)return M3Text.Pair("先完成第一章与桥梁修复。","Complete chapter one and repair the bridge first.");
            bool ok;
            switch(id)
            {
                case "wake-ya":ok=world.samples.All(v=>v);break;
                case "insulation":ok=true;break;
                case "relay-road":ok=s.insulation&&s.robots[1].awake;break;
                case "emergency-power":ok=s.relayRoad;break;
                case "wake-shou":case "navigation-record":case "maker-record":ok=s.emergencyPower;break;
                case "structure-plan":ok=true;break;
                case "structure":ok=s.Has("action:structure-plan")&&s.robots[1].awake;break;
                case "power":ok=s.structure&&s.emergencyPower;break;
                case "navigation":ok=s.power&&s.Has("action:navigation-record")&&s.Has("action:maker-record")&&world.greenhouseRecord;break;
                case "personal-ship":case "transport":case "collective-power":case "supplies":ok=s.navigation;break;
                case "pulse":ok=s.Has("action:structure-plan")&&M3Rules.Usable(c.Grid.Items,Structure.Workbench);break;
                case "clothing":ok=M3Rules.Usable(c.Grid.Items,Structure.Workbench);break;
                case "ya-request":ok=s.robots[1].awake&&M3Rules.Usable(c.Grid.Items,Structure.Planter);break;
                case "liang-request":ok=s.robots[0].awake&&M3Rules.Usable(c.Grid.Items,Structure.Table);break;
                case "shou-request":ok=s.robots[2].awake&&M3Rules.Usable(c.Grid.Items,Structure.WatchLight);break;
                case "du-request":ok=s.duMet;break;
                case "cen-repair":ok=s.duMet;break;
                default:return M3Text.Pair("未知工程。","Unknown project.");
            }
            return ok?"":M3Text.Pair("前置未完成：查看当前目标、样本、人物请求或所需完好设施。","Prerequisite missing: check objectives, samples, character requests and working facilities.");
        }
        public static bool Execute(string id,M3State s,M1ConstructionSystem c,M2WorldData world,Inventory stock,out string reason)
        {
            reason=Missing(id,s,c,world);if(reason!="")return false;
            var action=Actions.First(a=>a.Id==id);
            if(!stock.Pay(action.Costs)){reason=M3Text.Pair("缺少公共仓储材料：","Public storage needs: ")+Catalog.Describe(action.Costs);return false;}
            s.Claim("action:"+id);
            switch(id)
            {
                case "wake-ya":s.robots[1].awake=true;break;
                case "insulation":s.insulation=true;break;
                case "relay-road":s.relayRoad=true;break;
                case "emergency-power":s.emergencyPower=true;break;
                case "wake-shou":s.robots[2].awake=true;break;
                case "structure":s.structure=true;break;
                case "power":s.power=true;break;
                case "navigation":s.navigation=true;break;
                case "personal-ship":s.personalShip=true;break;
                case "transport":s.transport=true;break;
                case "collective-power":s.collectivePower=true;break;
                case "supplies":s.supplyDelivered=true;break;
                case "pulse":s.pulse=true;break;
                case "clothing":s.clothing=true;break;
                case "ya-request":s.robots[1].requestDone=true;break;
                case "liang-request":s.robots[0].requestDone=true;break;
                case "shou-request":s.robots[2].requestDone=true;break;
                case "du-request":s.duRequest=true;break;
                case "cen-repair":s.cenRepaired=true;break;
            }
            return true;
        }
    }
}
