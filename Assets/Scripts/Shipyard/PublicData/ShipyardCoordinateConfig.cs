using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed class ShipyardCoordinateConfig : MonoBehaviour
{
    [Min(.01f)] public float metersPerUnityUnit=10;public Vector3 origin;public float northRotation;
    public string locationBasis="CURRENT_LAYOUT_RECONSTRUCTION";
    public float Units(float metres)=>metres/metersPerUnityUnit;
    // Background dimension scale only. Existing LIVE rig control units remain unchanged DEMO.
}
