using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

public class Canasta : MonoBehaviour
{
    [Header("Bulbos dentro de la canasta")]
    [SerializeField] private int bulbosDentro = 10;
    [SerializeField] private GameObject prefabBulboFisico;
    [SerializeField] private Transform puntoInterior;
    [SerializeField] private float radioInterior = 0.15f;

    [Header("Dispersión")]
    [SerializeField] private Transform areaEsparcido;
    [SerializeField] private float radioEsparcido = 0.6f;
    [SerializeField] private float anguloInclinacionMinimo = 60f;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoEsparcir;

    private Vector3 _escalaOriginalBulbo;

    private Grabbable _grabbable;
    private bool _esparcido = false;
    private bool _inclinadaLoSuficiente = false;
    private int _manosSosteniendo = 0;
    private readonly List<GameObject> _bulbos = new List<GameObject>();

    private Quaternion _rotacionReposo;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
        _rotacionReposo = transform.rotation;   
        PoblarCanasta();
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    private void PoblarCanasta()
    {
        if (prefabBulboFisico == null || puntoInterior == null)
        {
            Debug.LogWarning("[Canasta] Falta asignar Prefab Bulbo Fisico o Punto Interior.");
            return;
        }

        for (int i = 0; i < bulbosDentro; i++)
        {
            Vector2 offset = Random.insideUnitCircle * radioInterior;
            Vector3 posicionMundo = puntoInterior.position + new Vector3(offset.x, Random.Range(0f, 0.05f), offset.y);

            GameObject bulbo = Instantiate(prefabBulboFisico, posicionMundo, Random.rotation);
            bulbo.transform.SetParent(puntoInterior, worldPositionStays: true);

            _escalaOriginalBulbo = prefabBulboFisico.transform.localScale;
            DesactivarComoPlaceholder(bulbo);
            _bulbos.Add(bulbo);
        }
    }

    private void DesactivarComoPlaceholder(GameObject bulbo)
    {
        if (bulbo.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        if (bulbo.TryGetComponent<Grabbable>(out var g)) g.enabled = false;
        if (bulbo.TryGetComponent<Bulbo>(out var b)) b.enabled = false;
        foreach (var col in bulbo.GetComponentsInChildren<Collider>()) col.enabled = false;
    }

    private void ActivarComoBulboFisico(GameObject bulbo, Vector3 posicionMundo)
    {
        bulbo.transform.SetParent(null, worldPositionStays: true);
        bulbo.transform.position = posicionMundo;
        bulbo.transform.rotation = Random.rotation;
        bulbo.transform.localScale = _escalaOriginalBulbo;   

        foreach (var col in bulbo.GetComponentsInChildren<Collider>()) col.enabled = true;
        if (bulbo.TryGetComponent<Grabbable>(out var g)) g.enabled = true;
        if (bulbo.TryGetComponent<Bulbo>(out var b)) b.enabled = true;
        if (bulbo.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select) _manosSosteniendo++;
        else if (evt.Type == PointerEventType.Unselect)
        {
            _manosSosteniendo = Mathf.Max(0, _manosSosteniendo - 1);
        }
    }

    private void Update()
    {
        if (_manosSosteniendo == 0 || _esparcido) return;

        float angulo = Quaternion.Angle(transform.rotation, _rotacionReposo);
        if (angulo >= anguloInclinacionMinimo)
        {
            _inclinadaLoSuficiente = true;
            Esparcir();   
        }
    }

    private void Esparcir()
    {
        _esparcido = true;

        foreach (var bulbo in _bulbos)
        {
            if (bulbo == null) continue;

            Vector2 offset = Random.insideUnitCircle * radioEsparcido;
            Vector3 posicion = areaEsparcido.position + new Vector3(offset.x, 0.3f, offset.y);
            ActivarComoBulboFisico(bulbo, posicion);
        }
        _bulbos.Clear();

        if (audioSource != null && sonidoEsparcir != null)
            audioSource.PlayOneShot(sonidoEsparcir);
    }
}