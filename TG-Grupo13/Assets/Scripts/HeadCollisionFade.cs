using UnityEngine;

[RequireComponent(typeof(Camera))]
public class HeadCollisionFade : MonoBehaviour
{
    [Header("Deteccion")]
    public LayerMask capasSolidas = ~0;
    public float radioCabeza = 0.15f;

    [Header("Fundido")]
    public float velocidadFundido = 8f;
    [Range(0f, 1f)] public float opacidadMaxima = 1f;

    Renderer _quad;
    Material _material;
    float _opacidadActual;
    readonly Collider[] _resultados = new Collider[4];

    void Awake()
    {
        CrearQuad();
    }

    void CrearQuad()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FundidoCabeza";
        go.transform.SetParent(transform, false);

        var cam = GetComponent<Camera>();
        float distancia = cam.nearClipPlane + 0.001f;
        go.transform.localPosition = new Vector3(0f, 0f, distancia);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(distancia * 8f, distancia * 8f, 1f);

        Destroy(go.GetComponent<Collider>());

        _quad = go.GetComponent<Renderer>();
        _quad.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _quad.receiveShadows = false;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        _material = new Material(shader);
        _material.SetFloat("_Surface", 1f);
        _material.SetFloat("_Blend", 0f);
        _material.renderQueue = 4000;
        _material.color = new Color(0f, 0f, 0f, 0f);
        _quad.material = _material;

        _quad.enabled = false;
    }

    void LateUpdate()
    {
        bool dentroDeAlgo = Physics.OverlapSphereNonAlloc(
            transform.position,
            radioCabeza,
            _resultados,
            capasSolidas,
            QueryTriggerInteraction.Ignore) > 0;

        float objetivo = dentroDeAlgo ? opacidadMaxima : 0f;

        _opacidadActual = Mathf.MoveTowards(
            _opacidadActual, objetivo, velocidadFundido * Time.deltaTime);

        bool visible = _opacidadActual > 0.001f;
        if (_quad.enabled != visible) _quad.enabled = visible;

        if (visible)
            _material.color = new Color(0f, 0f, 0f, _opacidadActual);
    }

    void OnDestroy()
    {
        if (_material != null) Destroy(_material);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radioCabeza);
    }
}
