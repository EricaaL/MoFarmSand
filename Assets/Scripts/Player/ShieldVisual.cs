using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shows a translucent bark bubble while the player has a shield. Listens to PlayerHealth.OnChanged.</summary>
[RequireComponent(typeof(PlayerHealth))]
public class ShieldVisual : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0.55f, 0.38f, 0.2f, 0.4f);
    [SerializeField] private float diameter = 2.2f;

    private PlayerHealth health;
    private GameObject bubble;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = "ShieldBubble";
        Destroy(bubble.GetComponent<Collider>());
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        bubble.transform.localScale = Vector3.one * diameter;

        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetFloat("_Surface", 1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetColor("_BaseColor", color);
        bubble.GetComponent<Renderer>().sharedMaterial = m;
        bubble.SetActive(false);
    }

    private void OnEnable()
    {
        health.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable() { health.OnChanged -= Refresh; }

    private void Refresh() { bubble.SetActive(health.HasShield); }
}
