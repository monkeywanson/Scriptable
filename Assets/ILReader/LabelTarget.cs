using System.Collections;
using System.Reflection.Emit;

namespace GameLib.CSharp
{
	/// <summary>
	/// 标签目标
	/// </summary>
    public class LabelTarget
    {
		/* properties */
		/// <summary>
		/// 名称
		/// </summary>
		public string Name { get; private set; }

		/* constructors */
		/// <summary>
		/// 构造方法
		/// </summary>
		public LabelTarget() : this(string.Empty) 
		{ 
		}
		/// <summary>
		/// 带有标签名称的构造方法
		/// </summary>
		/// <param name="name"></param>
		public LabelTarget(string name)
		{
			Name = name; 
		}

		/* common methods */
		/// <summary>
		/// 字符串
		/// </summary>
		public override string ToString()
		{
			return Name;
		}
	}
}
