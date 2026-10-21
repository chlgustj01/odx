using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

public sealed class PublicAssetMetadata : MonoBehaviour
{public string title;public string floatingDockName;[TextArea]public string detail;public string Describe(){if(!string.IsNullOrEmpty(floatingDockName)){var d=ShipyardEvidenceLabels.CurrentDatabase()?.floatingDocks.FirstOrDefault(x=>x.name==floatingDockName);if(d!=null)return title+"\n\n"+ShipyardEvidenceLabels.FloatingDetail(d);}var f=GetComponentInParent<ShipyardFacilityAdapter>();return title+"\n\n"+detail+(f!=null?ShipyardEvidenceLabels.ValueEvidence(f.Record):"");}}
