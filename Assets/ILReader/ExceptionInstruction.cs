using System;

namespace GameLib.CSharp
{
	/// <summary>
	/// 异常指令
	/// </summary>
	public class ExceptionInstruction : Instruction
	{
		/// <summary>
		/// 位置
		/// </summary>
		public int Position { get; private set; }
		/// <summary>
		/// 异常类型
		/// </summary>
		public ExceptionHandler ExceptionHandlerType { get; private set; }
		/// <summary>
		/// 异常捕获类型
		/// </summary>
		public Type CatchType { get; private set; }

		/* constructor */
		/// <summary>
		/// 构造方法
		/// </summary>
		public ExceptionInstruction(int p, ExceptionHandler e, Type c)
		{
			Position = p;
			ExceptionHandlerType = e;
			CatchType = c;
		}

		/// <summary>
		/// 打印IL
		/// </summary>
		public override string DisplayIL()
		{
			switch (ExceptionHandlerType)
			{
				case ExceptionHandler.Try:
					return ".try" + Environment.NewLine + "{";
				case ExceptionHandler.Filter:
					return "}" + Environment.NewLine + "filter" + Environment.NewLine + "{";
				case ExceptionHandler.Catch:
					return "}" + Environment.NewLine + "catch " + CatchType.FullName + Environment.NewLine + "{";
				case ExceptionHandler.Finally:
					return "}" + Environment.NewLine + "finally" + Environment.NewLine + "{";
				case ExceptionHandler.EndException:
					return "}";
				case ExceptionHandler.Fault:
					return "}" + Environment.NewLine + "finally" + Environment.NewLine + "{";
				case ExceptionHandler.EndFilter:
					return "endfilter" + Environment.NewLine + "}" + Environment.NewLine + "{";
				default:
					Console.WriteLine("Error: Unknown exception handling call, no exception handler emitted");
					return "Error";
			}
		}

	}
}
