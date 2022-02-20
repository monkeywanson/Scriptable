using GameLib.CSharp;
using System;
using UnityEngine;

public class ILReaderTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Action action = TestMethod;
        ILReader reader = new ILReader(action.Method, action.Method.DeclaringType.Assembly);
        Debug.LogError(reader);
    }


    void TestMethod()
    {
        int i = 0;
        float f = 0.1f;
        long l = 1000;
        Debug.Log(i + f + l);
    }
}
