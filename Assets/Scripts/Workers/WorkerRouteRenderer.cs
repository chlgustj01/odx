using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// Four cached pooled renderers/labels. No inactive worker controller or global
// scene search. Geometry updates only when a session revision/view changes.
[DefaultExecutionOrder(1060)]
public sealed class WorkerRouteRenderer : MonoBehaviour
{
 public WorkerRuntimeAgent agent;public TMP_FontAsset font;
 public int GeometryBuildCount{get;private set;}
 public int GlobalSearchesPerFrame=>0;
 public int VisibleWorkerCount{get;private set;}
 public bool[] PatternVisible{get;private set;}
 Mesh[] meshes;MeshRenderer[] renderers;TextMeshPro[] labels;Material material;int revision=-1;WorkerRouteView view;string worker;
 readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> triangles=new List<int>();
 void Start()
 {
  agent.EnsureInitialized();material=new Material(Shader.Find("Sprites/Default"));material.name="WorkerRouteDemoRuntime";
  meshes=new Mesh[agent.profiles.Length];renderers=new MeshRenderer[meshes.Length];labels=new TextMeshPro[meshes.Length];PatternVisible=new bool[meshes.Length];
  for(int i=0;i<meshes.Length;i++){
   var go=new GameObject("Route_"+agent.profiles[i].workerId);go.transform.SetParent(transform,false);meshes[i]=new Mesh{name="WorkerRoute_"+i,indexFormat=IndexFormat.UInt32};go.AddComponent<MeshFilter>().sharedMesh=meshes[i];renderers[i]=go.AddComponent<MeshRenderer>();renderers[i].sharedMaterial=material;renderers[i].shadowCastingMode=ShadowCastingMode.Off;renderers[i].receiveShadows=false;
   var text=new GameObject("Marker_"+agent.profiles[i].workerId);text.transform.SetParent(transform,false);labels[i]=text.AddComponent<TextMeshPro>();labels[i].font=font;labels[i].fontSize=1.6f;labels[i].alignment=TextAlignmentOptions.Center;labels[i].rectTransform.sizeDelta=new Vector2(2.8f,.6f);labels[i].text=agent.profiles[i].icon+" "+agent.profiles[i].workerId;labels[i].color=agent.profiles[i].routeColor;
  }
  RefreshNow();
 }
 static bool On(WorkerLineStyle style,float d)
 {if(style==WorkerLineStyle.SOLID)return true;if(style==WorkerLineStyle.DASHED)return d%1<.65f;if(style==WorkerLineStyle.DASH_DOT){float p=d%1.2f;return p<.65f||(p>.85f&&p<.95f);}return d%.5f<.12f;}
 void Quad(Vector3 a,Vector3 b)
 {var direction=b-a;direction.y=0;if(direction.sqrMagnitude<1e-8f)return;var side=Vector3.Cross(Vector3.up,direction.normalized)*.035f;int n=vertices.Count;vertices.Add(a+side);vertices.Add(a-side);vertices.Add(b+side);vertices.Add(b-side);triangles.AddRange(new[]{n,n+2,n+1,n+2,n+3,n+1});}
 void Build(int index,WorkerRouteSession session)
 {
  vertices.Clear();triangles.Clear();float phase=0;var points=session.sampledPositions;
  for(int i=1;i<points.Count;i++){
   if(points[i].breakBefore){phase=0;continue;}var a=points[i-1].position+Vector3.up*.08f;var b=points[i].position+Vector3.up*.08f;float length=Vector3.Distance(a,b);if(length<.0001f)continue;
   if(agent.profiles[index].routeLineStyle==WorkerLineStyle.SOLID)Quad(a,b);
   else for(float at=0;at<length;at+=.08f){float end=Mathf.Min(length,at+.08f);if(On(agent.profiles[index].routeLineStyle,phase+(at+end)*.5f))Quad(Vector3.Lerp(a,b,at/length),Vector3.Lerp(a,b,end/length));}
   phase+=length;
  }
  meshes[index].Clear();meshes[index].SetVertices(vertices);meshes[index].SetTriangles(triangles,0);meshes[index].RecalculateBounds();var block=new MaterialPropertyBlock();block.SetColor("_Color",agent.profiles[index].routeColor);renderers[index].SetPropertyBlock(block);GeometryBuildCount++;
 }
 public void RefreshNow(){revision=-1;}
 void LateUpdate()
 {
  if(meshes==null)return;bool exploring=agent.explorer.IsExploring;bool changed=revision!=agent.Revision||view!=agent.selection.routeView||worker!=agent.selection.activeWorkerId;
  if(changed){revision=agent.Revision;view=agent.selection.routeView;worker=agent.selection.activeWorkerId;}
  VisibleWorkerCount=0;
  for(int i=0;i<meshes.Length;i++){
   var s=agent.StateFor(agent.profiles[i].workerId);bool visible=exploring&&s.hasPosition&&(view==WorkerRouteView.ALL||s.workerId==worker);if(visible)VisibleWorkerCount++;renderers[i].enabled=visible;labels[i].gameObject.SetActive(visible);PatternVisible[i]=visible;
   if(!visible)continue;if(changed&&s.routeSession!=null)Build(i,s.routeSession);labels[i].transform.position=s.lastPosition+Vector3.up*.35f;labels[i].transform.rotation=agent.explorer.exploreCamera.transform.rotation;
  }
 }
 public bool TryHover(out WorkerRoutePoint result,out string workerId)
 {
  result=null;workerId=null;if(Mouse.current==null||agent.selection.modalOpen||Cursor.lockState==CursorLockMode.Locked)return false;var ray=agent.explorer.exploreCamera.ScreenPointToRay(Mouse.current.position.ReadValue());var plane=new Plane(Vector3.up,agent.explorer.player.position);if(!plane.Raycast(ray,out float distance))return false;var hit=ray.GetPoint(distance);float best=.35f;
  foreach(var state in agent.states){if(state.routeSession==null||agent.selection.routeView==WorkerRouteView.CURRENT&&state.workerId!=agent.selection.activeWorkerId)continue;foreach(var point in state.routeSession.sampledPositions){float d=Vector2.Distance(new Vector2(hit.x,hit.z),new Vector2(point.position.x,point.position.z));if(d>=best)continue;best=d;result=point;workerId=state.workerId;}}
  return result!=null;
 }
 void OnDestroy(){if(meshes!=null)foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
}
