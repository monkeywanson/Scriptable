#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Reflection;

namespace CLRSharp.Adapter.Unity
{
    /// <summary>
    /// 序列化数据
    /// @author 汪松民
    /// </summary>
    [Serializable]
    public struct SerializationEditorData
    {
        /* static methods */
        /// <summary>
        /// 更新数据到预览对象
        /// </summary>
        /// <param name="previewObject"></param>
        /// <param name="target"></param>
        /// <param name="runtimeType"></param>
        public static void UpdateData(object previewObject, object target, Type runtimeType)
        {
            var previewType = previewObject.GetType();
            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var fields = previewType.GetFields(bindingFlags);
            foreach (var field in fields)
            {
                var runtimeField = runtimeType.GetField(field.Name, bindingFlags);
                if (runtimeField == null) continue;
                field.SetValue(previewObject, runtimeField.GetValue(target));
            }
        }

        /* fields */
        /// <summary>
        /// 行为类型名称
        /// </summary>
        public MonoScript script;
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
        /// <summary>
        /// 属性值
        /// </summary>
        public PropertyData[] propertyDatas;

        /* methods */
        /// <summary>
        /// 反序列化
        /// 将编辑数据反序列化到运行时数据上
        /// </summary>
        public unsafe void Deserialize(object adapter, Type runtimeType)
        {
            ObjectData objectData = ObjectData.Create(ref this, runtimeType);
            objectData.Deserialize(ref adapter, ref this);
        }

        /// <summary>
        /// 将编辑器数据序列化到非编辑器序列化数据
        /// </summary>
        public unsafe void Serialize(ref SerializationData serializetionData)
        {
            if (script == null)
            {
                Debug.LogWarning($"Script {script} Missing");
                return;
            }

            Type contentType = script.GetClass();
            serializetionData.behaviourTypeName = contentType.AssemblyQualifiedName;
            serializetionData.referencedUnityObjects = referencedUnityObjects;
            serializetionData.animationCurves = animationCurves;
            serializetionData.gradients = gradients;

            List<string> strings = new List<string>(this.strings);

            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);

            ObjectData objectData = ObjectData.Create(ref this, script.GetClass());
            objectData.Serialize(writer, ref this, strings);
            writer.Flush();

            serializetionData.strings = strings.ToArray();
            serializetionData.serializedBytes = stream.ToArray();
        }

        /// <summary>
        /// 写入值
        /// </summary>
        public unsafe void WriteValue<T>(BinaryWriter writer, List<string> strings, string propertyName, SerializationData.DataType dataType, T value) where T : unmanaged
        {
            //name
            if (!propertyName.StartsWith("data["))
            {
                //type
                writer.Write((byte)dataType);

                int index = strings.IndexOf(propertyName);
                if (index < 0)
                {
                    index = strings.Count;
                    strings.Add(propertyName);
                }
                writer.Write(index);
            }

            //value
            int size = sizeof(T);
            byte* bytes = (byte*)&value;
            for (int i = 0; i <size; i++)
                writer.Write(bytes[i]);
        }

        /// <summary>
        /// 字符串
        /// </summary>
        public override string ToString()
        {
            return script == null ? "Missing Script" : script.GetClass().FullName;
        }


        /// <summary>
        /// 对象数据
        /// </summary>
        public class ObjectData
        {
            /* static methods */
            /// <summary>
            /// 根据编辑器数据创建对象
            /// </summary>
            public static ObjectData Create(ref SerializationEditorData serializationEditorData, Type contentType)
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

                ObjectData objectData = new ObjectData("Root", contentType);
                foreach (var propertyData in sortedPropertyData)
                {
                    var temp = propertyData;
                    objectData.AddPropertyData(ref temp, temp.PropertyNames, 0);
                }
                return objectData;
            }

            /* delegate fields */
            /// <summary>
            /// 设置器
            /// </summary>
            public delegate void Setter(ref object obj, object value);

            /* fields */
            /// <summary>
            /// 属性名称
            /// </summary>
            string propertyName;

            /// <summary>
            /// 内容类型
            /// </summary>
            Type contentType;

            /// <summary>
            /// 值设置器
            /// </summary>
            Setter setter;

