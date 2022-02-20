using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GameLib.CSharp
{
	/// <summary>
	/// IL读取器
	/// </summary>
	public unsafe class ILReader
	{
		/* static fields */
		/// <summary>
		/// 操作码
		/// </summary>
		private readonly static IDictionary<int, OpCode> opcodes =
				typeof(OpCodes).GetFields().Select(fi => (OpCode)fi.GetValue(null)).ToDictionary((op) => (int)op.Value);

		/* fields */
		/// <summary>
		/// 方法
		/// </summary>
		readonly MethodBase methodBase;
		/// <summary>
		/// 方法体
		/// </summary>
		readonly MethodBody methodBody;
		/// <summary>
		/// 模块
		/// </summary>
		readonly Module curModule;
		/// <summary>
		/// 异常指令集
		/// </summary>
		readonly List<ExceptionInstruction> ExceptionHandlers;// This keeps track of the exception blocks in a method.
		/// <summary>
		/// IL指令数据
		/// </summary>
		readonly byte[] IL;
		/// <summary>
		/// 代码开始位置
		/// </summary>
		int positionOfCode;
		/// <summary>
		/// 指令偏移位置
		/// </summary>
		int position;
		/// <summary>
		/// 工作程序集
		/// </summary>
		readonly Assembly workingAssm;

		/* properties */
		/// <summary>
		/// 指令集
		/// </summary>
		public List<Instruction> Instructions { get; private set; }

		/* constructors */
		/// <summary>
		/// 构造方法
		/// </summary>
		public ILReader(MethodBase methodInfo, Assembly workingAssm)
		{
			this.workingAssm = workingAssm;
			Instructions = new List<Instruction>();
			methodBase = methodInfo;
			ExceptionHandlers = new List<ExceptionInstruction>();

			if (methodInfo == null)
			{
				Console.WriteLine("Error, input method is not specified, can not continue");
				return;
			}
			methodBody = methodInfo.GetMethodBody();
			curModule = methodInfo.DeclaringType.Module;

			position = 0;
			positionOfCode = 0;

			IL = methodBody.GetILAsByteArray();

			IList<ExceptionHandlingClause> ehClauses = methodBody.ExceptionHandlingClauses;
			foreach (ExceptionHandlingClause ehclause in ehClauses)
			{
				ExceptionHandlers.Add(new ExceptionInstruction(ehclause.TryOffset, ExceptionHandler.Try, null));
				switch (ehclause.Flags)
				{
					//case 0:
					case ExceptionHandlingClauseOptions.Clause:
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset, ExceptionHandler.Catch, ehclause.CatchType));
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset + ehclause.HandlerLength, ExceptionHandler.EndException, null));
						break;
					//case 1:
					case ExceptionHandlingClauseOptions.Filter:
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.FilterOffset, ExceptionHandler.Filter, null));
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset, ExceptionHandler.EndFilter, null));
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset + ehclause.HandlerLength, ExceptionHandler.EndException, null));
						break;
					//case 2:
					case ExceptionHandlingClauseOptions.Finally:
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset, ExceptionHandler.Finally, null));
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset + ehclause.HandlerLength, ExceptionHandler.EndException, null));
						break;
					//case 4:
					case ExceptionHandlingClauseOptions.Fault:
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset, ExceptionHandler.Fault, null));
						ExceptionHandlers.Add(new ExceptionInstruction(ehclause.HandlerOffset + ehclause.HandlerLength, ExceptionHandler.EndException, null));
						break;
				}
			}
			// populate opcode
			Instruction instruction = ReadInstrction();
			while (instruction != null)                                             // This cycles through all the Opcodes
			{
				Instructions.Add(instruction);
				instruction = ReadInstrction();
			}

			foreach (var temp in Instructions)
			{
				if (temp.Operand is LabelTarget target)
				{
					temp.Operand = Instructions.Find(ins => ins.Label == target.Name);
					if (temp.Operand == null)
					{
						UnityEngine.Debug.LogError("Not Find");
					}
				}
				if (temp.Code == Code.Switch)
				{
					var list = temp.Operand as ArrayList;
					temp.Operand = Array.ConvertAll(list.ToArray(), o => Instructions.Find(ins => ins.Label == (o as LabelTarget).Name));
				}
			}
		}

		/* methods */
		/// <summary>
		/// 读取下一个指令
		/// </summary>
		Instruction ReadInstrction()
		{
			byte[] il = IL;
			if (position >= IL.Length) return null;
			positionOfCode = position;
			foreach (ExceptionInstruction exception in ExceptionHandlers)
			{
				if (exception.Position == position)
				{
					ExceptionHandlers.Remove(exception);
					return exception;
				}
			}

			int offset = position;
			short op = il[offset];
			int opValue = op;
			offset++;
			if (op == 0xFE)
			{
				opValue = 256 + il[offset];
				op = (short)((op << 8) + il[offset]);
				offset++;
			}
			OpCode opCode = opcodes[op];
			long argument = 0;
			int byteSize;
			switch (opCode.OperandType)
			{
				case OperandType.InlineNone:
					byteSize = 0;
					break;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					byteSize = 1;
					break;
				case OperandType.InlineVar:
					byteSize = 2;
					break;
				case OperandType.InlineI8:
				case OperandType.InlineR:
					byteSize = 8;
					break;
				case OperandType.InlineSwitch:
					position = offset;
					return OpcodeSwitch(Code.Switch);
				default:
					byteSize = 4;
					break;
			}
			if (byteSize > 0)
			{
				long n = 0;
				for (int i = 0; i < byteSize; ++i)
				{
					long v = il[offset + i];
					n += v << (i * 8);
				}
				argument = n;
				offset += byteSize;
			}
			position = offset;

			Code code = (Code)opValue;
            switch (opCode.OperandType)
            {
                case OperandType.InlineBrTarget:
					return CreateInstruction(code, new LabelTarget(string.Format("IL_{0:x4}", offset + (int)argument)));
				case OperandType.InlineField:
				case OperandType.InlineMethod:
				case OperandType.InlineSig:
					return CreateInstruction(code, curModule.ResolveMember((int)argument));
                case OperandType.InlineI:
					return CreateInstruction(code, (int)argument);
				case OperandType.InlineI8:
					return CreateInstruction(code, argument);
				case OperandType.InlineNone:
					switch (code)
					{
						case Code.Volatile:
						case Code.Tail:
							position--;
							var preInstruction = CreateInstruction(code);
							preInstruction.IsPrefix = true;
							Instruction nextInstruction = ReadInstrction();
							nextInstruction.Label = string.Empty;
							nextInstruction.Prefix = preInstruction;
							return nextInstruction;
						default:
							return CreateInstruction(code);
					}
				case OperandType.InlineR:
					return CreateInstruction(code, BitConverter.Int64BitsToDouble(argument));
                case OperandType.InlineString:
					return CreateInstruction(code, curModule.ResolveString((int)argument));
                case OperandType.InlineTok:
					int i = (int)argument;
					byte* bytes = (byte*)&i;
					if (bytes[3] == 4) //field
					{
						return CreateInstruction(code, curModule.ResolveMember(i));
					}
					if ((bytes[3] == 6) || (bytes[3] == 10)) //method
					{
						return CreateInstruction(code, curModule.ResolveMember(i));
					}
					if ((bytes[3] == 1) || (bytes[3] == 2) || (bytes[3] == 0x1b)) //0x1b is testspec.
					{
						return CreateInstruction(code, curModule.ResolveType(i));
					}
					return CreateInstruction(code, i);
				case OperandType.InlineType:
					return CreateInstruction(code, curModule.ResolveType((int)argument));
                case OperandType.InlineVar:
					switch (code)
					{
						case Code.Ldarg:
						case Code.Ldarga:
						case Code.Starg:
							return CreateInstruction(code, methodBase.GetParameters()[(ushort)argument]);
						case Code.Ldloc:
						case Code.Ldloca:
						case Code.Stloc:
							return CreateInstruction(code, methodBody.LocalVariables[(ushort)argument]);
						default:
							return CreateInstruction(code, (short)argument);
					}
                case OperandType.ShortInlineBrTarget:
					return CreateInstruction(code, new LabelTarget(string.Format("IL_{0:x4}", offset + (sbyte)argument)));
				case OperandType.ShortInlineI:
					return CreateInstruction(code, (byte)argument);
				case OperandType.ShortInlineR:
					i = (int)argument;
					return CreateInstruction(code, *(float*)&i);
				case OperandType.ShortInlineVar:
					byte b = (byte)argument;
					switch (code)
					{
						case Code.Unaligned:
							position--;
							var preInstruction = CreateInstruction(code, b);
							preInstruction.IsPrefix = true;
							Instruction nextInstruction = ReadInstrction();
							nextInstruction.Label = "";
							nextInstruction.Prefix = preInstruction;
							return nextInstruction;
						case Code.Ldarg_S:
						case Code.Ldarga_S:
						case Code.Starg_S:
							if (methodBase.IsStatic)
								return CreateInstruction(code, methodBase.GetParameters()[b]);
							if (argument == 0)
								return CreateInstruction(code, new ThisParameterInfo(methodBase.DeclaringType));
							return CreateInstruction(code, methodBase.GetParameters()[b - 1]);
						case Code.Ldloc_S:
						case Code.Ldloca_S:
						case Code.Stloc_S:
							return CreateInstruction(code, methodBody.LocalVariables[b]);
						default:
							return CreateInstruction(code, b);
					}
				default:
					return null;
			}
        }

        /// <summary>
        /// 创建指令
        /// </summary>
        Instruction CreateInstruction(Code code, object operand = null)
		{
			var instruction = Instruction.Create(code, operand);
			instruction.Label = string.Format("IL_{0:x4}", positionOfCode);
			return instruction;
		}

		/// <summary>
		/// Switch指令
		/// </summary>
		Instruction OpcodeSwitch(Code code)
		{
			var labelTargets = new List<LabelTarget>();
			byte[] b = new byte[4];
			for (int k = 0; k < 4; k++)
			{
				b[k] = IL[position];
				position++;
			}
			int i = BitConverter.ToInt32(b,0);				// This tells us how many parameters to expect.
			for (int k = 0; k < i; k++)						// Each parameter will be a four-byte offset
			{
				for (int k1 = 0; k1 < 4; k1++)
				{
					b[k1] = IL[position];
					position++;
				}
				int j = BitConverter.ToInt32(b,0);
				string s1 =string.Format("IL_{0:x4}", positionOfCode + j + 4 * i + 5);
				labelTargets.Add(new LabelTarget(s1));
			}
			return CreateInstruction(code, labelTargets);
		}

		/// <summary>
		/// 字符串
		/// </summary>
        public override string ToString()
        {
			System.Text.StringBuilder builder = new System.Text.StringBuilder();
			builder.AppendLine(methodBase.DeclaringType.Name + ":" + methodBase.Name);
			foreach (var temp in Instructions)
			{
				builder.AppendLine(temp.DisplayIL());
			}
			return builder.ToString();
        }
    }
}