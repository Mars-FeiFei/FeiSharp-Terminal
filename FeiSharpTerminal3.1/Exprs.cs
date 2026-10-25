using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace FeiSharpStudio
{
    public abstract class Expr { }
    public class UnaryExpr : Expr
    {
        public string Operator { get; }
        public Expr Operand { get; }
        public UnaryExpr(string op, Expr operand)
        {
            Operator = op;
            Operand = operand;
        }
    }
    public class IncrementExpr : Expr
    {
        public string Operator { get; }
        public Expr Operand { get; }
        public bool IsPrefix { get; }

        public IncrementExpr(string op, Expr operand, bool isPrefix)
        {
            Operator = op;
            Operand = operand;
            IsPrefix = isPrefix;
        }
    }
    public class VarExpr : Expr
    {
        public string Name { get; set; }
        public VarExpr(string name)
        {
            Name = name;
        }
    }
    public class ValueExpr : Expr
    {
        public object Value { get; set; }
        public ValueExpr(object value) { Value = value; }
    }
    public class RuntimeValueExpr : Expr
    {
        public object? Value { get; }
        public RuntimeValueExpr(object? value) { Value = value; }
    }
    public class StringExpr : Expr
    {
        public string Value { get; set; }
        public StringExpr(string value) { Value = value; }
    }
    public class VariableExpr : Expr
    {
        public string Name { get; }
        public VariableExpr(string name) { Name = name; }
    }
    public class BinaryExpr : Expr
    {
        public Expr Left { get; }
        public string Operator { get; }
        public Expr Right { get; }
        public BinaryExpr(Expr left, string op, Expr right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
    }
    public class ConditionalExpr : Expr
    {
        public Expr TrueValue { get; }
        public Expr Condition { get; }
        public Expr? FalseValue { get; }

        public ConditionalExpr(Expr trueValue, Expr condition, Expr? falseValue)
        {
            TrueValue = trueValue;
            Condition = condition;
            FalseValue = falseValue;
        }
    }
}
