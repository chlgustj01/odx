using System;
using System.Linq;
using TMPro;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Active analysis subject, not a profession-risk score or personnel identity system.
// Reuses existing Explorer modal/input guard and the SAME Explore camera for preview.
[DefaultExecutionOrder(1100)]
public sealed class WorkerSelectUI : MonoBehaviour
{
 public WorkerRuntimeAgent agent;public TMP_FontAsset font;public TextAsset uiTextData;public AccidentEvidenceRepository accidentRepository;
 public WorkerRouteRenderer routeRenderer;
 public bool IsOpen=>agent.selection.modalOpen;
 public int PreviewIndex{get;private set;}
 public int CardCount=>cards==null?0:cards.Length;
 public string LastExportPath{get;private set;}="";
 public string LastError{get;private set;}="";
 public WorkerUiCatalog Catalog{get;private set;}
 public GameObject ModalObject=>modal;
 public GameObject PreviewVisual{get;private set;}
 public TMP_Text[] WorkerTexts=>root.GetComponentsInChildren<TMP_Text>(true);
 GameObject root,hud,modal;Transform previewRoot;TMP_Text activeText,activeTitle,routeHint,legend,description,roleTitle,roleEnglish,roleCode,roleStats,mapping,exportPath,previewTitle,warning,analysisContext,hover;
 Button[] cards;Button selectButton,evidenceButton;TMP_Text[] cardLabels;Button[] langs;
 WorkerAccidentRoleAdapter roles;Material previewMaterial;TMP_Text existingCrosshair;bool crosshairSuppressed,crosshairWasEnabled,showEvidence;float nextRefresh;int shownRevision=-1;WorkerLanguage lastLanguage;
 readonly Color surface=new Color32(8,28,42,255),soft=new Color32(20,57,74,255),white=new Color32(224,237,245,255),cyan=new Color32(88,210,238,255);
 string T(string key)=>Catalog.Get(key,agent.selection.language);
 RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)=>ShipyardUIBinder.Rect(parent,name,x,y,w,h);
 Image Box(Transform parent,string name,float x,float y,float w,float h,Color color,bool ray=true){var r=Rect(parent,name,x,y,w,h);var i=r.gameObject.AddComponent<Image>();i.color=color;i.raycastTarget=ray;return i;}
 TMP_Text Text(Transform parent,string name,float x,float y,float w,float h,int size,string value)
 {var r=Rect(parent,name,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.color=white;t.alignment=TextAlignmentOptions.MidlineLeft;t.enableAutoSizing=true;t.fontSizeMax=size;t.fontSizeMin=size-3;t.text=value;t.raycastTarget=false;t.richText=false;t.overflowMode=TextOverflowModes.Overflow;return t;}
 Button Button(Transform parent,string name,float x,float y,float w,float h,string text,Action action)
 {var i=Box(parent,name,x,y,w,h,soft);var b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;b.navigation=new Navigation{mode=Navigation.Mode.None};Text(i.transform,"Label",12,0,w-24,h,19,text);b.onClick.AddListener(()=>action());return b;}
 void Start()
 {
  Catalog=JsonConvert.DeserializeObject<WorkerUiCatalog>(uiTextData.text);roles=new WorkerAccidentRoleAdapter(accidentRepository);agent.EnsureInitialized();Build();existingCrosshair=agent.explorer.hud.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Crosshair");Refresh(true);
 }
 void Build()
 {
  root=new GameObject("ActiveWorkerUI",typeof(RectTransform));root.transform.SetParent(transform,false);var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=140;var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;root.AddComponent<GraphicRaycaster>();
  hud=Rect(root.transform,"ActiveWorkerHUD",0,0,1920,1080).gameObject;
  var card=Box(hud.transform,"ActiveWorkerCard",24,150,450,246,surface,false).transform;activeTitle=Text(card,"Heading",18,8,414,32,21,"");activeText=Text(card,"WorkerContext",18,44,414,136,19,"");Button(card,"ChangeWorker",18,192,414,40,"",Open);
  var toolbar=Box(hud.transform,"WorkerRouteToolbar",1340,650,556,200,surface).transform;
  Button(toolbar,"CurrentWorkerRoutes",12,12,258,42,"",()=>SetRouteView(WorkerRouteView.CURRENT));Button(toolbar,"AllWorkerRoutes",280,12,264,42,"",()=>SetRouteView(WorkerRouteView.ALL));
  Button(toolbar,"ClearWorkerSession",12,64,258,40,"",()=>{agent.ClearCurrentSession();Refresh(true);});Button(toolbar,"ExportWorkerRoutes",280,64,264,40,"",ExportRoutes);routeHint=Text(toolbar,"RouteOverlayDisclosure",12,112,532,38,16,"");legend=Text(toolbar,"RouteLegend",12,152,532,38,17,"");
  exportPath=Text(hud.transform,"WorkerExportPath",510,820,790,54,16,"");
  var context=Box(hud.transform,"ActiveWorkerAnalysisContext",1340,862,556,160,surface,false).transform;analysisContext=Text(context,"ActiveWorkerHazardContext",14,10,528,138,18,"");hover=Text(hud.transform,"WorkerRouteHover",520,704,780,112,17,"");
  modal=Rect(root.transform,"WorkerSelectionModal",0,0,1920,1080).gameObject;
  // An open center window reveals the 3D prefab rendered by the existing Explore
  // camera. No offscreen/worker camera or second Player is created.
  Box(modal.transform,"Header",0,0,1920,132,surface);Box(modal.transform,"LeftPanel",0,132,510,948,surface);Box(modal.transform,"RightPanel",1130,132,790,948,surface);Box(modal.transform,"Footer",510,900,620,180,surface);
  Text(modal.transform,"WorkerSelectTitle",28,18,1110,42,29,"");Text(modal.transform,"WorkerDemoDisclosure",28,66,1110,40,18,"");Button(modal.transform,"CloseWorkerModal",1740,28,152,48,"",Close);
  langs=new Button[3];for(int i=0;i<3;i++){int l=i;langs[i]=Button(modal.transform,"WorkerLanguage_"+i,1190+i*174,28,158,48,((WorkerLanguage)i).ToString(),()=>SetLanguage((WorkerLanguage)l));}
  cards=new Button[4];cardLabels=new TMP_Text[4];for(int i=0;i<4;i++){int n=i;cards[i]=Button(modal.transform,"WorkerCard_"+agent.profiles[i].workerId,26,160+i*180,458,162,"",()=>Preview(n));cardLabels[i]=cards[i].GetComponentInChildren<TMP_Text>();cardLabels[i].rectTransform.anchoredPosition=new Vector2(18,-8);cardLabels[i].rectTransform.sizeDelta=new Vector2(422,146);cardLabels[i].fontSizeMax=21;cardLabels[i].fontSizeMin=18;}
  previewTitle=Text(modal.transform,"PreviewRole",540,156,560,76,26,"");previewTitle.alignment=TextAlignmentOptions.Center;
  Text(modal.transform,"Preview3DLabel",540,248,560,38,19,"3D PREVIEW / DEMO").alignment=TextAlignmentOptions.Center;
  Button(modal.transform,"PreviousWorker",540,916,268,52,"",()=>Preview((PreviewIndex+3)%4));Button(modal.transform,"NextWorker",826,916,274,52,"",()=>Preview((PreviewIndex+1)%4));
  warning=Text(modal.transform,"WorkerNotice",540,984,560,68,17,"");warning.alignment=TextAlignmentOptions.Center;
  roleTitle=Text(modal.transform,"RoleName",1160,160,710,56,28,"");roleEnglish=Text(modal.transform,"RoleEnglish",1160,220,710,36,20,"");roleCode=Text(modal.transform,"RoleCode",1160,264,710,34,17,"");
  var viewport=Box(modal.transform,"WorkerDescriptionScroll",1160,308,710,362,Color.clear).rectTransform;viewport.gameObject.AddComponent<RectMask2D>();var scroll=viewport.gameObject.AddComponent<ScrollRect>();var content=Rect(viewport,"Content",0,0,710,362);scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.scrollSensitivity=30;description=Text(content,"RoleDescription",0,0,710,362,21,"");description.alignment=TextAlignmentOptions.TopLeft;roleStats=Text(modal.transform,"RoleSession",1160,682,710,72,20,"");mapping=Text(modal.transform,"RoleAccidentMapping",1160,766,710,52,18,"");
  evidenceButton=Button(modal.transform,"RoleAccidentEvidence",1160,830,710,48,"",()=>{showEvidence=!showEvidence;Refresh(true);});
  selectButton=Button(modal.transform,"SelectActiveWorker",1160,920,710,66,"",()=>Select(PreviewIndex));
  modal.SetActive(false);hud.SetActive(false);
  var preview=new GameObject("WorkerSelectionPreview");preview.transform.SetParent(agent.explorer.exploreCamera.transform,false);preview.transform.localPosition=new Vector3(-.36f,-.78f,2.4f);preview.transform.localRotation=Quaternion.Euler(0,180,0);previewRoot=preview.transform;
  var backdrop=GameObject.CreatePrimitive(PrimitiveType.Cube);backdrop.name="PreviewBackdrop";backdrop.layer=2;backdrop.transform.SetParent(previewRoot,false);backdrop.transform.localPosition=new Vector3(0,.85f,-.22f);backdrop.transform.localScale=new Vector3(2.7f,3,.03f);Destroy(backdrop.GetComponent<Collider>());previewMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Sprites/Default"));if(previewMaterial.HasProperty("_BaseColor"))previewMaterial.SetColor("_BaseColor",surface);if(previewMaterial.HasProperty("_Color"))previewMaterial.SetColor("_Color",surface);backdrop.GetComponent<Renderer>().sharedMaterial=previewMaterial;previewRoot.gameObject.SetActive(false);
 }
 public void Open()
 {if(!agent.explorer.IsExploring||root==null)return;if(existingCrosshair!=null&&!crosshairSuppressed){crosshairWasEnabled=existingCrosshair.enabled;existingCrosshair.enabled=false;crosshairSuppressed=true;}agent.selection.modalOpen=true;agent.explorer.SetWorkerModal(true);agent.explorer.inputGuard.Acquire();agent.explorer.UnlockCursor();modal.SetActive(true);PreviewIndex=Array.FindIndex(agent.profiles,p=>p.workerId==agent.selection.activeWorkerId);Preview(Mathf.Max(0,PreviewIndex));Refresh(true);}
 public void Close()
 {agent.selection.modalOpen=false;agent.explorer.SetWorkerModal(false);if(modal!=null)modal.SetActive(false);if(previewRoot!=null)previewRoot.gameObject.SetActive(false);RestoreCrosshair();agent.explorer.CaptureCursor();Refresh(true);}
 void RestoreCrosshair(){if(crosshairSuppressed&&existingCrosshair!=null)existingCrosshair.enabled=crosshairWasEnabled;crosshairSuppressed=false;}
 public void Preview(int index)
 {PreviewIndex=Mathf.Clamp(index,0,3);showEvidence=false;if(PreviewVisual!=null)Destroy(PreviewVisual);PreviewVisual=Instantiate(agent.profiles[PreviewIndex].avatarVisualPrefab,previewRoot,false);PreviewVisual.name="PreviewVisual";PreviewVisual.transform.localScale=Vector3.one*.85f;previewRoot.gameObject.SetActive(IsOpen);Refresh(true);}
 public bool Select(int index)
 {bool ok=agent.SelectWorker(agent.profiles[Mathf.Clamp(index,0,3)].workerId);if(ok)Close();else{LastError=agent.RestoreNotice;Refresh(true);}return ok;}
 public void SetLanguage(WorkerLanguage value){agent.selection.language=value;Refresh(true);}
 public void SetRouteView(WorkerRouteView value){agent.selection.routeView=value;routeRenderer?.RefreshNow();Refresh(true);}
 public void ExportRoutes()
 {try{LastExportPath=WorkerRouteExport.Export(agent,agent.selection.routeView==WorkerRouteView.ALL);LastError="";}catch(Exception ex){LastError=ex.Message;}Refresh(true);}
 void Label(string name,string value){var t=root.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name==name)?.GetComponentInChildren<TMP_Text>();if(t!=null)t.text=value;}
 public void Refresh(bool force=false)
 {
  if(root==null)return;if(!force&&Time.unscaledTime<nextRefresh&&shownRevision==agent.Revision&&lastLanguage==agent.selection.language)return;nextRefresh=Time.unscaledTime+.25f;shownRevision=agent.Revision;lastLanguage=agent.selection.language;
  var p=agent.ActiveProfile;var s=agent.ActiveState;activeTitle.text=T("active");activeText.text=p.icon+"  "+p.Name(agent.selection.language)+"\n"+p.displayNameEn+"\n"+T("facility")+": "+s.currentFacilityId+"\n"+T("route")+": "+s.totalDistance.ToString("F1")+"m (DEMO)  / "+T("sessionRisk")+": —";
  Label("ChangeWorker",T("change"));Label("CurrentWorkerRoutes",T("current"));Label("AllWorkerRoutes",T("all"));Label("ClearWorkerSession",T("clear"));Label("ExportWorkerRoutes",T("export"));
  routeHint.text=agent.selection.routeView==WorkerRouteView.ALL?T("allDisclosure"):T("routePreferenceDisclosure");var labels=agent.profiles.Select(a=>a.icon+" "+a.roleCode+" "+a.Pattern).ToArray();legend.text=agent.selection.routeView==WorkerRouteView.ALL?labels[0]+"   "+labels[1]+"\n"+labels[2]+"   "+labels[3]:p.icon+" "+p.workerId+" "+p.Pattern;
  var context=agent.HazardContext;analysisContext.text=T("analysisContext")+"  "+context.workerId+"\n"+context.roleCode+"\n"+T("existingHazard")+": "+context.geometryState+"\n"+T("rulesDeferred");
  hover.text=routeRenderer==null||!routeRenderer.TryHover(out var point,out var worker)?"":worker+"\n"+point.timestampUtc+" / "+point.facilityId+"\n"+point.loadId+" / "+point.craneStage;
  exportPath.text=LastError!=""?LastError:LastExportPath==""?"":T("exported")+"\n"+LastExportPath;
  if(!IsOpen)return;
  root.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="WorkerSelectTitle").text=T("choose");root.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="WorkerDemoDisclosure").text=T("disclosure");
  Label("CloseWorkerModal",T("close"));Label("PreviousWorker",T("previous"));Label("NextWorker",T("next"));selectButton.GetComponentInChildren<TMP_Text>().text=T("select");evidenceButton.GetComponentInChildren<TMP_Text>().text=T("accidentEvidence");
  for(int i=0;i<4;i++){var q=agent.profiles[i];var state=agent.StateFor(q.workerId);cards[i].targetGraphic.color=i==PreviewIndex?new Color32(19,95,117,255):soft;cardLabels[i].text=q.icon+"  "+q.Name(agent.selection.language)+"\n"+q.roleCode+"\n"+T("facility")+": "+(state.hasPosition?state.currentFacilityId:"—")+"   "+state.totalDistance.ToString("F1")+"m\n"+T("sessionRisk")+": —";}
  var profile=agent.profiles[PreviewIndex];var st=agent.StateFor(profile.workerId);var matches=roles.Matches(profile);roleTitle.text=profile.Name(agent.selection.language);previewTitle.text=profile.Name(agent.selection.language);roleEnglish.text=profile.displayNameEn;roleCode.text=profile.workerId+" / "+profile.roleCode;
  description.text=showEvidence&&matches.Count>0?string.Join("\n\n",matches.Take(5).Select(r=>r.accidentId+" / "+r.title)):profile.Description(agent.selection.language);
  float descriptionHeight=Mathf.Max(362,description.GetPreferredValues(description.text,710,0).y+12);description.rectTransform.sizeDelta=new Vector2(710,descriptionHeight);description.transform.parent.GetComponent<RectTransform>().sizeDelta=new Vector2(710,descriptionHeight);
  roleStats.text=T("facility")+": "+(st.hasPosition?st.currentFacilityId:"—")+"   "+T("route")+": "+st.totalDistance.ToString("F1")+"m\n"+T("sessionRisk")+": —  / "+T("riskNotEvaluated");
  mapping.text=matches.Count==0?T("noRoleData"):"CM-H04: "+profile.accidentRoleCode+" / "+T("matchingCases")+": "+matches.Count;evidenceButton.interactable=matches.Count>0;
  warning.text=T("translationNotice")+"\n"+(agent.RestoreNotice==""?T("routePreferenceDisclosure"):agent.RestoreNotice);
 }
 void Update(){if(IsOpen&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Close();}
 void LateUpdate()
 {
  if(root==null)return;bool exploring=agent.explorer.IsExploring;if(!exploring&&IsOpen)Close();hud.SetActive(exploring&&!IsOpen);if(previewRoot!=null)previewRoot.gameObject.SetActive(exploring&&IsOpen);Refresh();
 }
 void OnDisable(){RestoreCrosshair();if(agent!=null){agent.selection.modalOpen=false;agent.explorer.SetWorkerModal(false);}if(root!=null)root.SetActive(false);}
 void OnDestroy(){if(previewRoot!=null)Destroy(previewRoot.gameObject);if(previewMaterial!=null)Destroy(previewMaterial);}
}
