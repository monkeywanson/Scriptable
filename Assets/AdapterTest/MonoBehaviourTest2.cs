using System.Collections.Generic;
using UnityEngine;

public class MonoBehaviourTest2 : MonoBehaviour
{
    public AnimationCurve curve;
    public Gradient gradient;
    public Class clazz;
    public Struct @struct;
    public Object unityObject;
    public TEnum value2;
    public List<Struct> structs;
    public List<Class> clazzes;

    public byte ibyte = 1;
    public sbyte isbyte = 1;
    public bool iboolean = false;

    /// <summary>
    /// 类
    /// </summary>
    [System.Serializable]
    public class Class
    {
        public int x;
        public int y;
        public Object target;
        private int z;
    }

    /// <summary>
    /// 结构体
    /// </summary>
    [System.Serializable]
    public struct Struct
    {
        public int x;
        public int y;
        private int z;

        public List<int> ints;
    }

    public enum TEnum
    {
        Value1 = 1,
        Value2
    }
}
