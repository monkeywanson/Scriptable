using CLRSharp.Adapter.Unity;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameLib.Editors.Unity
{
    /// <summary>
    /// 序列化写入器
    /// </summary>
    public class SerializationEditorDataWriter
    {
        /* static fields */
        /// <summary>
        /// 标记
        /// </summary>
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// 忽略名单
        /// </summary>
        public readonly static HashSet<string> IgnoreProperties = new HashSet<string>()
        {
            "m_ObjectHideFlags",
            "m_CorrespondingSourceObject",
            "m_PrefabInstance",
            "m_PrefabAsset",
            "m_GameObject",
            "m_Enabled",
            "m_EditorHideFlags",
            "m_Script",
            "m_Name",
            "m_EditorClassIdentifier",
        };

        /* fields */
        /// <summary>
        /// 序列化Unity对象
        /// </summary>
        public List<(int, UnityEngine.Object data)> referencedUnityObjects = new List<(int, UnityEngine.Object)>();
        /// <summary>
        /// 序列化AnimationCurve对象
        /// </summary>
        public List<(int, AnimationCurve data)> animationCurves = new List<(int, AnimationCurve)>();
        /// <summary>
        /// 序列化Gradient对象
        /// </summary>
        public List<(int, Gradient data)> gradients = new List<(int, Gradient)>();
        /// <summary>
        /// 序列化字符串数据
        /// </summary>
        public List<(int, string data)> strings = new List<(int, string)>();
        /// <summary>
        /// 历史属性记录
        /// </summary>
        Dictionary<string, (int index, PropertyData data)> propertyDatas = new Dictionary<string, (int index, PropertyData data)>();

        /* methods */
        /// <summary>
        /// 清理
        /// </summary>
        public void Clear()
        {
            referencedUnityObjects.Clear();
            animationCurves.Clear();
            gradients.Clear();
            strings.Clear();
            propertyDatas.Clear();
        }

        /// <summary>
        /// 写入值
        /// </summary>
        public void Write(ref SerializationEditorData serializationEditorData, UnityEngine.Object content)
        {
            if (!(content is MonoBehaviour)) throw new Exception("content isn't MonoBehaviour");

            var propertyDatas = serializationEditorData.propertyDatas;
            int length = propertyDatas == null ? 0 : propertyDatas.Length;
            for (int i = 0; i < length; i++)
            {
                var propertyData = propertyDatas[i];
                if (string.IsNullOrEmpty(propertyData.propertyPath)) continue; //占位数据
                this.propertyDatas.Add(propertyData.propertyPath, (i, propertyData));
            }

            Type contentType = content.GetType();
            if (serializationEditorData.script && serializationEditorData.script.GetClass() != contentType)
                throw new Exception("不支持更换脚本!");

            SerializedObject serializedObject = new SerializedObject(content);
            var iterator = serializedObject.GetIterator();

            //收集当前属性数据
            List<(int index, PropertyData data)> newPropertyDatas = new List<(int index, PropertyData data)>();
            WriteObject(contentType, iterator, newPropertyDatas);
            var propertyDataResults = GetResult(newPropertyDatas, data => string.IsNullOrEmpty(data.propertyPath));
            var referencedUnityObjectsResults = GetResult(referencedUnityObjects, data => data == null);
            var animationCurvesResults = GetResult(animationCurves, data => data == null);
            var gradientsResults = GetResult(gradients, data => data == null);
            var stringsResults = GetResult(strings, data => data == null);

            var results = propertyDataResults.ConvertAll(value => value.data);

            //再次处理其他数据
            for (int i = 0; i < results.Count; i++)
            {
                var propertyData = results[i];
                if (string.IsNullOrEmpty(propertyData.propertyPath)) continue;
                switch ((SerializedPropertyType)propertyData.propertyType)
                {
                    case SerializedPropertyType.String:
                        propertyData.Index = stringsResults.FindIndex(value => value.oldIndex == propertyData.Index);
                        break;
                    case SerializedPropertyType.ObjectReference:
                        propertyData.Index = referencedUnityObjectsResults.FindIndex(value => value.oldIndex == propertyData.Index);
                        break;
                    case SerializedPropertyType.AnimationCurve:
                        propertyData.Index = animationCurvesResults.FindIndex(value => value.oldIndex == propertyData.Index);
                        break;
                    case SerializedPropertyType.Gradient:
                        propertyData.Index = gradientsResults.FindIndex(value => value.oldIndex == propertyData.Index);
                        break;
                }
                results[i] = propertyData;
            }

            serializationEditorData.script = MonoScript.FromMonoBehaviour(content as MonoBehaviour);
            serializationEditorData.propertyDatas = results.ToArray();
            serializationEditorData.referencedUnityObjects = referencedUnityObjectsResults.ConvertAll(t => t.data).ToArray();
            serializationEditorData.animationCurves = animationCurvesResults.ConvertAll(t => t.data).ToArray();
            serializationEditorData.gradients = gradientsResults.ConvertAll(t => t.data).ToArray();
            serializationEditorData.strings = stringsResults.ConvertAll(t => t.data).ToArray();

            //StringBuilder builder = new StringBuilder();
            //builder.AppendLine(behaviourTypeName);
            //PrintObject(results, contentType, builder);
            //Debug.LogError(builder);
        }

        /// <summary>
        /// 打印对象
        /// </summary>
        public void PrintObject(List<PropertyData> propertyDatas, Type contentType, StringBuilder builder)
        {
            Type GetContentType(Type contentType, string[] propertyNames, int index)
            {
                if (index >= propertyNames.Length) return contentType;
                string propertyName = propertyNames[index];
                if (contentType.IsArray || (contentType.IsGenericType && contentType.GetGenericTypeDefinition() == typeof(List<>)))
                {
                    if (propertyName == "Array") { }
                    else if (propertyName == "size") contentType = typeof(int);
                    else if (contentType.IsArray) contentType = contentType.GetElementType();
                    else contentType = contentType.GetGenericArguments()[0];
                }
                else
                {
                    FieldInfo fieldInfo = contentType.GetField(propertyName);
                    contentType = fieldInfo.FieldType;
                }
                return GetContentType(contentType, propertyNames, index + 1);
            }

            List<PropertyData> sortedPropertyData = new List<PropertyData>(propertyDatas);
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
                string[] propertyNames = propertyData.PropertyNames;
                PrintValue(propertyNames.Length, propertyData, SerializationUtility.GetType(contentType, propertyNames), builder);
            }
        }

        /// <summary>
        /// 打印属性
        /// </summary>
        public void PrintValue(int depth, PropertyData propertyData, Type contentType, StringBuilder builder)
        {
            for (int i = 0; i < depth; i++)
                builder.Append("    ");
            builder.Append(propertyData.propertyPath);
            switch ((SerializedPropertyType)propertyData.propertyType)
            {
                case SerializedPropertyType.ArraySize:
                    builder.Append(propertyData.Get<int>());
                    break;
                case SerializedPropertyType.Integer:
                    builder.Append(propertyData.Get<long>());
                    break;
                case SerializedPropertyType.Boolean:
                    builder.Append(propertyData.Get<bool>());
                    break;
                case SerializedPropertyType.Float:
                    builder.Append(propertyData.Get<double>());
                    break;
                case SerializedPropertyType.String:
                    builder.Append(strings[propertyData.Index].data);
                    break;
                case SerializedPropertyType.Color:
                    builder.Append(propertyData.Get<Color>());
                    break;
                case SerializedPropertyType.ObjectReference:
                    builder.Append(referencedUnityObjects[propertyData.Index].data);
                    break;
                case SerializedPropertyType.LayerMask:
                    builder.Append((LayerMask)propertyData.Get<int>());
                    break;
                case SerializedPropertyType.Enum:
                    builder.Append(Enum.ToObject(contentType, propertyData.Get<long>()));
                    break;
                case SerializedPropertyType.Vector2:
                    builder.Append(propertyData.Get<Vector2>());
                    break;
                case SerializedPropertyType.Vector3:
                    builder.Append(propertyData.Get<Vector3>());
                    break;
                case SerializedPropertyType.Vector4:
                    builder.Append(propertyData.Get<Vector4>());
                    break;
                case SerializedPropertyType.Rect:
                    builder.Append(propertyData.Get<Rect>());
                    break;
                case SerializedPropertyType.Character:
                    builder.Append(propertyData.Get<char>());
                    break;
                case SerializedPropertyType.AnimationCurve:
                    builder.Append(animationCurves[propertyData.Index].data);
                    break;
                case SerializedPropertyType.Bounds:
                    builder.Append(propertyData.Get<Bounds>());
                    break;
                case SerializedPropertyType.Gradient:
                    builder.Append(gradients[propertyData.Index].data);
                    break;
                case SerializedPropertyType.Quaternion:
                    builder.Append(propertyData.Get<Quaternion>());
                    break;
                case SerializedPropertyType.Vector2Int:
                    builder.Append(propertyData.Get<Vector2Int>());
                    break;
                case SerializedPropertyType.Vector3Int:
                    builder.Append(propertyData.Get<Vector3Int>());
                    break;
                case SerializedPropertyType.RectInt:
                    builder.Append(propertyData.Get<RectInt>());
                    break;
                case SerializedPropertyType.BoundsInt:
                    builder.Append(propertyData.Get<BoundsInt>());
                    break;
            }
            builder.AppendLine();
        }

        /// <summary>
        /// 写入一个对象
        /// </summary>
        public void WriteObject(Type contentType, SerializedProperty iterator, List<(int index, PropertyData data)> propertyDatas)
        {
            iterator = iterator.Copy();
            bool enterChildren = true;
            Stack<Type> contentTypes = new Stack<Type>();
            contentTypes.Push(contentType);
            while (iterator.Next(enterChildren))
            {
                enterChildren = iterator.propertyType == SerializedPropertyType.Generic;
                string name = iterator.name;
                if (IgnoreProperties.Contains(name)) continue;
                if (iterator.propertyType == SerializedPropertyType.Generic && iterator.type == "Array" && iterator.name == "Array") continue;
                while (iterator.depth + 1 < contentTypes.Count)
                    contentTypes.Pop();
                switch (iterator.propertyType)
                {
                    case SerializedPropertyType.ExposedReference:
                    case SerializedPropertyType.FixedBufferSize:
                    case SerializedPropertyType.ManagedReference:
                        throw new NotSupportedException("暂不支持的序列化");
                    case SerializedPropertyType.Generic:
                        if (iterator.depth + 1 == contentTypes.Count)
                        {
                            contentType = contentTypes.Peek();
                            if (contentType.IsArray || (contentType.IsGenericType && contentType.GetGenericTypeDefinition() == typeof(List<>)))
                            {
                                if (contentType.IsArray) contentTypes.Push(contentType.GetElementType());
                                else contentTypes.Push(contentType.GetGenericArguments()[0]);
                            }
                            else
                            {
                                FieldInfo field = contentType.GetField(name, Flags);
                                if (field == null)
                                {
                                    Debug.LogError($"{contentType}, Can't Find Field {name} {iterator.propertyType}");
                                    continue;
                                }
                                contentTypes.Push(field.FieldType);
                            }
                        }
                        break;
                    case SerializedPropertyType.ArraySize:
                        WriteValue(propertyDatas, typeof(int), iterator);
                        break;
                    default:
                        contentType = contentTypes.Peek();
                        if (contentType.IsArray || (contentType.IsGenericType && contentType.GetGenericTypeDefinition() == typeof(List<>)))
                        {
                            if (contentType.IsArray) WriteValue(propertyDatas, contentType.GetElementType(), iterator);
                            else WriteValue(propertyDatas, contentType.GetGenericArguments()[0], iterator);
                        }
                        else
                        {
                            FieldInfo field = contentType.GetField(name, Flags);
                            if (field == null)
                            {
                                Debug.LogError($"{contentType}, Can't Find Field {name} {iterator.propertyType}");
                                continue;
                            }
                            WriteValue(propertyDatas, field.FieldType, iterator);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// 写入属性
        /// </summary>
        public void WriteValue(List<(int index, PropertyData data)> propertyDatas, Type propertyType, SerializedProperty property)
        {
            PropertyData propertyData = new PropertyData()
            {
                propertyPath = property.propertyPath,
                propertyType = (int)property.propertyType,
            };

            int index = - propertyDatas.Count - 1;
            if (this.propertyDatas.TryGetValue(propertyData.propertyPath, out var value))
                index = value.index;

            switch (property.propertyType)
            {
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.LayerMask:
                    propertyData.Set(property.intValue);
                    break;
                case SerializedPropertyType.Boolean:
                    propertyData.Set(property.boolValue);
                    break;
                case SerializedPropertyType.Float:
                    propertyData.Set(property.doubleValue);
                    break;
                case SerializedPropertyType.Color:
                    propertyData.Set(property.colorValue);
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    propertyData.Set(property.longValue);
                    break;
                case SerializedPropertyType.Vector2:
                    propertyData.Set(property.vector2Value);
                    break;
                case SerializedPropertyType.Vector3:
                    propertyData.Set(property.vector3Value);
                    break;
                case SerializedPropertyType.Vector4:
                    propertyData.Set(property.vector4Value);
                    break;
                case SerializedPropertyType.Rect:
                    propertyData.Set(property.rectValue);
                    break;
                case SerializedPropertyType.Bounds:
                    propertyData.Set(property.boundsValue);
                    break;
                case SerializedPropertyType.Quaternion:
                    propertyData.Set(property.quaternionValue);
                    break;
                case SerializedPropertyType.Vector2Int:
                    propertyData.Set(property.vector2IntValue);
                    break;
                case SerializedPropertyType.Vector3Int:
                    propertyData.Set(property.vector3IntValue);
                    break;
                case SerializedPropertyType.RectInt:
                    propertyData.Set(property.rectIntValue);
                    break;
                case SerializedPropertyType.BoundsInt:
                    propertyData.Set(property.boundsIntValue);
                    break;
                case SerializedPropertyType.String:
                case SerializedPropertyType.ObjectReference:
                case SerializedPropertyType.AnimationCurve:
                case SerializedPropertyType.Gradient:
                    if (this.propertyDatas.TryGetValue(propertyData.propertyPath, out value))
                        propertyData.Index = value.index;
                    else
                        propertyData.Index = - strings.Count - 1;
                    switch (property.propertyType)
                    {
                        case SerializedPropertyType.String:
                            strings.Add((propertyData.Index, property.stringValue));
                            break;
                        case SerializedPropertyType.AnimationCurve:
                            animationCurves.Add((propertyData.Index, property.animationCurveValue));
                            break;
                        case SerializedPropertyType.ObjectReference:
                            referencedUnityObjects.Add((propertyData.Index, property.objectReferenceValue));
                            break;
                        case SerializedPropertyType.Gradient:
                            var gradientValue = (Gradient)property.GetType().GetProperty("gradientValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(property);
                            gradients.Add((propertyData.Index, gradientValue));
                            break;
                    }
                    break;
            }
            propertyDatas.Add((index, propertyData));
        }

        /// <summary>
        /// 获取结果
        /// </summary>
        /// <param name="emptyCheck">空数据检查</param>
        public List<(int oldIndex, T data)> GetResult<T>(List<(int index, T data)> datas, Func<T, bool> emptyCheck)
        {
            List<(int oldIndex, T data)> results = new List<(int oldIndex, T data)>();
            //先填充已知结果
            foreach (var data in datas)
            {
                if (data.index < 0) continue;
                while (results.Count <= data.index)
                    results.Add(default);
                results[data.index] = data;
            }
            //再填充新添加内容
            foreach (var data in datas)
            {
                if (data.index >= 0) continue;
                //查找一个历史空位索引
                int newIndex = -1;
                for (int i = 0; i < results.Count; i++)
                {
                    if (!emptyCheck.Invoke(results[i].data)) continue;
                    newIndex = i;
                    break;
                }
                if (newIndex == -1) //未找到将添加一个空位
                {
                    newIndex = results.Count;
                    results.Add(default);
                }
                results[newIndex] = data;
            }
            return results;
        }
    }
}