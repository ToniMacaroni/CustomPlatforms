using UnityEngine;
using UnityEngine.Serialization;

using Zenject;


namespace CustomFloorPlugin
{
    public class SpectrogramMaterial : MonoBehaviour, INotifyPlatformEnabled
    {
        [Header("The Array property (uniform float arrayName[64])")]
        [FormerlySerializedAs("PropertyName")] public string? propertyName;
        [Header("The global intensity (float valueName)")]
        [FormerlySerializedAs("AveragePropertyName")] public string? averagePropertyName;

        private BasicSpectrogramData? _basicSpectrogramData;

        private Renderer Renderer => _renderer ??= GetComponent<Renderer>();
        private Renderer? _renderer;

        [Inject]
        public void Construct([InjectOptional] BasicSpectrogramData basicSpectrogramData)
        {
            _basicSpectrogramData = basicSpectrogramData;
        }

        public void PlatformEnabled(DiContainer container)
        {
            container.Inject(this);
        }

        private void Update()
        {
            if (_basicSpectrogramData != null && Renderer != null)
            {
                float average = 0.0f;
                for (int i = 0; i < 64; i++)
                {
                    average += _basicSpectrogramData.ProcessedSamples[i];
                }
                average /= 64.0f;

                foreach (Material mat in Renderer.materials)
                {
                    mat.SetFloatArray(propertyName, _basicSpectrogramData.ProcessedSamples);
                    mat.SetFloat(averagePropertyName, average);
                }
            }
        }
    }
}