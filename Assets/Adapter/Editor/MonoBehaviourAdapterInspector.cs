using CLRSharp.Adapter.Unity;
using GameLib.CSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace GameLib.Editors.Unity
{
    /// <summary>
    /// 行为适配器监视器
    /// </summary>
    [CustomEditor(typeof(MonoBehaviourAdapter), true), CanEditMultipleObjects]
    public class MonoBehaviourAdapterInspector : Editor
    {
        /* static fields */
        /// <summary>
        /// 监视器标题
        /// </summary>
        private static Dictionary<Type, string> s_InspectorTitles;
        /// <summary>
        /// 当前编辑节点
        /// </summary>
        private static Dictionary<MultipleObjects, MultipleObjectsEditData> multipleObjectsEditDatas = new Dictionary<MultipleObjects, MultipleObjectsEditData>();
        /// <summary>
        /// 查询节点
        /// </summary>
        private static Dictionary<UnityEngine.Object, GameObject> searchObjects = new Dictionary<UnityEngine.Object, GameObject>();
        /// <summary>
        /// 包装对象
        /// </summary>
        private static Dictionary<UnityEngine.Object, UnityEngine.Object> wrapComponents = new Dictionary<UnityEngine.Object, UnityEngine.Object>();
        /// <summary>
        /// 目标对象
        /// </summary>
        private static GameObject targetObject;
        /// <summary>
        /// 序列化写入器
        /// </summary>
        private static SerializationEditorDataWriter writer = new SerializationEditorDataWriter();
        /// <summary>
        /// 需要删除的对象
        /// </summary>
        private static List<UnityEngine.Object> needDestoryObjects = new List<UnityEngine.Object>();

        /* static methods */
        /// <summary>
        /// 处理头
        /// </summary>
        static bool HeaderItem(Rect rectangle, UnityEngine.Object[] targets)
        {
            if (!(targets[0] is MonoBehaviourAdapter adapter)) return false;

            if (multipleObjectsEditDatas.TryGetValue(targets, out var value))
            {
                var editItem = value.GetEditItem(targets[0]);
                if (editItem != null)
                {
                    s_InspectorTitles[typeof(MonoBehaviourAdapter)] = editItem.displayName;
                    return false;
                }
            }
            
            MonoScript script = adapter.serializationEditorData.script;
            if (script != null)
            {
                Type type = script.GetClass();
                if (type != null)
                {
                    s_InspectorTitles[typeof(MonoBehaviourAdapter)] = ObjectNames.NicifyVariableName($"{type.Name} (Proxy)");
                    return false;
                }
            }
            s_InspectorTitles[typeof(MonoBehaviourAdapter)] = ObjectNames.NicifyVariableName($"{ typeof(MonoBehaviourAdapter).Name} (Script)");
            return false;
        }

        /// <summary>
        /// 初始化头
        /// </summary>
        /// <returns></returns>
        static void InitHeader()
        {
            if (s_InspectorTitles != null) return;
            var flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var fieldInfo = typeof(EditorGUIUtility).GetField("s_EditorHeaderItemsMethods", flags);
            var value = fieldInfo.GetValue(null);
            if (value == null) return;
            var delegateType = value.GetType().GetGenericArguments()[0];
            Func<Rect, UnityEngine.Object[], bool> func = HeaderItem;
            ((IList)value).Add(Delegate.CreateDelegate(delegateType, func.Method));

            Type inspectorTitleType = typeof(ObjectNames).GetNestedType("InspectorTitles", flags);
            fieldInfo = inspectorTitleType.GetField(nameof(s_InspectorTitles), flags);
            s_InspectorTitles = (Dictionary<Type, string>)fieldInfo.GetValue(null);
            EditorApplication.update -= InitHeader;
        }

        /// <summary>
        /// 当添加组件
        /// </summary>
        static void OnAddComponentEvent(Component component)
        {
            if (StackHelper.IsCallFrom(typeof(MonoBehaviourAdapterInspector))) return;
            bool fromAddCompoentWindow = StackHelper.IsCallFrom(typeof(Editor).Assembly.GetType("UnityEditor.AddComponent.AddComponentWindow"));
            bool fromEidtorDragging = StackHelper.IsCallFrom(typeof(Editor).Assembly.GetType("UnityEditor.EditorDragging"));
            if ( !fromAddCompoentWindow && !fromEidtorDragging) return;
            if (component is MonoBehaviourAdapter)
            {
                DestoryObject(component);
            }
            else if (component.GetType().IsScriptType())
            {
                var adapter = component.gameObject.AddComponent<MonoBehaviourAdapter>();
                EditItem.Serialize(adapter, component);
                DestoryObject(component);
            }
        }

        /// <summary>
        /// 删除对象
        /// </summary>
        static void DestoryObjects()
        {
            foreach (var temp in needDestoryObjects)
                DestroyImmediate(temp);
            needDestoryObjects.Clear();
        }

        /// <summary>
        /// 销毁对象
        /// </summary>
        static void DestoryObject(UnityEngine.Object value)
        {
            if (needDestoryObjects.Count == 0) EditorApplication.delayCall += DestoryObjects;
            needDestoryObjects.Add(value);
            value.hideFlags = HideFlags.HideInInspector;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        [InitializeOnLoadMethod]
        static void InitOnLoad()
        {
            EditorApplication.update += InitHeader;
            ObjectFactory.componentWasAdded += OnAddComponentEvent;
        }

        /* fields */
        /// <summary>
        /// 节点索引
        /// </summary>
        EditItem editItem;
        /// <summary>
        /// 多对象编辑器数据
        /// </summary>
        MultipleObjectsEditData multipleObjectsEditData;
        

        /* methods */
        /// <summary>
        /// 初始化
        /// </summary>
        private void OnEnable()
        {
            if (!multipleObjectsEditDatas.TryGetValue(targets, out multipleObjectsEditData))
            {
                multipleObjectsEditData = new MultipleObjectsEditData();
                multipleObjectsEditData.Setup(targets);
                multipleObjectsEditDatas.Add(targets, multipleObjectsEditData);
            }
            editItem = multipleObjectsEditData.GetEditItem(target);
            editItem.OnEnable();
            multipleObjectsEditData.OnEnable();
        }

        /// <summary>
        /// 销毁
        /// </summary>
        private void OnDisable()
        {
            if (multipleObjectsEditData.OnDisable())
                multipleObjectsEditDatas.Remove(multipleObjectsEditData.MultipleObjects);

            editItem.OnDisable();

            if (multipleObjectsEditDatas.Count <= 0)
            {
                DestroyImmediate(targetObject);
                targetObject = null;
                searchObjects.Clear();
                wrapComponents.Clear();
            }
        }

        /// <summary>
        /// 监视器
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (editItem.componentEditor == null)
            {
                EditorGUILayout.HelpBox($"Script {editItem.script} Missing", MessageType.Warning);
                return;
            }

            if (!editItem.editable)
            {
                EditorGUILayout.HelpBox("Can't Support MultipleObjectsEdit", MessageType.Warning);
                return;
            }
            editItem.UpdateData();  
            EditorGUI.BeginChangeCheck();
            editItem.componentEditor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
                editItem.Serialize();
        }

        /// <summary>
        /// 多对象编辑数据
        /// </summary>
        private struct MultipleObjects : IEquatable<MultipleObjects>
        {
            /// <summary>
            /// 目标对象
            /// </summary>
            public UnityEngine.Object[] targets;

            /// <summary>
            /// 哈希值
            /// </summary>
            int hashCode;

            /// <summary>
            /// 是否相同
            /// </summary>
            bool IEquatable<MultipleObjects>.Equals(MultipleObjects other)
            {
                if (targets.Length != other.targets.Length) return false;
                for (int i = 0; i < targets.Length; i++)
                {
                    searchObjects.TryGetValue(targets[i], out var gameObject1);
                    searchObjects.TryGetValue(other.targets[i], out var gameObject2);
                    if (gameObject1 != gameObject2) return false;
                }
                return true;
            }

            /// <summary>
            /// 获取哈希值
            /// </summary>
            public override int GetHashCode()
            {
                return hashCode;
            }

            /// <summary>
            /// 隐式转换
            /// </summary>
            /// <param name="targets"></param>
            public static implicit operator MultipleObjects(UnityEngine.Object[] targets)
            {
                MultipleObjects multipleObjects = new MultipleObjects();
                multipleObjects.targets = targets;

                int hashCode = 17;
                foreach (var target in targets)
                {
                    if (!searchObjects.TryGetValue(target, out var gameObject))
                    {
                        if (!target) throw new Exception("一个没有走OnEnable的对象被销毁了！");
                        gameObject = (target as MonoBehaviourAdapter).gameObject;
                        searchObjects.Add(target, gameObject);
                    }
                    hashCode = 65537 * hashCode + gameObject.GetInstanceID();
                }
                multipleObjects.hashCode = hashCode;
                return multipleObjects;
            }
        }

        /// <summary>
        /// 多对象编辑数据
        /// </summary>
        private class MultipleObjectsEditData
        {
            /// <summary>
            /// 当前编辑节点
            /// </summary>
            List<EditItem> currentEditItems = new List<EditItem>();

            /// <summary>
            /// 所有适配器
            /// </summary>
            Dictionary<GameObject, List<MonoBehaviourAdapter>> totalAdapters = new Dictionary<GameObject, List<MonoBehaviourAdapter>>();

            /// <summary>
            /// 引用数量
            /// </summary>
            int referenceCount;

            /// <summary>
            /// 多编辑对象
            /// </summary>
            public MultipleObjects MultipleObjects { get; private set; }

            /// <summary>
            /// 初始化
            /// </summary>
            public void Setup(MultipleObjects multipleObjects)
            {
                MultipleObjects = multipleObjects;
                List<MonoBehaviourAdapter> targetAdapters = null;
                foreach (var targetObject in multipleObjects.targets)
                {
                    var adapter = targetObject as MonoBehaviourAdapter;
                    var gameObject = adapter.gameObject;
                    if (totalAdapters.TryGetValue(gameObject, out var value)) continue;
                    value = new List<MonoBehaviourAdapter>();
                    gameObject.GetComponents(value);
                    totalAdapters.Add(gameObject, value);
                    if (targetAdapters == null || targetAdapters.Count < value.Count) targetAdapters = value;
                }

                List<List<MonoBehaviourAdapter>> allAdapters = new List<List<MonoBehaviourAdapter>>();
                foreach (var objectApdaters in totalAdapters.Values)
                    allAdapters.Add(new List<MonoBehaviourAdapter>(objectApdaters));

                targetAdapters = new List<MonoBehaviourAdapter>(targetAdapters);
                for (int i = 0; i < targetAdapters.Count; i++)
                {
                    EditItem editItem = new EditItem();
                    var script = targetAdapters[i].serializationEditorData.script;
                    foreach (var objectApdaters in allAdapters)
                    {
                        for (int j = 0; j < objectApdaters.Count; j++)
                        {
                            var adapter = objectApdaters[j];

                            if (adapter.serializationEditorData.script != script) continue;
                            objectApdaters.RemoveAt(j);
                            editItem.adapters.Add(adapter);
                            break;
                        }
                    }
                    if (script != null)
                    {
                        editItem.SetComponentType(script);
                    }
                    else
                    {
                        editItem.displayName = ObjectNames.NicifyVariableName($"{ typeof(MonoBehaviourAdapter).Name} (Script)");
                    }
                    editItem.editable = editItem.adapters.Count == multipleObjects.targets.Length;
                    currentEditItems.Add(editItem);
                }
                currentEditItems.Sort((i1, i2) => i2.adapters.Count.CompareTo(i1.adapters.Count));
            }

            /// <summary>
            /// 获取编辑节点
            /// </summary>
            public EditItem GetEditItem(UnityEngine.Object target)
            {
                var adapter = (target as MonoBehaviourAdapter);
                totalAdapters.TryGetValue(adapter.gameObject, out var values);
                int index = values.IndexOf(adapter);
                return currentEditItems[index];
            }

            /// <summary>
            /// 激活
            /// </summary>
            public void OnEnable()
            {
                referenceCount++;
            }

            /// <summary>
            /// 关闭
            /// </summary>
            public bool OnDisable()
            {
                referenceCount--;
                return referenceCount == 0;
            }
        }

        /// <summary>
        /// 编辑节点
        /// </summary>
        class EditItem
        {
            /// <summary>
            /// 适配器列表
            /// </summary>
            public List<MonoBehaviourAdapter> adapters = new List<MonoBehaviourAdapter>();
            /// <summary>
            /// 目标组件列表
            /// </summary>
            public UnityEngine.Object[] targetComponents;
            /// <summary>
            /// 目标对象
            /// </summary>
            public Editor componentEditor;
            /// <summary>
            /// 展现名称
            /// </summary>
            public string displayName;
            /// <summary>
            /// 可编辑的
            /// </summary>
            public bool editable;
            /// <summary>
            /// 行为脚本
            /// </summary>
            public MonoScript script;
            /// <summary>
            /// 组件类型
            /// </summary>
            Type componentType;
            /// <summary>
            /// 引用数量
            /// </summary>
            int referenceCount;


            /// <summary>
            /// 序列化存储
            /// </summary>
            public static void Serialize(MonoBehaviourAdapter adapter, UnityEngine.Object targetComponent)
            {
                writer.Clear();
                writer.Write(ref adapter.serializationEditorData, targetComponent);
                EditorUtility.SetDirty(adapter);
            }

            /// <summary>
            /// 设置组件类型
            /// </summary>
            public void SetComponentType(MonoScript script, bool serialized = true)
            {
                this.script = script;
                Type componentType = script.GetClass();
                if (this.componentType != null) throw new Exception("重复设置组件类型");
                if (componentType != null && typeof(MonoBehaviourAdapter).BaseType.IsAssignableFrom(componentType))
                {
                    this.componentType = componentType;
                    displayName = ObjectNames.NicifyVariableName($"{componentType.Name} (Proxy)");

                    targetComponents = new UnityEngine.Object[adapters.Count];
                    for (int i = 0; i < adapters.Count; i++)
                        targetComponents[i] = CreatePreviewComponent(adapters[i], serialized);
                    CreateCachedEditor(targetComponents, null, ref componentEditor);
                    if (!serialized) Serialize();
                }
            }

            /// <summary>
            /// 序列化存储所有
            /// </summary>
            public void Serialize()
            {
                if (targetComponents == null) return;
                for (int i = 0; i < targetComponents.Length; i++)
                {
                    Serialize(adapters[i], targetComponents[i]);
                    adapters[i].ApplyData();
                }
            }

            /// <summary>
            /// 更新数据
            /// </summary>
            public void UpdateData()
            {
                for (int i = 0; i < targetComponents.Length; i++)
                {
                    if(adapters[i].UpdateData(targetComponents[i]))
                        Serialize(adapters[i], targetComponents[i]);
                }
            }

            /// <summary>
            /// 初始化
            /// </summary>
            public void OnEnable()
            {
                if (referenceCount == 0)
                {
                    foreach (var adapter in adapters)
                    {
                        adapter.ResetCall += ResetCall;
                    }
                }
                referenceCount++;
            }

            /// <summary>
            /// 销毁
            /// </summary>
            public void OnDisable()
            {
                referenceCount--;
                if (referenceCount != 0) return;

                foreach (var adapter in adapters)
                {
                    adapter.ResetCall -= ResetCall;
                }

                if (componentEditor != null)
                {
                    DestroyImmediate(componentEditor);
                }
            }

            /// <summary>
            /// 重置回调
            /// </summary>
            public void ResetCall(MonoBehaviourAdapter adapter)
            {
                int index = adapters.IndexOf(adapter);
                if (editable)
                {
                    Unsupported.SmartReset(targetComponents[index]);
                    Serialize(adapter, targetComponents[index]);
                }
                else
                {
                    Serialize(adapter, targetComponents[index]);
                }
            }

            /// <summary>
            /// 创建预览对象
            /// </summary>
            private UnityEngine.Object CreatePreviewComponent(MonoBehaviourAdapter adapter, bool serialized)
            {
                if (!wrapComponents.TryGetValue(adapter, out var targetComponent))
                {
                    if (targetObject == null)
                    {
                        targetObject = new GameObject("[Adapter]");
                        targetObject.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                        targetObject.SetActive(false);
                        if (!PrefabUtility.IsPartOfPrefabAsset(adapter)) targetObject.transform.parent = adapter.transform;
                        targetObject.transform.localPosition = Vector3.zero;
                    }

                    targetComponent = targetObject.AddComponent(componentType);
                    if (serialized) SerializationEditorDataReader.Read(ref adapter.serializationEditorData, targetComponent);
                    wrapComponents.Add(adapter, targetComponent);
                }
                return targetComponent;
            }
        }
    }
}