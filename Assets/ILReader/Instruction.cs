using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;

namespace GameLib.CSharp
{
	/// <summary>
	/// 操作指令
	/// </summary>
	public class Instruction
	{
		/// <summary>
		/// 指令名称
		/// </summary>
		public Code Code { get; private set; }

		/// <summary>
		/// 指令参数
		/// </summary>
		public object Operand = string.Empty;

		/// <summary>
		/// 前缀指令
		/// </summary>
		public Instruction Prefix;

		/// <summary>
		/// 标记，用来追踪指令
		/// </summary>
		public string Label;

		/// <summary>
		/// 是否是前缀指令
		/// </summary>
		public bool IsPrefix;

		/// <summary>
		/// 指令所在程序集
		/// </summary>
		public Assembly workingAssm;
		
		/// <summary>
		/// 指令所在类型
		/// </summary>
		public Type workingType;

		/* constructors */
		/// <summary>
		/// 构造方法
		/// </summary>
		public Instruction()
		{
			Operand = null;
			Prefix = null;
			IsPrefix = false;
		}

		/// <summary>
		/// 构造方法paramval
		/// </summary>
		public Instruction(Code code, object operand)
		{
			Code = code;
			Operand = operand;
			Prefix = null;
			IsPrefix = false;
		}
		
		/* methods */
		/// <summary>
		/// 打印IL
		/// </summary>
		public virtual string DisplayIL()
		{
			var s = new StringBuilder();
			if (Label != null)
				s.Append(Label + ":");
			s.Append(Code.ToString() + " ");                   // This appends the name
			if (Operand != null)                   // This appends the parameter(s) if any
			{
				if (typeof(MethodBase).IsInstanceOfType(Operand))
				{
					MethodBase mb = (MethodBase)Operand;
					if (!mb.IsStatic)
						s.Append("instance ");
					s.Append(Instruction.PrintMethod(mb, workingAssm, workingType));

				}
				else if (typeof(ArrayList).IsInstanceOfType(Operand))
				{
					s.Append("(" + Environment.NewLine);
					int count = 0;
					foreach (LabelTarget T in (ArrayList)Operand)  // This is for a switch with several targets
						if ((++count) == ((ArrayList)Operand).Count)
							s.Append("\t" + T.ToString() + ")");
						else s.Append("\t" + T.ToString() + "," + Environment.NewLine);
				}
				else s.Append(Operand.ToString());
			}
			return s.ToString();
		}

		/* common methods */
		/// <summary>
		/// 字符串
		/// </summary>
        public override string ToString()
        {
			return DisplayIL();
		}

		/* static methods */
        /// <summary>
        /// 打印类型
        /// </summary>
        public static string PrintTypeWithAssem(Type t, Assembly workingAssm)
		{
			if (t == null) throw new ArgumentNullException(nameof(t));
			string s = "";
			if (!t.IsPrimitive && (t != typeof(string)) && (t != typeof(object)) && (t.Assembly != workingAssm))
				s = ("[" + t.Assembly.GetName().Name + "]");
			return (s + t);

		}

		/// <summary>
		/// 打印方法
		/// </summary>
		public static string PrintMethod(MethodBase mb, Assembly workingAssm, Type workingType)
		{
			if (mb == null) throw new ArgumentNullException(nameof(mb));
			var s = new StringBuilder();
			if (typeof(MethodInfo).IsInstanceOfType(mb))
			{
				s.Append(PrintTypeWithAssem(((MethodInfo)mb).ReturnType, workingAssm));
				s.Append(" ");
			}
			if (mb.DeclaringType != null)
			{
				if (mb.DeclaringType.Assembly != workingAssm)
					s.Append("[" + mb.DeclaringType.Assembly.GetName().Name + "]");
				if (mb.DeclaringType != workingType)
					s.Append(mb.DeclaringType + "::"); // we don't do the check to see if the method is declared in the same body with the calling site
			}
			s.Append(mb.Name);
			if (mb.IsGenericMethodDefinition)
			{
				Type[] ts = mb.GetGenericArguments(); // we must have at least one generic argument	
				s.Append(" <");
				for (int i = 0; i < ts.Length; i++)
				{
					s.Append("("); // we should always have some generic constrains
					Type[] cts = ts[i].GetGenericParameterConstraints();
					Array.Sort(cts, new StringCompare());
					for (int j = 0; j < cts.Length; j++)
					{
						s.Append(PrintTypeWithAssem(cts[j], workingAssm));
						if (j < cts.Length - 1)
							s.Append(", ");
					}
					s.Append(") ");
					s.Append(ts[i]);
					if (i < (ts.Length - 1))
						s.Append(", ");
				}
				s.Append(">");
			}
			else if (mb.IsGenericMethod)
			{
				s.Append(" <");
				Type[] param = mb.GetGenericArguments();
				for (int i = 0; i < param.Length; i++)
				{
					s.Append(PrintTypeWithAssem(param[i], workingAssm));
					if (i < (param.Length - 1))
						s.Append(", ");
				}
				s.Append("> ");
			}
			s.Append(" (");
			int count = 0;
			foreach (ParameterInfo pa in mb.GetParameters())
			{
				if (pa.ParameterType.IsGenericParameter)
				{
					if (pa.ParameterType.DeclaringMethod != null)
						s.Append("!!" + pa.ParameterType.GenericParameterPosition + " " + pa.ParameterType);
					else s.Append("!" + pa.ParameterType.GenericParameterPosition + " " + pa.ParameterType);

				}
				else
				{
					s.Append(PrintTypeWithAssem(pa.ParameterType, workingAssm));
				}
				if ((++count) < mb.GetParameters().Length)
					s.Append(", ");
				else s.Append(")");
			}
			if (count == 0) s.Append(")");
			return s.ToString();
		}

		/// <summary>
		/// 创建指令
		/// </summary>
		public static Instruction Create(Code code, object operand = null)
		{
			return new Instruction(code, operand);
		}

		/* inner class */
		/// <summary>
		/// 字符串比较器
		/// </summary>
		private class StringCompare : IComparer
		{
			int IComparer.Compare(Object x, Object y)
			{
				if (x.ToString().Equals(y.ToString()))
					return 1;
				else return string.Compare(x.ToString(), y.ToString());
			}
		}
	}
}
