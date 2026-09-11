using System;
using System.Text;
using System.Text.RegularExpressions;

namespace LastLight
{
    // Self-contained bilingual records survive save/load without runtime registration order.
    // Narrative events additionally have explicit stable IDs; text is never a gameplay key.
    public static class M3Text
    {
        private static readonly Regex Pattern=new Regex("\uE010([A-Za-z0-9+/=]+):([A-Za-z0-9+/=]+)\uE011",RegexOptions.Compiled);
        public static string Pair(string zh,string en)=>"\uE010"+Convert.ToBase64String(Encoding.UTF8.GetBytes(zh))+":"+Convert.ToBase64String(Encoding.UTF8.GetBytes(en))+"\uE011";
        public static string Resolve(string value)=>Pattern.Replace(value,m=>Encoding.UTF8.GetString(Convert.FromBase64String(m.Groups[L.English?2:1].Value)));
        public static string Robot(int i)=>Pair(new[]{"梁","芽","守"}[i],new[]{"Liang","Ya","Shou"}[i]);
        public static string Job(M3Job job)=>Pair(new[]{"建筑修复","零件加工","种植维护","安全采集","能源维护","警戒巡查"}[(int)job],new[]{"Repair","Processing","Gardening","Gathering","Energy care","Watch"}[(int)job]);
        public static string Stop(M3Stop reason)=>Pair(new[]{"工作中","岗位关闭","待维修","供电不足","通路受阻","缺少原料","输出已满","没有可用目标"}[(int)reason],new[]{"Working","Disabled","Needs repair","No power","Path blocked","Needs materials","Output full","No usable target"}[(int)reason]);
        public static string Season(M3Season season)=>Pair(new[]{"春","夏","秋","冬"}[(int)season],new[]{"Spring","Summer","Autumn","Winter"}[(int)season]);
    }
}
