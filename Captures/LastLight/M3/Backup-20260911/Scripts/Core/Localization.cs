using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LastLight
{
    // Text records remain stable across language changes and save/load.
    public static partial class L
    {
        public static bool English;
        private static readonly Regex Token=new Regex("\uE000([^\uE001]+)\uE001",RegexOptions.Compiled);
        public static string K(string key)=>"\uE000"+key+"\uE001";
        public static string F(string key,params object[] args)
        {
            var parts=new string[args.Length+1];parts[0]=key;
            for(int i=0;i<args.Length;i++)
            {
                string format=null;var match=Regex.Match(Zh[key],@"\{"+i+@"(?:,-?\d+)?(?::([^}]+))?\}");if(match.Success&&match.Groups[1].Success)format=match.Groups[1].Value;
                string value=args[i] is IFormattable f?f.ToString(format,CultureInfo.InvariantCulture):args[i]?.ToString()??"";
                parts[i+1]=Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
            }
            return "\uE000"+string.Join("|",parts)+"\uE001";
        }
        public static string Resolve(string text)
        {
            if(string.IsNullOrEmpty(text))return text??"";
            return Token.Replace(text,m=>{
                var parts=m.Groups[1].Value.Split('|');var table=English?En:Zh;
                if(!table.TryGetValue(parts[0],out var value))throw new KeyNotFoundException("Missing localization: "+parts[0]);
                if(parts.Length==1)return value;
                var args=new object[parts.Length-1];for(int i=1;i<parts.Length;i++)args[i-1]=Resolve(Encoding.UTF8.GetString(Convert.FromBase64String(parts[i])));
                value=Regex.Replace(value,@"(\{\d+(?:,-?\d+)?):[^}]+\}","$1}");return string.Format(CultureInfo.InvariantCulture,value,args);
            });
        }
    }
}
