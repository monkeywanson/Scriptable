using System;
using UnityEditor;
using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using CLRSharp.Adapter.Unity;

namespace GameLib.Editors.Unity
{
    /// <summary>
    /// 序列化工具
    /// </summary>
    public static class SerializationUtility
    {
        /// <summary>
        ///  待适配列表
        /// </summary>
        public static HashSet<string> UpdateAssemblyNames = new HashSet<string>()
        {
            "AdapterTest",
        };

        /// <summary>
        /// 是否是适配脚本
        /// </summary>
        public static bool IsScriptType(this Type type)
        {
            return UpdateAssemblyNames.Contains(type.Assembly.GetName().Name);
        }


        [MenuItem("Adapter/PrintSerialize")]
        public static void Print()
        {
            var activeGameObject = Selection.activeGameObject;
            if (activeGameObject == null) return;
            MonoBehaviour[] behaviours = activeGameObject.GetComponents<MonoBehaviour>();

            StringBuilder builder = new StringBuilder();

            foreach (var behaviour in behaviours)
            {
                builder.Length = 0;
                builder.Append("Type:").AppendLine(behaviour.GetType().FullName);
                SerializedObject serializedObject = new SerializedObject(behaviour);
                SerializedProperty iterator = serializedObject.GetIterator();
                builder.AppendLine($"{iterator.type} {iterator.propertyType} {iterator.propertyPath}");
                bool enterChildren = true;
                while (iterator.Next(enterChildren))
                {
                    for (int i = 0; i < iterator.depth; i++)
                        builder.Append("    ");
                    builder.Append($"{iterator.propertyType} {iterator.propertyPath} {iterator.intValue} {iterator.longValue} {iterator.floatValue} {iterator.doubleValue}");
                    builder.AppendLine();
                    enterChildren = iterator.propertyType == SerializedPropertyType.Generic;
                }
                Debug.LogError(builder.ToString());
                Debug.LogError(JsonUtility.ToJson(behaviour));
            }
        }

        [MenuItem("Adapter/PrintObjectData")]
        public static void PrintObjectData()
        {
            var activeGameObject = Selection.activeGameObject;
            if (activeGameObject == null) return;
            MonoBehaviour behaviour = activeGameObject.GetComponent<MonoBehaviour>();
            if (behaviour == null) return;

            SerializationEditorDataWriter writer = new SerializationEditorDataWriter();
            SerializationEditorData serializationEditorData = default;
            writer.Write(ref serializationEditorData, behaviour);

            SerializationEditorData.ObjectData objectData = SerializationEditorData.ObjectData.Create(ref serializationEditorData, serializationEditorData.script.GetClass());

            StringBuilder builder = new StringBuilder();
            objectData.Print(builder, ref serializationEditorData, 0);
            Debug.LogError(builder);
        }

        /// <summary>
        /// 获取内容类型
        /// </summary>
        public static Type GetType(Type contentType, string[] propertyNames, int index = 0)
        {
            if (index >= propertyNames.Length) return contentType;
            string propertyName = propertyNames[index];
            contentType = GetType(contentType, propertyName);
            return GetType(contentType, propertyNames, index + 1);
        }

        /// <summary>
        /// 获取属性类型
        /// </summary>
        public static Type GetType(Type contentType, string propertyName)
        {
            if (contentType.IsArray || (contentType.IsGenericType && contentType.GetGenericTypeDefinition() == typeof(List<>)))
            {
                if (propertyName == "Array") return contentType;
                if (propertyName == "size") return typeof(int);
                if (propertyName.StartsWith("data[") && propertyName.EndsWith("]"))
                {
                    if (contentType.IsArray) return contentType.GetElementType();
                    return contentType.GetGenericArguments()[0];
                }
                throw new NotImplementedException("不存在的属性!!!");
            }
            var field = contentType.GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                throw new Exception($"{contentType}.{propertyName}");
            return field.FieldType;
        }
    }
}