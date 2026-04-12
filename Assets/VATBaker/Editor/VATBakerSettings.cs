using UnityEngine;

namespace VATBaker
{
    [CreateAssetMenu(menuName = "VAT Baker/Settings", fileName = "VATBakerSettings")]
    public class VATBakerSettings : ScriptableObject
    {
        public Mesh[] sourceMeshes = new Mesh[0];
        public string outputPath = "Assets/VATBaker/Generated";
        public bool flattenSubmeshes = true;
        public bool useFullFloatPrecision = false;
    }
}
