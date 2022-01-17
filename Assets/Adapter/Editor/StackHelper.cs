using System;
using System.Diagnostics;
using System.Reflection;

namespace GameLib.CSharp
{
    /// <summary>
    /// 栈帮助器
    /// </summary>
    public static class StackHelper
    {
		/// <summary>
		/// 判定调用来源
		/// </summary>
		public static bool IsCallFrom(Type type, int skipFrame = 2)
        {
			if (skipFrame < 0) skipFrame = 0;
			StackFrame[] frames = new StackTrace().GetFrames();
			for (int i = skipFrame; i < frames.Length; i++)
			{
				MethodBase method = frames[i].GetMethod();
				if (method.DeclaringType != type) continue;
				return true;
			}
			return false;
		}
    }
}
