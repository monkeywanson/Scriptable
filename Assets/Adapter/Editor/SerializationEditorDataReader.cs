using CLRSharp.Adapter.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameLib.Editors.Unity
{
    /// <summary>
    /// 序列化编辑器数据读取
    /// </summary>
    public class SerializationEditorDataReader
    {
        /// <summary>
        /// 写入值
        /// </summary>
        public static void Read(ref SerializationEditorData serializationEditorData, UnityEngine.Object content)
        {
            List<PropertyData> sortedPropertyData = new List<PropertyData>(serializationEditorData.propertyDatas);
            sortedPropertyData.RemoveAll(p => string.IsNullOrEmpty(p.propertyPath));
            sortedPropertyData.Sort((p1, p2) => 
            {
                int depth1 = p1.PropertyNames.Length;
                int depth2 = p2.PropertyNames.Length;
                if (depth1 != depth2) return depth1.CompareTo(depth2);
                return p2.propertyPath.CompareTo(p1.propertyPath);
            });
            foreach (var propertyData in sortedPropertyData)
            {
                Read(propertyData, ref serializationEditorData, content.GetType(), content);
            }
        }

        /// <summary>
        /// 读取属性值
        /// </summary>
        public static void Read(PropertyData propertyData, ref SerializationEditorData serializationEditorData, Type contentType, UnityEngine.Object target)
        {
            switch ((SerializedPropertyType)propertyData.propertyType)
            {
                case SerializedPropertyType.ArraySize:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<int>());
                    break;
                case SerializedPropertyType.Integer:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<long>());
                    break;
                case SerializedPropertyType.Boolean:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<bool>());
                    break;
                case SerializedPropertyType.Float:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<double>());
                    break;
                case SerializedPropertyType.String:
                    SetValue(contentType, target, propertyData.PropertyNames, serializationEditorData.strings[propertyData.Index]);
                    break;
                case SerializedPropertyType.Color:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Color>());
                    break;
                case SerializedPropertyType.ObjectReference:
                    SetValue(contentType, target, propertyData.PropertyNames, serializationEditorData.referencedUnityObjects[propertyData.Index]);
                    break;
                case SerializedPropertyType.LayerMask:
                    SetValue(contentType, target, propertyData.PropertyNames, (LayerMask)propertyData.Get<int>());
                    break;
                case SerializedPropertyType.Enum:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<long>());
                    break;
                case SerializedPropertyType.Vector2:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Vector2>());
                    break;
                case SerializedPropertyType.Vector3:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Vector3>());
                    break;
                case SerializedPropertyType.Vector4:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Vector4>());
                    break;
                case SerializedPropertyType.Rect:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Rect>());
                    break;
                case SerializedPropertyType.Character:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<char>());
                    break;
                case SerializedPropertyType.AnimationCurve:
                    SetValue(contentType, target, propertyData.PropertyNames, serializationEditorData.animationCurves[propertyData.Index]);
                    break;
                case SerializedPropertyType.Bounds:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Bounds>());
                    break;
                case SerializedPropertyType.Gradient:
                    SetValue(contentType, target, propertyData.PropertyNames, serializationEditorData.gradients[propertyData.Index]);
                    break;
                case SerializedPropertyType.Quaternion:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Quaternion>());
                    break;
                case SerializedPropertyType.Vector2Int:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Vector2Int>());
                    break;
                case SerializedPropertyType.Vector3Int:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<Vector3Int>());
                    break;
                case SerializedPropertyType.RectInt:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<RectInt>());
                    break;
                case SerializedPropertyType.BoundsInt:
                    SetValue(contentType, target, propertyData.PropertyNames, propertyData.Get<BoundsInt>());
                    break;
            }
        }

        /// <summary>
        /// 设置值
        /// </summary>
        public static void SetValue(Type contentType, object target, string[] propertyNames, object value)
        {
            object content = target;
            FieldInfo field = null;

            Stack<ReflectionData> reflectionDatas = new Stack<ReflectionData>();
            try
            {
                for (int i = 0; i < propertyNames.Length; i++)
                {
                    string propertyName = propertyNames[i];
                    if (contentType.IsArray || (contentType.IsGenericType && contentType.GetGenericTypeDefinition() == typeof(List<>)))
                    {
                        if (propertyName == "Array") continue;
                        if (propertyName == "size")
                        {
                            if (i != propertyNames.Length - 1 || field == null || !(value is int) || content == null)
                                throw new NotImplementedException();
                            int size = (int)value;
                            if (contentType.IsArray)
                            {
                                value = Array.CreateInstance(contentType.GetElementType(), size);
                                field.SetValue(content, value);
                                reflectionDatas.Peek().target = value;
                            }
                            else
                            {
                                IList list = target as IList;
                                Type elementType = contentType.GetGenericArguments()[0];
                                bool isUnityObject = typeof(UnityEngine.Object).IsAssignableFrom(elementType);
                                while(list.Count < size)
                                    list.Add(isUnityObject ? null : Activator.CreateInstance(elementType));
                            }
                            return;
                        }
                        if (propertyName.StartsWith("data[") && propertyName.EndsWith("]"))
                        {
                            if (!(target is IList))
                                throw new NotImplementedException();
                            int index = int.Parse(propertyName.Substring("data[".Length, propertyName.Length - "data[]".Length));
                            if (i == propertyNames.Length - 1) //设置值
                            {
                                if (contentType.IsArray) contentType = contentType.GetElementType();
                                else contentType = contentType.GetGenericArguments()[0];
                                try
                                {
                                    if (contentType.IsEnum)
                                        value = Enum.ToObject(contentType, value);
                                    else if (contentType.IsPrimitive)
                                        value = Convert.ChangeType(value, contentType);
                                    ((IList)target)[index] = value;
                                }
                                catch
                                {
                                    //不匹配直接丢弃数据
                                }
                            }
                            else //获取值
                            {
                                content = target;
                                target = ((IList)content)[index];
                                if (contentType.IsArray) contentType = contentType.GetElementType();
                                else contentType = contentType.GetGenericArguments()[0];

                                if (target == null && !typeof(UnityEngine.Object).IsAssignableFrom(contentType))
                                {
                                    target = Activator.CreateInstance(contentType);
                                    ((IList)content)[index] = target;
                                }

                                if (contentType.IsValueType)
                                {
                                    reflectionDatas.Push(new ReflectionData()
                                    {
                                        target = content,
                                        handle = index,
                                        value = target,
                                    });
                                }
                                continue;
                            }
                            return;
                        }
                        throw new NotImplementedException("不存在的属性!!!");
                    }
                    else
                    {
                        field = contentType.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                        if (field == null) //域删除了放弃
                        {
                            Debug.LogWarning($"域被删除{contentType}.{propertyName}");
                            return;
                        }

                        if (i == propertyNames.Length - 1)
                        {
                            try
                            {
                                if (field.FieldType.IsEnum)
                                    value = Enum.ToObject(field.FieldType, value);
                                else if(field.FieldType.IsPrimitive)
                                    value = Convert.ChangeType(value, field.FieldType);
                                field.SetValue(target, value);
                            }
                            catch
                            { 
                                //不匹配直接丢弃数据
                            }
                        }
                        else
                        {
                            content = target;
                            target = field.GetValue(content);
                            if (target == null && !typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                            {
                                target = Activator.CreateInstance(field.FieldType);
                                field.SetValue(content, target);
                            }
                            contentType = field.FieldType;

                            reflectionDatas.Push(new ReflectionData()
                            {
                                target = content,
                                handle = field,
                                value = target,
                            });
                        }
                    }
                }
            }
            finally
            {
                while (reflectionDatas.Count > 0)
                    reflectionDatas.Pop().UpdateValue();
            }
        }

        /// <summary>
        /// 反射数据
        /// </summary>
        private class ReflectionData
        {
            /// <summary>
            /// 目标
            /// </summary>
            public object target;
            /// <summary>
            /// 句柄
            /// </summary>
            public object handle;
            /// <summary>
            /// 值
            /// </summary>
            public object value;


            /// <summary>
            /// 更新值
            /// </summary>
            public void UpdateValue()
            {
                if (handle is FieldInfo fieldInfo)
                    fieldInfo.SetValue(target, value);
                else if (target is IList list && handle is int index)
                    list[index] = value;
            }
        }
    }
}