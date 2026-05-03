#region FeiSharp Assembly
#endregion
#region Import
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static FeiSharpTerminal3._1.Introduction;
#endregion
namespace FeiSharpTerminal3._1
{
    public class NameSelecter
    {
        private string Name = "";
        public NameSelecter(Lang lang)
        {
            if (lang == Lang.English)
            {
                Name = "FeiSharp";
            }
            else if (lang == Lang.Chinese)
            {
                Name = "飞沙铺";
            }
            else if (lang == Lang.Japanese)
            {
                Name = "すなや";
            }
            else if (lang == Lang.Byelorussian)
            {
                Name = "Фейшапу";
            }
            else
            {
                Name = "Песочный стеллаж";
            }
        }
        public string GetName()
        {
            return Name;
        }
    }
    public enum Lang
    {
        Chinese,
        English,
        Russian,
        Japanese,
        Byelorussian
    }
    public static class Introduction
    {
        public static Lang Lang = Lang.English;
        public static NameSelecter NameSelecter { get { return new NameSelecter(Lang); } }
        public static string GetIntroduction()
        {
            return @"
Introduction
1.FeiSharp Overview
FeiSharp is a powerful PL.
This is the terminal.
2.Name
Chinese:飞沙铺
English:FeiSharp
Japanese:すなや
Byelorussian:Фейшапу
Russian:Песочный стеллаж
3.Using
(1):keyword+(args);
(2):If keyword have not args, it will be 'keyword+;'
(3):Usual Keyword:print(text);import(name);define commandname name value;var name = value;set(varname,newvalue);func FunctionName(args)[DoAnyThings;]
            ";
        }
    }
}
