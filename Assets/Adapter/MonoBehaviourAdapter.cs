using System;
using UnityEngine;

namespace CLRSharp.Adapter.Unity
{
    /// <summary>
    /// MonoBehaviour Adapter.
    /// 脚本行为序列化
    /// </summary>
    public unsafe class MonoBehaviourAdapter : MonoBehaviour, ISerializationCallbackReceiver
    {
        /* static fields */
        /// <summary>
        /// 引擎启动
        /// </summary>
        public static bool VMWeekup = true;

        /* unity fields */
        /// <summary>
        /// 序列化数据
        /// </summary>
        public SerializationData serializationData;

#if UNITY_EDITOR
        /// <summary>
        /// 序列化编辑器数据
        /// </summary>
        public SerializationEditorData serializationEditorData;
        /// <summary>
        /// 重置回调
        /// </summary>
        public event Action<MonoBehaviourAdapter> ResetCall;

        /// <summary>
        /// 重置
        /// </summary>
        private void Reset()
        {
            ResetCall?.Invoke(this);
        }
        /// <summary>
        /// 更新数据
        /// </summary>
        public bool UpdateData(object previewObject)
        {
            if (!Application.isPlaying) return false;
            if (!VMWeekup) return false;
            if (target == null) return false;
            //target.GetType() : 如果是VM执行的类型，请采用VM提供的Type
            SerializationEditorData.UpdateData(previewObject, target, target.GetType());
            return true;
        }
        /// <summary>
        /// 引用数据
        /// </summary>
        public void ApplyData()
        {
            if (!Application.isPlaying) return;
            if (!VMWeekup) return;
            if (target == null) return;
            //target.GetType() : 如果是VM执行的类型，请采用VM提供的Type
            serializationEditorData.Deserialize(target, target.GetType());
        }
#endif

        /* fields */
        /// <summary>
        /// 目标
        /// </summary>
        UnityEngine.Object target;

        /* properties */

        /* unity methods */
        /// <summary>
        /// 激活
        /// </summary>
        public void Awake()
        {
            //以下代码采用DLL反射热更方式, 若采用VM解释模式，请自行实现
#if UNITY_EDITOR && !USEASSETBUNDLE //编辑器下非AssetBundle模式
            serializationEditorData.Serialize(ref serializationData);
            var type = Type.GetType(serializationData.behaviourTypeName);
            target = gameObject.AddComponent(type) as MonoBehaviour;
            serializationData.Deserialize(target);

            //var type = serializationEditorData.script.GetClass();
            //target = gameObject.AddComponent(type) as MonoBehaviour;
            //serializationEditorData.Deserialize(target);
#else
            var type = Type.GetType(serializationData.behaviourTypeName);
            target = gameObject.AddComponent(type) as MonoBehaviour;
            serializationData.Deserialize(target);
#endif
        }

        /// <summary>
        /// 序列化前
        /// </summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
#if UNITY_EDITOR
            if(Application.isPlaying) return;
            var stackTrace = new System.Diagnostics.StackTrace();
            int frameCount = stackTrace.FrameCount;
            bool callFromBuild = false;
            for (int i = 0; i < frameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                var method = frame.GetMethod();
                if (method.DeclaringType != typeof(UnityEditor.BuildPipeline)) continue;
                callFromBuild = true;
                break;
            }
            if (callFromBuild)
            {
                serializationEditorData.Serialize(ref serializationData);
            }
#endif
        }

        /// <summary>
        /// 反序列化后
        /// </summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
        }
    }
}