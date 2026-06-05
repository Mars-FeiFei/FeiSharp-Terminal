using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace FeiSharpStudio
{
    public enum MethodVisibility
    {
        Private,
        Public
    }
    public class FunctionInfo
    {
        public string Name { get; internal set; }
        public List<string> Parameter { get; internal set; }
        public List<Token> FunctionBody { get; internal set; }
        public MethodVisibility Visibility { get; internal set; }
        public bool IsClassMember { get; internal set; }
        public string? DeclaringClassName { get; internal set; }
        public FunctionInfo(
            string name,
            IEnumerable<string> parameter,
            List<Token> functionBody,
            MethodVisibility visibility = MethodVisibility.Private,
            bool isClassMember = false,
            string? declaringClassName = null)
        {
            Name = name;
            Parameter = new(parameter);
            FunctionBody = functionBody;
            Visibility = visibility;
            IsClassMember = isClassMember;
            DeclaringClassName = declaringClassName;
        }
    }
    public class OutputEventArgs : EventArgs
    {
        public string Message { get; set; }
        public string Type { get; set; }
        public OutputEventArgs(string message, string type = "info")
        {
            Message = message;
            Type = type;
        }
    }
}
