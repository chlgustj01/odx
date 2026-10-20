using UnityEngine;
using UnityEngine.UI;

// Public publisher CG illustrations are references, not photographs or CAD.
// This presentation component never accesses command/control components.
[DefaultExecutionOrder(650)]
public sealed class FacilityReferencePanel : MonoBehaviour
{
    public ShipyardSelectionManager selection;
    public RawImage image;
    public Texture2D[] officialIllustrations;
    private FacilityInfo previous;
    private void LateUpdate()
    {
        var f=selection.FacilityContext;
        if(f==previous)return;
        previous=f;
        if(image!=null)image.texture=f!=null&&f.facilityNumber<=officialIllustrations.Length?officialIllustrations[f.facilityNumber-1]:null;
    }
}
