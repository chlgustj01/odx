using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed class ShipyardDatabaseLoader : MonoBehaviour
{
    public ShipyardDatabase Database{get;private set;}public int LoadCount{get;private set;}public string Error{get;private set;}
    public bool LoadOnce(){if(Database!=null)return true;if(LoadCount>0)return false;LoadCount++;try{Database=ShipyardDatabase.Read(Path.Combine(Application.streamingAssetsPath,"ShipyardDB"));return true;}catch(Exception e){Error=e.Message;return false;}}
    void Awake(){LoadOnce();}
}
