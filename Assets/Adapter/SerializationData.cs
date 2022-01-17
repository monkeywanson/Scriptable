using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace CLRSharp.Adapter.Unity
{
    /// <summary>
    /// 序列化数据
    /// @author 汪松民
    /// </summary>
    [Serializable]
    public struct SerializationData
    {
        /* fields */
        /// <summary>
        /// 行为类型名称
        /// </summary>
        public string behaviourTypeName;
        /// <summary>
        /// 序列化字节
        /// </summary>
        public byte[] serializedBytes;
        /// <summary>
        /// 序列化Unity对象
        /// </summary>
        public UnityEngine.Object[] referencedUnityObjects;
        /// <summary>
        /// 序列化AnimationCurve对象
        /// </summary>
        public AnimationCurve[] animationCurves;
        /// <summary>
        /// 序列化Gradient对象
        /// </summary>
        public Gradient[] gradients;
        /// <summary>
        /// 字符串列表
        /// </summary>
        public string[] strings;

        /* methods */
        /// <summary>
        /// 反序列化
        /// </summary>
        public unsafe void Deserialize(object target)
        {
            int length = serializedBytes.Length;
            string[] strings = this.strings;
            
            fixed (byte* p = serializedBytes)
            {
                ReadObject(p, length, target, target.GetType());
            }
        }

        /// <summary>
        /// 读取对象数据
        /// </summary>
        public unsafe void ReadObject(byte* p, int length, object value, Type contentType)
        {
            if (length < 9) throw new Exception("Bag Image!!!");

            //data type = Generic
            int dataType = *p;
            Debug.Assert(dataType == (int)DataType.Generic);

            //name 
            string root = strings[*(int*)(p + 1)];
            Debug.Assert(root == "Root");

            //classSize
            int classDataSize = *(int*)(p + 5);
            if(length < classDataSize + 9) throw new Exception("Bag Image!!!");

            string typeName = strings[*(int*)(p + 9)];
            if (contentType != Type.GetType(typeName)) //数据类型不匹配
                return;

            int fieldNum = *(int*)(p + 13);
            int offset = 17;
            for (int i = 0; i < fieldNum; i++)
            {
                if (offset + 5 > length) throw new Exception("Bad Image!!!");

                int fieldDataType = p[offset];
                offset += 1;

                //fieldName
                string fieldName = strings[*(int*)(p + offset)];
                offset += 4;

                var fieldInfo = contentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (fieldInfo == null)
                {
                    Debug.LogError($"Field {fieldName} Not Find In Type {contentType.FullName}");
                }

                (object fieldValue, int size) = ReadObject((DataType)fieldDataType, fieldInfo == null ? null: fieldInfo.FieldType, p + offset, length - offset, fieldName);
                if (fieldValue != null) fieldInfo.SetValue(value, fieldValue);

                offset += size;
            }
        }

        /// <summary>
        /// 读取对象
        /// </summary>
        public unsafe (object, int) ReadObject(DataType dataType, Type contentType, byte* p, int length, string debugPath)
        {
            switch (dataType)
            {
                case DataType.Generic:
                    if (length < sizeof(int)) throw new Exception("Bad Imaged !!!");
                    int classDataSize = *(int*)p;
                    if (length < classDataSize + 4) throw new Exception("Bad Imaged !!!");

                    string fieldTypeName = strings[*(int*)(p + 4)];
                    if (contentType == Type.GetType(fieldTypeName))
                    {
                        object value = Activator.CreateInstance(contentType);

                        int fieldNum = *(int*)(p + 8);
                        int offset = 12;
                        for (int i = 0; i < fieldNum; i++)
                        {
                            if (offset + 5 > length) throw new Exception("Bad Imaged !!!");

                            //fieldDataType 
                            int fieldDataType = p[offset];
                            offset += 1;

                            //fieldName
                            string fieldName = strings[*(int*)(p + offset)];
                            offset += 4;

                            var fieldInfo = contentType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (fieldInfo == null)
                            {
                                Debug.LogError($"Field {fieldName} Not Find In Type {contentType.FullName}");
                            }
                            (object fieldValue,int size) = ReadObject((DataType)fieldDataType, fieldInfo == null ? null : fieldInfo.FieldType, p + offset, length - offset, debugPath + "." + fieldName);
                            if (fieldValue != null) fieldInfo.SetValue(value, fieldValue);

                            offset += size;
                        }
                        return (value, classDataSize + 4);
                    }
                    return (null, classDataSize + 4);
                case DataType.Integer:
                    if(length < sizeof(long)) throw new Exception("Bad Imaged !!!");
                    try
                    {
                        object value = *(long*)(p);
                        value = Convert.ChangeType(value, contentType);
                        return (value, sizeof(long));
                    }
                    catch
                    {
                    }
                    return (null, sizeof(long));
                case DataType.Float:
                    if (length < sizeof(double)) throw new Exception("Bad Imaged !!!");
                    if (contentType == typeof(float))
                        return ((float)(*(double*)(p)), sizeof(double));
                    else if (contentType == typeof(double))
                        return (*(double*)p, sizeof(double));
                    return (null, sizeof(double));
                case DataType.Enum:
                    if (length < sizeof(long)) throw new Exception("Bad Imaged !!!");
                    try
                    {
                        if (contentType.IsEnum)
                        {
                            object value = *(long*)(p);
                            value = Convert.ChangeType(value, contentType.GetEnumUnderlyingType());
                            return (value, sizeof(long));
                        }
                    }
                    catch
                    {
                    }
                    return (null, sizeof(long));
                case DataType.Array:
                    if (length < sizeof(int)) throw new Exception("Bad Imaged !!!");
                    classDataSize = *(int*)(p);
                    if (length < classDataSize + 4) throw new Exception("Bad Imaged !!!");

                    fieldTypeName = strings[*(int*)(p + 4)];
                    if (contentType == Type.GetType(fieldTypeName))
                    {
                        int arrayLength = *(int*)(p + 8);
                        int fieldDataType = *(p + 12);

                        int offset = 13;
                        
                        if (contentType.IsArray)
                        {
                            var elementType = contentType.GetElementType();
                            var array = Array.CreateInstance(elementType, arrayLength);
                            for (int i = 0; i < arrayLength; i++)
                            {
                                (object elementValue, int size) = ReadObject((DataType)fieldDataType, elementType, p + offset, length - offset, debugPath + "." + i);
                                if (elementValue != null) array.SetValue(elementValue, i);
                                offset += size;
                            }
                            return (array, classDataSize + 4);
                        }
                        else if (typeof(IList).IsAssignableFrom(contentType))
                        {
                            var value = Activator.CreateInstance(contentType) as IList;
                            var elementType = contentType.GetGenericArguments()[0];
                            var defalutValue = elementType.IsValueType ? Activator.CreateInstance(elementType) : null;
                            for (int i = 0; i < arrayLength; i++)
                            {
                                (object elementValue, int size) = ReadObject((DataType)fieldDataType, elementType, p + offset, length - offset, debugPath + "." + i);
                                if (elementValue != null)
                                    value.Add(elementValue);
                                else
                                    value.Add(defalutValue);
                                offset += size;
                            }
                            return (value, classDataSize + 4);
                        }
                    }
                    return (null, classDataSize + 4);
                case DataType.AnimationCurve:
                    if (contentType == typeof(AnimationCurve))
                        return (animationCurves[*(int*)(p)], sizeof(int));
                    return (null, sizeof(int));
                case DataType.Gradient:
                    if (contentType == typeof(Gradient))
                        return (gradients[*(int*)(p)], sizeof(int));
                    return (null, sizeof(int));
                case DataType.String:
                    if (contentType == typeof(string))
                        return (strings[*(int*)p], sizeof(int));
                    return (null, sizeof(int));
                case DataType.ObjectReference:
                    if (typeof(UnityEngine.Object).IsAssignableFrom(contentType))
                    {
                        UnityEngine.Object value = referencedUnityObjects[*(int*)p];
                        if (value != null && contentType.IsAssignableFrom(value.GetType()))
                            return (value, sizeof(int));
                    }
                    return (null, sizeof(int));
                case DataType.Character:
                    return CheckAndReturn<BoundsInt>(contentType, p, length);
                case DataType.LayerMask:
                    return CheckAndReturn<LayerMask>(contentType, p, length);
                case DataType.Color:
                    return CheckAndReturn<Color>(contentType, p, length);
                case DataType.Boolean:
                    return CheckAndReturn<bool>(contentType, p, length);
                case DataType.Vector2:
                    return CheckAndReturn<Vector2>(contentType, p, length);
                case DataType.Vector3:
                    return CheckAndReturn<Vector3>(contentType, p, length);
                case DataType.Vector4:
                    return CheckAndReturn<Vector4>(contentType, p, length);
                case DataType.Rect:
                    return CheckAndReturn<Rect>(contentType, p, length);
                case DataType.Bounds:
                    return CheckAndReturn<Bounds>(contentType, p, length);
                case DataType.Quaternion:
                    return CheckAndReturn<Quaternion>(contentType, p, length);
                case DataType.Vector2Int:
                    return CheckAndReturn<Vector2Int>(contentType, p, length);
                case DataType.Vector3Int:
                    return CheckAndReturn<Vector3Int>(contentType, p, length);
                case DataType.RectInt:
                    return CheckAndReturn<RectInt>(contentType, p, length);
                case DataType.BoundsInt:
                    return CheckAndReturn<BoundsInt>(contentType, p, length);
                default:
                    throw new Exception("Not Support DataType" + dataType);
            }
        }

        /// <summary>
        /// 校验并返回数据
        /// </summary>
        public unsafe (object value, int size) CheckAndReturn<T>(Type targetType, byte* p, int length) where T : unmanaged
        {
            int needBytes = sizeof(T);
            if (length < needBytes) throw new Exception("Bad Image!!!");
            if (targetType != typeof(T)) return (null, needBytes);
            return (*(T*)p, needBytes);
        }

        /// <summary>
        /// 数据类型
        /// </summary>
        public enum DataType
        {
            /// <summary>
            /// 指示需要创建对象
            /// </summary>
            Generic,
            /// <summary>
            /// 整数
            /// </summary>
            Integer,
            /// <summary>
            /// 布尔
            /// </summary>
            Boolean,
            /// <summary>
            /// 单精度浮点数
            /// </summary>
            Float,
            /// <summary>
            /// 字符串
            /// </summary>
            String,
            /// <summary>
            /// 颜色
            /// </summary>
            Color,
            /// <summary>
            /// Unity对象引用
            /// </summary>
            ObjectReference,
            /// <summary>
            /// LayerMask
            /// </summary>
            LayerMask,
            /// <summary>
            /// 枚举
            /// </summary>
            Enum,
            /// <summary>
            /// 向量2
            /// </summary>
            Vector2,
            /// <summary>
            /// 向量3
            /// </summary>
            Vector3,
            /// <summary>
            /// 向量4
            /// </summary>
            Vector4,
            /// <summary>
            /// 矩形区域
            /// </summary>
            Rect,
            /// <summary>
            /// 数组
            /// </summary>
            Array,
            /// <summary>
            /// 字符
            /// </summary>
            Character,
            /// <summary>
            /// 动画曲线
            /// </summary>
            AnimationCurve,
            /// <summary>
            /// 边界
            /// </summary>
            Bounds,
            /// <summary>
            /// 渐变
            /// </summary>
            Gradient,
            /// <summary>
            /// 四元数
            /// </summary>
            Quaternion,
            /// <summary>
            /// 整型向量2
            /// </summary>
            Vector2Int,
            /// <summary>
            /// 整型向量3
            /// </summary>
            Vector3Int,
            /// <summary>
            /// 整型区域
            /// </summary>
            RectInt,
            /// <summary>
            /// 整型边界
            /// </summary>
            BoundsInt,
        }
    }
}