            /// <summary>
            /// 值获取器
            /// </summary>
            Func<object, object> getter;

            /// <summary>
            /// 属性值
            /// </summary>
            PropertyData propertyData;

            /// <summary>
            /// 域
            /// </summary>
            Dictionary<string, ObjectData> fields = new Dictionary<string, ObjectData>();

            /* constructor */
            /// <summary>
            /// 构造方法
            /// </summary>
            public ObjectData(string propertyName, Type contentType, Setter setter = null, Func<object, object> getter = null)
            {
                this.propertyName = propertyName;
                this.contentType = contentType;
                this.setter = setter;
                this.getter = getter;
                propertyData.propertyType = (int)SerializedPropertyType.Generic;
            }

            /* method */
            /// <summary>
            /// 添加属性
            /// </summary>
            public void AddPropertyData(ref PropertyData propertyData, string[] propertyNames, int propertyIndex)
            {
                if (propertyIndex >= propertyNames.Length) return;

                string propertyName = propertyNames[propertyIndex];
                if (propertyData.propertyType == (int)SerializedPropertyType.ArraySize)
                {
                    if (propertyName == "Array")
                    {
                        AddPropertyData(ref propertyData, propertyNames, propertyIndex + 1);
                        return;
                    }
                    if (propertyIndex == propertyNames.Length - 1 && propertyName == "size")
                    {
                        return;
                    }
                }

                if (fields.TryGetValue(propertyName, out var objectData))
                {
                    objectData.AddPropertyData(ref propertyData, propertyNames, propertyIndex + 1);
                }
                else
                {
                    if (this.propertyData.propertyType == (int)SerializedPropertyType.ArraySize && propertyName == "Array")
                    {
                        AddPropertyData(ref propertyData, propertyNames, propertyIndex + 1);
                        return;
                    }

                    Type elementType = null;
                    Setter setter = null;
                    Func<object, object> getter = null;
                    if (contentType != null)
                    {
                        if (contentType.IsArray)
                        {
                            elementType = contentType.GetElementType();
                            setter = (ref object target, object value) => target = value;
                        }
                        else if (typeof(IList).IsAssignableFrom(contentType))
                        {
                            elementType = contentType.GetGenericArguments()[0];
                            setter = (ref object target, object value) => target = value;
                        }
                        else
                        {
                            var field = contentType.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            if (field != null)
                            {
                                elementType = field.FieldType;
                                setter = (ref object target, object value) =>
                                {
                                    if (elementType.IsEnum)
                                        value = Convert.ChangeType(value, elementType.GetEnumUnderlyingType());
                                    else if (elementType.IsPrimitive)
                                        value = Convert.ChangeType(value, elementType);
                                    field.SetValue(target, value);
                                };
                                getter = (target) =>
                                {
                                    return field.GetValue(target);
                                };
                            }
                        }
                    }
                    objectData = new ObjectData(propertyName, elementType, setter, getter);
                    objectData.propertyData = propertyData;
                    fields.Add(propertyName, objectData);
                    objectData.AddPropertyData(ref propertyData, propertyNames, propertyIndex + 1);
                }
            }

