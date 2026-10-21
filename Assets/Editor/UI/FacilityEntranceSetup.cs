#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Targeted migration of five existing facilities. Does not regenerate the yard.
public static class FacilityEntranceSetup
{
    const string Folder = "Assets/ShipyardExplorer/Entrances";
    static Material wall, floor, frame, leaf;
    static ShipyardUIBinder ui;
    static ShipyardExploreCameraController explorer;
    static Transform Group(Transform parent, string name, Vector3 position = default)
    { var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = position; return g.transform; }
    static Material Material(string name, Color color)
    { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = color; AssetDatabase.CreateAsset(m, Folder + "/" + name + ".mat"); return m; }
    static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, Material material, string collisionLayer = null)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = material;
        if (collisionLayer == null) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        else { g.layer = LayerMask.NameToLayer(collisionLayer); var poi = g.AddComponent<ShipyardInteractable>(); poi.kind = ShipyardPoiKind.Facility; poi.facility = p.GetComponentInParent<FacilityInfo>(); poi.subject = poi.facility.transform; poi.title = poi.facility.facilityId + " / FACILITY ENTRANCE"; }
        return g;
    }
    static Bounds FirstCube(Mesh mesh)
    { var v = mesh.vertices; var b = new Bounds(v[0], Vector3.zero); for (int i = 1; i < 24; i++) b.Encapsulate(v[i]); return b; }
    static Mesh SaveMesh(Mesh mesh, string name)
    { mesh.name = name; mesh.RecalculateBounds(); string path = Folder + "/" + name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (existing != null) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); return existing; } AssetDatabase.CreateAsset(mesh, path); return mesh; }
    static void ReplaceMesh(MeshFilter filter, Mesh replacement)
    {
        var old = filter.sharedMesh; filter.sharedMesh = replacement; EditorUtility.SetDirty(filter);
        foreach (var c in explorer.transform.Find("ExplorerCollision").GetComponentsInChildren<MeshCollider>(true))
            if (c.sharedMesh == old && c.enabled) { c.sharedMesh = replacement; EditorUtility.SetDirty(c); }
    }
    static void RemoveFirstBox(MeshFilter filter, string id)
    {
        var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
        var triangles = mesh.triangles; mesh.triangles = Enumerable.Range(0, triangles.Length / 3).Where(i => triangles[i * 3] >= 24 || triangles[i * 3 + 1] >= 24 || triangles[i * 3 + 2] >= 24).SelectMany(i => new[] { triangles[i * 3], triangles[i * 3 + 1], triangles[i * 3 + 2] }).ToArray();
        ReplaceMesh(filter, SaveMesh(mesh, id + "_HollowBay"));
    }
    static void ShiftShutter(Transform model, string id, float front, Bounds bay)
    {
        foreach (string name in new[] { "Batched Door", "Batched Seam" })
        {
            var filter = model.Find(name).GetComponent<MeshFilter>(); var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh); var v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
                if ((name == "Batched Door" && i < 24) || (name == "Batched Seam" && v[i].x > bay.min.x && v[i].x < bay.max.x && v[i].z < front && v[i].z > front - .2f && v[i].y < 3.2f)) v[i].x -= .55f;
            mesh.vertices = v; ReplaceMesh(filter, SaveMesh(mesh, id + "_" + name.Replace(' ', '_')));
        }
    }
    static void Label(Transform parent, string text, Vector3 pos, float width)
    {
        var t = Group(parent, "EntranceMarker", pos); var tmp = t.gameObject.AddComponent<TMPro.TextMeshPro>(); tmp.font = ui.font; tmp.fontSize = 2.3f; tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.text = text; tmp.color = Color.white; tmp.rectTransform.sizeDelta = new Vector2(width, .45f);
    }
    static FacilityEntrance Entrance(Transform parent, FacilityInfo facility, Vector3 pos, float width = 1.2f)
    {
        var t = Group(parent, "Entrance_01", pos); var entrance = t.gameObject.AddComponent<FacilityEntrance>(); entrance.facility = facility; entrance.clearWidth = width;
        entrance.exteriorPoint = Group(t, "ExteriorApproach", new Vector3(0, .035f, -3)); entrance.interiorPoint = Group(t, "InteriorApproach", new Vector3(0, .035f, 3));
        return entrance;
    }
    static void Door(FacilityEntrance entrance)
    {
        var t = entrance.transform;
        Box(t, "DoorFrameLeft", new Vector3(-.66f, 1.15f, 0), new Vector3(.12f, 2.3f, .2f), frame);
        Box(t, "DoorFrameRight", new Vector3(.66f, 1.15f, 0), new Vector3(.12f, 2.3f, .2f), frame);
        Box(t, "DoorFrameHeader", new Vector3(0, 2.26f, 0), new Vector3(1.44f, .12f, .2f), frame);
        entrance.doorLeaf = Group(t, "DoorVisual", new Vector3(-.6f, 0, -.12f));
        Box(entrance.doorLeaf, "PersonnelDoor", new Vector3(.6f, 1.1f, 0), new Vector3(1.2f, 2.2f, .055f), leaf);
        var blocker = Group(t, "DoorOpeningCollider", new Vector3(0, 1.1f, 0)); blocker.gameObject.layer = LayerMask.NameToLayer("ExploreSolid"); var c = blocker.gameObject.AddComponent<BoxCollider>(); c.size = new Vector3(1.2f, 2.2f, .12f); c.enabled = false; entrance.closedDoorCollider = c;
        entrance.doorLeaf.localRotation = Quaternion.Euler(0, -95, 0); Label(t, entrance.facility.facilityId + " PERSONNEL\nF OPEN / CLOSE", new Vector3(0, 2.65f, -.16f), 3);
        Box(t, "ApproachMarking", new Vector3(0, .022f, -1.2f), new Vector3(1.2f, .012f, 1.3f), frame);
    }
    static void Zone(FacilityEntrance entrance, Transform parent, Vector3 center, Vector3 size)
    { var g = Group(parent, "InteriorZone", center); g.gameObject.layer = 2; var c = g.gameObject.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = size; entrance.interiorZone = c; }
    static void Factory(int id)
    {
        var f = ui.catalog.Find(id); var model = f.transform.Find(id == 19 ? "PublicDataVisual" : "Reference-based detailed visual");
        if (!model.gameObject.activeInHierarchy) throw new Exception("Inactive source " + id);
        var root = Group(model, "WalkableEntrance"); Bounds bay; float doorX, front, height;
        if (id != 19)
        {
            var filter = model.Find("Batched Wall").GetComponent<MeshFilter>(); bay = FirstCube(filter.sharedMesh); height = bay.size.y; front = bay.min.z; doorX = bay.max.x - .8f;
            RemoveFirstBox(filter, f.facilityId); ShiftShutter(model, f.facilityId, front, bay);
        }
        else
        {
            var original = model.Find("MainHall_DB_LW_ESTIMATED_HEIGHT"); bay = new Bounds(original.localPosition, original.localScale); height = bay.size.y; front = bay.min.z; doorX = bay.max.x - .8f;
            var filter = model.Find("BatchedPublic_Concrete").GetComponent<MeshFilter>(); RemoveFirstBox(filter, f.facilityId);
            model.Find("Factory collision").GetComponent<Collider>().enabled = false;
            var steel = model.Find("BatchedPublic_Steel").GetComponent<MeshFilter>();
            var sources = model.GetComponentsInChildren<MeshRenderer>(true).Where(r => !r.name.StartsWith("BatchedPublic_") && r.name != "LargeBayDoor" && r.sharedMaterial == steel.GetComponent<MeshRenderer>().sharedMaterial).Select(r => new CombineInstance { mesh = r.GetComponent<MeshFilter>().sharedMesh, transform = model.worldToLocalMatrix * r.localToWorldMatrix }).ToArray();
            var mesh = new Mesh(); mesh.CombineMeshes(sources); ReplaceMesh(steel, SaveMesh(mesh, "F19_ShutterOpening"));
            Box(root, "RetainedFactoryShutter_2.6UU", new Vector3(-.55f, 1.3f, front - .03f), new Vector3(3.78f, 2.6f, .04f), steel.GetComponent<Renderer>().sharedMaterial, "ExploreSolid");
        }
        const float thickness = .12f;
        // Hollow one existing bay, retaining the other bays, roof and exterior dimensions.
        Box(root, "LeftSideWall", new Vector3(bay.min.x + thickness / 2, height / 2, bay.center.z), new Vector3(thickness, height, bay.size.z), wall, "ExploreSolid");
        if (id != 19) Box(root, "RightSideWall", new Vector3(bay.max.x - thickness / 2, height / 2, bay.center.z), new Vector3(thickness, height, bay.size.z), wall, "ExploreSolid");
        Box(root, "BackWall", new Vector3(bay.center.x, height / 2, bay.max.z - thickness / 2), new Vector3(bay.size.x, height, thickness), wall, "ExploreSolid");
        if (id == 19) { float z = front + 3.5f; Box(root, "EastWallFront", new Vector3(bay.max.x - thickness / 2, height / 2, (bay.min.z + z - .72f) / 2), new Vector3(thickness, height, z - .72f - bay.min.z), wall, "ExploreSolid"); Box(root, "EastWallBack", new Vector3(bay.max.x - thickness / 2, height / 2, (z + .72f + bay.max.z) / 2), new Vector3(thickness, height, bay.max.z - z - .72f), wall, "ExploreSolid"); Box(root, "EastDoorLintel", new Vector3(bay.max.x, (height + 2.38f) / 2, z), new Vector3(thickness, height - 2.38f, 1.44f), wall, "ExploreSolid"); }
        float leftEnd = doorX - .72f, rightStart = doorX + .72f;
        if (id != 19) Box(root, "FrontWallLeft", new Vector3((bay.min.x + leftEnd) / 2, height / 2, front), new Vector3(leftEnd - bay.min.x, height, thickness), wall, "ExploreSolid");
        if (id != 19) Box(root, "FrontWallRight", new Vector3((rightStart + bay.max.x) / 2, height / 2, front), new Vector3(bay.max.x - rightStart, height, thickness), wall, "ExploreSolid");
        if (id != 19) Box(root, "DoorLintel", new Vector3(doorX, (height + 2.38f) / 2, front), new Vector3(1.44f, height - 2.38f, thickness), wall, "ExploreSolid");
        if (id == 19) Box(root, "FrontWall", new Vector3(bay.center.x, height / 2, front), new Vector3(bay.size.x, height, thickness), wall, "ExploreSolid");
        // Low continuous floor: 0.04 UU rise is below the unchanged 0.25 stepOffset.
        Box(root, "InteriorFloor", new Vector3(bay.center.x, .02f, bay.center.z), new Vector3(bay.size.x - thickness, .04f, bay.size.z - thickness), floor, "ExploreGround");
        if (id != 19) Box(root, "ApproachFloor", new Vector3(doorX, .02f, front - 1.5f), new Vector3(2, .04f, 3.2f), floor, "ExploreGround");
        if (id == 19) Box(root, "EastApproachFloor", new Vector3(bay.max.x + 1.5f, .02f, front + 3.5f), new Vector3(3.2f, .04f, 2), floor, "ExploreGround");
        var entrance = Entrance(root, f, id == 19 ? new Vector3(bay.max.x, .04f, front + 3.5f) : new Vector3(doorX, .04f, front)); if (id == 19) entrance.transform.localRotation = Quaternion.Euler(0, -90, 0); Door(entrance);
        Zone(entrance, root, new Vector3(bay.center.x, 1.5f, bay.center.z), new Vector3(bay.size.x - .24f, 3, bay.size.z - .24f));
        // Keep F10's established spawn for existing POI/camera flows. Other factories lacked one.
        if (f.exploreSpawnPoint == null || id == 19) { if (f.exploreSpawnPoint == null) f.exploreSpawnPoint = Group(f.transform, "ExploreSpawnPoint"); f.exploreSpawnPoint.position = entrance.exteriorPoint.position; f.exploreSpawnPoint.rotation = entrance.transform.rotation; EditorUtility.SetDirty(f); }
        Label(root, "INTERIOR / DEMO WALKABLE BAY", new Vector3(bay.center.x, 2.5f, front + 4), bay.size.x - .5f);
    }
    static void Dock()
    {
        var f = ui.catalog.Find(1); var model = f.transform.Find("PublicDataVisual"); var root = Group(model, "DockWalkingAccess");
        // Read the existing cutaway dock walkable floor. Access uses steps, not a portal.
        float floorWorld = model.Find("Dock basin collision").GetComponent<Collider>().bounds.max.y;
        float floorLocal = model.InverseTransformPoint(new Vector3(0, floorWorld, 0)).y;
        float front = -model.Find("DryDockBasin_DB").localScale.z * .5f;
        Box(root, "AccessStepLower", new Vector3(0, floorLocal - .1125f, front - 1.7f), new Vector3(2.4f, .075f, 1.4f), floor, "ExploreGround");
        Box(root, "AccessStepUpper", new Vector3(0, floorLocal - .075f, front - .6f), new Vector3(2.4f, .15f, 1.2f), floor, "ExploreGround");
        Box(root, "DockAccessLanding", new Vector3(0, floorLocal - .04f, front + 1), new Vector3(2.4f, .08f, 2), floor, "ExploreGround");
        var entrance = Entrance(root, f, new Vector3(0, floorLocal, front), 2.4f); entrance.dockAccess = true;
        // Exterior marker must be at the exterior ground datum, not the raised cutaway floor.
        entrance.exteriorPoint.position = new Vector3(entrance.exteriorPoint.position.x, .035f, entrance.exteriorPoint.position.z);
        Zone(entrance, root, new Vector3(0, floorLocal + 1.5f, 0), new Vector3(12.8f, 3, 52.7f));
        foreach (int side in new[] { -1, 1 }) { Box(root, "AccessMarkerPost", new Vector3(side * 1.35f, floorLocal + .7f, front - .2f), new Vector3(.12f, 1.4f, .12f), frame); }
        Label(entrance.transform, "F01 DOCK ACCESS\nWALK / TWO-WAY", new Vector3(0, 2.4f, 0), 4);
    }
    public static void RegisterVisibility() { var u = UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>(); var all = UnityEngine.Object.FindObjectsByType<FacilityEntrance>(FindObjectsInactive.Include, FindObjectsSortMode.None).SelectMany(e => e.transform.parent.GetComponentsInChildren<Renderer>(true)); u.overview.siteProxyRenderers = u.overview.siteProxyRenderers.Where(r => r != null).Union(all).ToArray(); EditorUtility.SetDirty(u.overview); }
    public static object Install()
    {
        if (Application.isPlaying) throw new Exception("Edit Mode required");
        if (UnityEngine.Object.FindObjectsByType<FacilityEntrance>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length != 0) throw new Exception("Entrances already installed");
        ui = UnityEngine.Object.FindFirstObjectByType<ShipyardUIBinder>(); explorer = UnityEngine.Object.FindFirstObjectByType<ShipyardExploreCameraController>();
        Directory.CreateDirectory(Application.dataPath + "/ShipyardExplorer/Entrances"); AssetDatabase.Refresh();
        wall = Material("EntranceWall", new Color(.65f, .74f, .75f)); floor = Material("EntranceFloor", new Color(.45f, .48f, .46f)); frame = Material("EntranceMarking", new Color(.04f, .78f, .62f)); leaf = Material("PersonnelDoor", new Color(.14f, .3f, .34f));
        foreach (int id in new[] { 10, 11, 12, 19 }) Factory(id); Dock(); RegisterVisibility();
        AssetDatabase.SaveAssets(); var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene); bool saved = EditorSceneManager.SaveScene(scene);
        return new { saved, entrances = 5, portalCount = 0 };
    }
}
#endif
