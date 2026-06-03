using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeiSharpStudio;
namespace FeiSharpStudio.ClassInstance
{
    internal class ClassInfo
    {
        public string Name {  get; set; }
        public string? BaseClassName { get; set; }
        public FunctionInfo? ConstructorInfo { get; set; }
        public Dictionary<string,FunctionInfo> _FunctionInfo {  get; set; }
        public Dictionary<string,object> _Vars { get; set; }
        public ClassInfo(Dictionary<string,FunctionInfo> functionInfos,Dictionary<string,object> vars,string name, FunctionInfo? constructorInfo = null, string? baseClassName = null) {
            _FunctionInfo = new(functionInfos);
            _Vars = new(vars);
            Name = name;
            ConstructorInfo = constructorInfo;
            BaseClassName = baseClassName;
        }
    }
}