            /// <summary>
            /// 序列化(SerializationEditorData->SerializationData)
            /// </summary>
            public void Serialize(BinaryWriter writer, ref SerializationEditorData serializationEditorData, List<string> strings)
            {
                if (contentType == null) return;

                if (fields.Count == 0)
                {
                    //数据与类型不匹配，调整了代码，但是没有重新去编辑Prefab
                    if (!CheckData((SerializedPropertyType)propertyData.propertyType, contentType)) return;

                    string typeName = ((SerializedPropertyType)propertyData.propertyType).ToString();
                    Enum.TryParse<SerializationData.DataType>(typeName, out var dataType);

                    switch ((SerializedPropertyType)propertyData.propertyType)
                    {
                        case SerializedPropertyType.Integer:
                        case SerializedPropertyType.Enum:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<long>());
                            break;
                        case SerializedPropertyType.Boolean:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<bool>());
                            break;
                        case SerializedPropertyType.Float:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<double>());
                            break;
                        case SerializedPropertyType.String:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Index);
                            break;
                        case SerializedPropertyType.Color:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Color>());
                            break;
                        case SerializedPropertyType.LayerMask:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, (LayerMask)propertyData.Get<int>());
                            break;
                        case SerializedPropertyType.Vector2:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Vector2>());
                            break;
                        case SerializedPropertyType.Vector3:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Vector3>());
                            break;
                        case SerializedPropertyType.Vector4:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Vector4>());
                            break;
                        case SerializedPropertyType.Rect:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Rect>());
                            break;
                        case SerializedPropertyType.Character:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<char>());
                            break;
                        case SerializedPropertyType.Bounds:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Bounds>());
                            break;
                        case SerializedPropertyType.Quaternion:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Quaternion>());
                            break;
                        case SerializedPropertyType.Vector2Int:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Vector2Int>());
                            break;
                        case SerializedPropertyType.Vector3Int:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<Vector3Int>());
                            break;
                        case SerializedPropertyType.RectInt:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<RectInt>());
                            break;
                        case SerializedPropertyType.BoundsInt:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Get<BoundsInt>());
                            break;

                        case SerializedPropertyType.AnimationCurve:
                        case SerializedPropertyType.Gradient:
                        case SerializedPropertyType.ObjectReference:
                            serializationEditorData.WriteValue(writer, strings, propertyName, dataType, propertyData.Index);
                            break;
                    }
                    return;
                }

                if (propertyData.propertyType == (int)SerializedPropertyType.ArraySize)
                {
                    List<KeyValuePair<string, ObjectData>> array = new List<KeyValuePair<string, ObjectData>>(fields);
                    array.Sort((x, y) => x.Key.CompareTo(y.Key));
                    if (array.Count != propertyData.Get<int>()) throw new Exception("数据与长度不匹配");

                    MemoryStream stream = new MemoryStream();
                    BinaryWriter memberWriter = new BinaryWriter(stream);

                    int index = strings.IndexOf(this.contentType.AssemblyQualifiedName);
                    if (index < 0) { index = strings.Count; strings.Add(this.contentType.AssemblyQualifiedName); }

                    memberWriter.Write(index);
                    memberWriter.Write(array.Count);

                    if (array.Count > 0)
                    {
                        if (array[0].Value.propertyData.propertyPath.EndsWith("]"))
                        {
                            string typeName = ((SerializedPropertyType)array[0].Value.propertyData.propertyType).ToString();
                            Enum.TryParse<SerializationData.DataType>(typeName, out var dataType);
                            memberWriter.Write((byte)dataType);
                        }
                        else
                        {
                            memberWriter.Write((byte)SerializationData.DataType.Generic);
                        }
                    }
                    
                    foreach (var temp in array)
                        temp.Value.Serialize(memberWriter, ref serializationEditorData, strings);

                    memberWriter.Flush();
                    byte[] bytes = stream.ToArray();
                    serializationEditorData.WriteValue(writer, strings, propertyName, SerializationData.DataType.Array, bytes.Length);
                    writer.Write(bytes);
                } 
                else
                {
                    List<KeyValuePair<string, ObjectData>> validFields = new List<KeyValuePair<string, ObjectData>>();
                    foreach (var field in fields)
                    {
                        if (field.Value.contentType == null) continue;
                        if(field.Value.fields.Count == 0 && !field.Value.CheckData((SerializedPropertyType)field.Value.propertyData.propertyType, field.Value.contentType)) continue;
                        validFields.Add(field);
                    }

                    MemoryStream stream = new MemoryStream();
                    BinaryWriter memberWriter = new BinaryWriter(stream);
                    int index = strings.IndexOf(this.contentType.AssemblyQualifiedName);
                    if (index < 0) { index = strings.Count; strings.Add(this.contentType.AssemblyQualifiedName); }
                    memberWriter.Write(index);
                    memberWriter.Write(validFields.Count);
                    
                    foreach (var field in validFields)
                    {
                        field.Value.Serialize(memberWriter, ref serializationEditorData, strings);
                    }
                    memberWriter.Flush();

                    byte[] bytes = stream.ToArray();
                    serializationEditorData.WriteValue(writer, strings, propertyName, SerializationData.DataType.Generic, bytes.Length);
                    writer.Write(bytes);
                }
            }

            /// <summary>
            /// 序列化
            /// </summary>
            public void Serialize(object adapter, ref SerializationEditorData serializationEditorData)
            {
                try
                {
                    if (contentType == null) return;

                    if (fields.Count == 0)
                    {
                        if (getter == null) return;
                        //数据与类型不匹配，调整了代码，但是没有重新去编辑Prefab
                        if (!CheckData((SerializedPropertyType)propertyData.propertyType, contentType)) return;

                        string typeName = ((SerializedPropertyType)propertyData.propertyType).ToString();
                        Enum.TryParse<SerializationData.DataType>(typeName, out var dataType);

                        switch ((SerializedPropertyType)propertyData.propertyType)
                        {
                            case SerializedPropertyType.Integer:
                            case SerializedPropertyType.Enum:
                                propertyData.Set((long)Convert.ChangeType(getter.Invoke(adapter), typeof(long)));
                                break;
                            case SerializedPropertyType.Boolean:
                                propertyData.Set((bool)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Float:
                                propertyData.Set((double)Convert.ChangeType(getter.Invoke(adapter), typeof(double)));
                                break;
                            case SerializedPropertyType.Color:
                                propertyData.Set((Color)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.LayerMask:
                                if (getter != null) propertyData.Set((LayerMask)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Vector2:
                                propertyData.Set((Vector2)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Vector3:
                                propertyData.Set((Vector3)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Vector4:
                                propertyData.Set((Vector4)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Rect:
                                propertyData.Set((Rect)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Character:
                                propertyData.Set((char)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Bounds:
                                propertyData.Set((Bounds)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Quaternion:
                                propertyData.Set((Quaternion)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Vector2Int:
                                propertyData.Set((Vector2Int)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.Vector3Int:
                                propertyData.Set((Vector3Int)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.RectInt:
                                propertyData.Set((RectInt)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.BoundsInt:
                                propertyData.Set((BoundsInt)getter.Invoke(adapter));
                                break;
                            case SerializedPropertyType.String:
                                {
                                    string value = (string)getter.Invoke(adapter);
                                    serializationEditorData.strings[propertyData.Index] = value;
                                }
                                break;
                            case SerializedPropertyType.AnimationCurve:
                                {
                                    AnimationCurve value = (AnimationCurve)getter.Invoke(adapter);
                                    serializationEditorData.animationCurves[propertyData.Index] = value;
                                }
                                break;
                            case SerializedPropertyType.Gradient:
                                {
                                    Gradient value = (Gradient)getter.Invoke(adapter);
                                    serializationEditorData.gradients[propertyData.Index] = value;
                                }
                                break;
                            case SerializedPropertyType.ObjectReference:
                                {
                                    UnityEngine.Object value = (UnityEngine.Object)getter.Invoke(adapter);
                                    serializationEditorData.referencedUnityObjects[propertyData.Index] = value;
                                }
                                break;
                        }
                        return;
                    }

                    if (propertyData.propertyType == (int)SerializedPropertyType.ArraySize)
                    {
                        List<KeyValuePair<string, ObjectData>> array = new List<KeyValuePair<string, ObjectData>>(fields);
                        array.Sort((x, y) => x.Key.CompareTo(y.Key));
                        if (array.Count != propertyData.Get<int>()) throw new Exception("数据与长度不匹配");

                        if (contentType.IsArray)
                        {
                            Array value = (Array)getter.Invoke(adapter);
                            for (int i = 0; i < array.Count; i++)
                            {
                                object elementValue = value.GetValue(i);
                                array[i].Value.Serialize(elementValue, ref serializationEditorData);
                            }
                            propertyData.Set(value.Length);
                        }
                        else if (contentType.IsGenericType && contentType.GetGenericTypeDefinition() == typeof(List<>))
                        {
                            IList value = (IList)getter.Invoke(adapter);

                            int count = value == null ? 0 : value.Count;
                            
                            for (int i = 0; i < count; i++)
                                array[i].Value.Serialize(value[i], ref serializationEditorData);

                            propertyData.Set(count);
                        }
                    }
                    else
                    {
                        List<KeyValuePair<string, ObjectData>> validFields = new List<KeyValuePair<string, ObjectData>>();
                        foreach (var field in fields)
                        {
                            if (field.Value.contentType == null) continue;
                            validFields.Add(field);
                        }
                        if (validFields.Count <= 0) return;
                        if (getter != null) adapter = getter.Invoke(adapter);
                        foreach (var temp in validFields)
                        {
                            temp.Value.Serialize(adapter, ref serializationEditorData);
                        }
                    }
                }
                finally
                {
                    for (int i = 0; i < serializationEditorData.propertyDatas.Length; i++)
                    {
                        if (propertyData.propertyPath != serializationEditorData.propertyDatas[i].propertyPath) continue;
                        serializationEditorData.propertyDatas[i] = propertyData;
                    }
                }
            }

            /// <summary>
            /// 反序列化
            /// </summary>
            public void Deserialize(ref object adapter, ref SerializationEditorData serializationEditorData)
            {
                if (contentType == null) return;

                if (fields.Count == 0)
                {
                    //数据与类型不匹配，调整了代码，但是没有重新去编辑Prefab
                    if (!CheckData((SerializedPropertyType)propertyData.propertyType, contentType)) return;

                    string typeName = ((SerializedPropertyType)propertyData.propertyType).ToString();
                    Enum.TryParse<SerializationData.DataType>(typeName, out var dataType);

                    switch ((SerializedPropertyType)propertyData.propertyType)
                    {
                        case SerializedPropertyType.Integer:
                        case SerializedPropertyType.Enum:
                            setter?.Invoke(ref adapter, propertyData.Get<long>());
                            break;
                        case SerializedPropertyType.Boolean:
                            setter?.Invoke(ref adapter, propertyData.Get<bool>());
                            break;
                        case SerializedPropertyType.Float:
                            setter?.Invoke(ref adapter, propertyData.Get<double>());
                            break;
                        case SerializedPropertyType.String:
                            setter?.Invoke(ref adapter, propertyData.Index);
                            break;
                        case SerializedPropertyType.Color:
                            setter?.Invoke(ref adapter, propertyData.Get<Color>());
                            break;
                        case SerializedPropertyType.LayerMask:
                            setter?.Invoke(ref adapter, (LayerMask)propertyData.Get<int>());
                            break;
                        case SerializedPropertyType.Vector2:
                            setter?.Invoke(ref adapter, propertyData.Get<Vector2>());
                            break;
                        case SerializedPropertyType.Vector3:
                            setter?.Invoke(ref adapter, propertyData.Get<Vector3>());
                            break;
                        case SerializedPropertyType.Vector4:
                            setter?.Invoke(ref adapter, propertyData.Get<Vector4>());
                            break;
                        case SerializedPropertyType.Rect:
                            setter?.Invoke(ref adapter, propertyData.Get<Rect>());
                            break;
                        case SerializedPropertyType.Character:
                            setter?.Invoke(ref adapter, propertyData.Get<char>());
                            break;
                        case SerializedPropertyType.Bounds:
                            setter?.Invoke(ref adapter, propertyData.Get<Bounds>());
                            break;
                        case SerializedPropertyType.Quaternion:
                            setter?.Invoke(ref adapter, propertyData.Get<Quaternion>());
                            break;
                        case SerializedPropertyType.Vector2Int:
                            setter?.Invoke(ref adapter, propertyData.Get<Vector2Int>());
                            break;
                        case SerializedPropertyType.Vector3Int:
                            setter?.Invoke(ref adapter, propertyData.Get<Vector3Int>());
                            break;
                        case SerializedPropertyType.RectInt:
                            setter?.Invoke(ref adapter, propertyData.Get<RectInt>());
                            break;
                        case SerializedPropertyType.BoundsInt:
                            setter?.Invoke(ref adapter, propertyData.Get<BoundsInt>());
                            break;
                        case SerializedPropertyType.AnimationCurve:
                            setter?.Invoke(ref adapter, serializationEditorData.animationCurves[propertyData.Index]);
                            break;
                        case SerializedPropertyType.Gradient:
                            setter?.Invoke(ref adapter, serializationEditorData.gradients[propertyData.Index]);
                            break;
                        case SerializedPropertyType.ObjectReference:
                            setter?.Invoke(ref adapter, serializationEditorData.referencedUnityObjects[propertyData.Index]);
                            break;
                    }
                    return;
                }

                if (propertyData.propertyType == (int)SerializedPropertyType.ArraySize)
                {
                    List<KeyValuePair<string, ObjectData>> array = new List<KeyValuePair<string, ObjectData>>(fields);
                    array.Sort((x, y) => x.Key.CompareTo(y.Key));
                    if (array.Count != propertyData.Get<int>())
                        throw new Exception("数据与长度不匹配");

                    if (contentType.IsArray)
                    {
                        var elementType = contentType.GetElementType();
                        Array value = Array.CreateInstance(elementType, array.Count);
                        for (int i = 0; i < array.Count; i++)
                        {
                            object elementValue = typeof(UnityEngine.Object).IsAssignableFrom(elementType) ? null : Activator.CreateInstance(elementType);
                            array[i].Value.Deserialize(ref elementValue, ref serializationEditorData);
                            value.SetValue(elementValue, i);
                        }
                        setter?.Invoke(ref adapter, value);
                    }
                    else if (contentType.IsGenericType && contentType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var elementType = contentType.GetGenericArguments()[0];
                        IList value = Activator.CreateInstance(contentType) as IList;
                        foreach (var temp in array)
                        {
                            object elementValue = typeof(UnityEngine.Object).IsAssignableFrom(elementType) ? null : Activator.CreateInstance(elementType);
                            temp.Value.Deserialize(ref elementValue, ref serializationEditorData);
                            if (elementType.IsEnum)
                                elementValue = Enum.ToObject(elementType, Convert.ChangeType(elementValue, elementType.GetEnumUnderlyingType()));
                            else if(elementType.IsPrimitive)
                                elementValue = Convert.ChangeType(elementValue, elementType);
                            value.Add(elementValue);
                        }
                        setter?.Invoke(ref adapter, value);
                    }
                }
                else
                {
                    List<KeyValuePair<string, ObjectData>> validFields = new List<KeyValuePair<string, ObjectData>>();
                    foreach (var field in fields)
                    {
                        if (field.Value.contentType == null) continue;
                        validFields.Add(field);
                    }
                    if (validFields.Count <= 0) return;

                    object fieldValue = adapter;
                    if (getter != null) fieldValue = getter.Invoke(adapter);
                    if(fieldValue == null) fieldValue = typeof(UnityEngine.Object).IsAssignableFrom(contentType) ? null : Activator.CreateInstance(contentType);
                    foreach (var temp in validFields)
                        temp.Value.Deserialize(ref fieldValue, ref serializationEditorData);
                    setter?.Invoke(ref adapter, fieldValue);
                }
            }

            /// <summary>
            /// 打印
            /// </summary>
            public void Print(StringBuilder builder, ref SerializationEditorData serializationEditorData, int depth)
            {
                if (fields.Count == 0)
                {
                    builder.Append(propertyName).Append(":");
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
                            builder.Append(serializationEditorData.strings[propertyData.Index]);
                            break;
                        case SerializedPropertyType.Color:
                            builder.Append(propertyData.Get<Color>());
                            break;
                        case SerializedPropertyType.ObjectReference:
                            builder.Append(serializationEditorData.referencedUnityObjects[propertyData.Index]);
                            break;
                        case SerializedPropertyType.LayerMask:
                            builder.Append((LayerMask)propertyData.Get<int>());
                            break;
                        case SerializedPropertyType.Enum:
                            builder.Append(propertyData.Get<long>());
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
                            builder.Append(serializationEditorData.animationCurves[propertyData.Index]);
                            break;
                        case SerializedPropertyType.Bounds:
                            builder.Append(propertyData.Get<Bounds>());
                            break;
                        case SerializedPropertyType.Gradient:
                            builder.Append(serializationEditorData.gradients[propertyData.Index]);
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
                    return;
                }

                builder.Append(propertyName).AppendLine(":");

                depth++;
                foreach (var field in fields)
                {
                    for (int i = 0; i < depth; i++)
                        builder.Append("    ");
                    field.Value.Print(builder, ref serializationEditorData, depth);
                }
            }


            /// <summary>
            /// 校验类型
            /// </summary>
            public bool CheckData(SerializedPropertyType propertyType, Type contentType)
            {
                if (contentType == null) return false;
                switch (propertyType)
                {
                    case SerializedPropertyType.Integer:
                        if (contentType == typeof(byte)) return true;
                        if (contentType == typeof(sbyte)) return true;
                        if (contentType == typeof(short)) return true;
                        if (contentType == typeof(ushort)) return true;
                        if (contentType == typeof(int)) return true;
                        if (contentType == typeof(uint)) return true;
                        if (contentType == typeof(long)) return true;
                        if (contentType == typeof(ulong)) return true;
                        return false;
                    case SerializedPropertyType.Boolean:
                        return contentType == typeof(byte);
                    case SerializedPropertyType.Float:
                        return contentType == typeof(float) || contentType == typeof(double);
                    case SerializedPropertyType.String:
                        return contentType == typeof(string);
                    case SerializedPropertyType.Color:
                        return contentType == typeof(Color);
                    case SerializedPropertyType.ObjectReference:
                        return typeof(UnityEngine.Object).IsAssignableFrom(contentType);
                    case SerializedPropertyType.LayerMask:
                        return contentType == typeof(LayerMask);
                    case SerializedPropertyType.Enum:
                        return contentType.IsEnum;
                    case SerializedPropertyType.Vector2:
                        return contentType == typeof(Vector2);
                    case SerializedPropertyType.Vector3:
                        return contentType == typeof(Vector3);
                    case SerializedPropertyType.Vector4:
                        return contentType == typeof(Vector4);
                    case SerializedPropertyType.Rect:
                        return contentType == typeof(Rect);
                    case SerializedPropertyType.Character:
                        return contentType == typeof(char);
                    case SerializedPropertyType.AnimationCurve:
                        return contentType == typeof(AnimationCurve);
                    case SerializedPropertyType.Bounds:
                        return contentType == typeof(Bounds);
                    case SerializedPropertyType.Gradient:
                        return contentType == typeof(Gradient);
                    case SerializedPropertyType.Quaternion:
                        return contentType == typeof(Quaternion);
                    case SerializedPropertyType.Vector2Int:
                        return contentType == typeof(Vector2Int);
                    case SerializedPropertyType.Vector3Int:
                        return contentType == typeof(Vector3Int);
                    case SerializedPropertyType.RectInt:
                        return contentType == typeof(RectInt);
                    case SerializedPropertyType.BoundsInt:
                        return contentType == typeof(BoundsInt);
                    default:
                        return false;
                }
            }
        }
    }

    /// <summary>
    /// 索引属性
    /// </summary>
    [Serializable]
    public struct PropertyData
    {
        /* fields */
        /// <summary>
        /// 属性类型
        /// </summary>
        public int propertyType;
        /// <summary>
        /// 属性路径
        /// </summary>
        public string propertyPath;
        /// <summary>
        /// 属性数据
        /// </summary>
        public BoundsInt data;

        /* properties */
        /// <summary>
        /// 数据索引
        /// </summary>
        public int Index
        {
            get => data.x;
            set => data.x = value;
        }
        /// <summary>
        /// 路径
        /// </summary>
        public string[] PropertyNames 
        {
            get => propertyPath.Split('.');
        }

        /// <summary>
        /// 属性名称
        /// </summary>
        public string PropertyName
        {
            get
            {
                int index = propertyPath.LastIndexOf(".");
                return propertyPath.Substring(index + 1);
            }
        }

        /* methods */
        /// <summary>
        /// 设置值
        /// </summary>
        public unsafe void Set<T>(T value) where T : unmanaged
        {
            if (sizeof(T) > sizeof(BoundsInt)) throw new NotSupportedException("Data Out Of Size");
            BoundsInt data = new BoundsInt();
            *(T*)&data = value;
            this.data = data;
        }

        /// <summary>
        /// 获取值
        /// </summary>
        public unsafe T Get<T>() where T : unmanaged
        {
            if (sizeof(T) > sizeof(BoundsInt)) throw new NotSupportedException("Data Out Of Size");
            BoundsInt data = this.data;
            return *(T*)&data;
        }

        public override string ToString()
        {
            return propertyPath;
        }
    }
}
#endif