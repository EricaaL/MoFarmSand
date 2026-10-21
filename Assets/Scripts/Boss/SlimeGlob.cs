using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A lobbed glob of slime. Lands on the target point and leaves a SlimePuddle.</summary>
public class SlimeGlob : MonoBehaviour
{
    private Vector3 from;
    private Vector3 to;
    private float flightSeconds;
    private float arcHeight;
    private float t;
    private SlimePuddle.Settings puddle;

    public static void Spawn(Vector3 from, Vector3 to, SlimePuddle.Settings puddle, float flightSeconds = 0.8f, float arcHeight = 2.5f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SlimeGlob";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 0.5f;

        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", puddle.color);
        m.SetFloat("_Smoothness", 0.9f);
        go.GetComponent<Renderer>().sharedMaterial = m;

        SlimeGlob glob = go.AddComponent<SlimeGlob>();
        glob.from = from;
        glob.to = new Vector3(to.x, 0f, to.z);
        glob.flightSeconds = Mathf.Max(0.1f, flightSeconds);
        glob.arcHeight = arcHeight;
        glob.puddle = puddle;
    }

    private void Update()
    {
        t += Time.deltaTime / flightSeconds;
        float k = Mathf.Clamp01(t);
        Vector3 pos = Vector3.Lerp(from, to, k);
        pos.y += Mathf.Sin(k * Mathf.PI) * arcHeight;
        transform.position = pos;

        if (t < 1f) return;
        SlimePuddle.Spawn(to, puddle);
        Destroy(gameObject);
    }
}
