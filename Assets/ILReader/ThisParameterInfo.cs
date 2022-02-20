using System;
using System.Reflection;

namespace GameLib.CSharp
{
    /// <summary>
    /// This参数
    /// </summary>
    public class ThisParameterInfo : ParameterInfo
    {
        private Type thisType;


        public ThisParameterInfo(Type thisType)
        {
            this.thisType = thisType;
        }

        public override Type ParameterType => thisType;

        public override string Name => "this";

        public override int Position => -1;
    }
}
