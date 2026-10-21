using System.Collections.Generic;
using UnityEngine;

// Geometry uses Scene UU directly. Personnel clearance is operational 1 UU = 1m.
// No GIS distance conversion, worker ownership, or crane control is performed here.
[DefaultExecutionOrder(925)]
public sealed class FacilityEntrance : MonoBehaviour
{
    public FacilityInfo facility;
    public BoxCollider interiorZone;
    public Transform exteriorPoint, interiorPoint, doorLeaf;
    public BoxCollider closedDoorCollider;
    public bool doorOpen = true;
    public bool dockAccess;
    public float clearWidth = 1.2f, clearHeight = 2.2f;
    public bool IsInside { get; private set; }
    public int EntryCount { get; private set; }
    public int ExitCount { get; private set; }
    public int InteractionCount { get; private set; }
    bool observing;
    static readonly List<FacilityEntrance> active = new List<FacilityEntrance>();
    void OnEnable() { active.Add(this); ApplyDoor(); }
    void OnDisable() { active.Remove(this); observing = false; IsInside = false; }
    void ApplyDoor()
    {
        if (doorLeaf != null) doorLeaf.localRotation = Quaternion.Euler(0, doorOpen ? -95 : 0, 0);
        if (closedDoorCollider != null) closedDoorCollider.enabled = !doorOpen;
    }
    public bool Contains(Vector3 worldPoint)
    {
        if (interiorZone == null) return false;
        var local = interiorZone.transform.InverseTransformPoint(worldPoint) - interiorZone.center;
        var half = interiorZone.size * .5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }
    public void Observe(Vector3 worldPoint)
    {
        bool inside = Contains(worldPoint);
        if (observing && inside != IsInside) { if (inside) EntryCount++; else ExitCount++; }
        IsInside = inside; observing = true;
    }
    void LateUpdate()
    {
        var e = ShipyardExploreCameraController.Instance;
        if (e == null || !e.IsExploring || e.Facility != facility) { observing = false; IsInside = false; return; }
        Observe(e.player.position);
    }
    public bool TryToggle(ShipyardExploreCameraController explorer)
    {
        if (dockAccess || explorer == null || !explorer.IsExploring || explorer.UiFocused || explorer.Facility != facility) return false;
        var p = transform.InverseTransformPoint(explorer.player.position);
        // Do not shut a leaf through the player while straddling the threshold.
        if (doorOpen && Mathf.Abs(p.z) < explorer.character.radius + .2f && Mathf.Abs(p.x) < clearWidth * .5f + explorer.character.radius) return false;
        doorOpen = !doorOpen; InteractionCount++; ApplyDoor(); return true;
    }
    public string Prompt => dockAccess ? "WALK / DOCK ACCESS" : "[F] " + (doorOpen ? "CLOSE" : "OPEN") + " DOOR / WALK ENTER OR EXIT";
    public static FacilityEntrance ForFacility(FacilityInfo facility) => active.Find(x => x != null && x.facility == facility);
    public static FacilityEntrance Resolve(ShipyardExploreCameraController explorer)
    {
        if (!explorer.IsExploring || explorer.Blending || explorer.UiFocused) return null;
        var ray = explorer.exploreCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
        // Match POI camera reach while also bounding the actual actor distance.
        // Third-person camera offset must not make F unusable at an open door.
        FacilityEntrance nearest = null; float best = 8f;
        foreach (var entrance in active)
        {
            if (entrance == null || entrance.facility != explorer.Facility) continue;
            var actor = entrance.transform.InverseTransformPoint(explorer.player.position);
            if (new Vector2(actor.x, actor.z).magnitude > 4.5f) continue;
            var plane = new Plane(entrance.transform.forward, entrance.transform.position);
            if (!plane.Raycast(ray, out float distance) || distance < 0 || distance > best) continue;
            var p = entrance.transform.InverseTransformPoint(ray.GetPoint(distance));
            if (Mathf.Abs(p.x) > entrance.clearWidth * .5f + .15f || p.y < 0 || p.y > entrance.clearHeight + .15f) continue;
            if (explorer.Target != null && explorer.TargetDistance + .05f < distance) continue;
            bool blocked = false;
            foreach (var hit in Physics.RaycastAll(ray, Mathf.Max(0, distance - .06f), explorer.solidMask, QueryTriggerInteraction.Ignore))
                if (hit.collider != entrance.closedDoorCollider) { blocked = true; break; }
            if (!blocked) { nearest = entrance; best = distance; }
        }
        return nearest;
    }
}
