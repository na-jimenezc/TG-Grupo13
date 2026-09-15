using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

public class Canasta : MonoBehaviour
{
    [Header("Bulbos dentro de la canasta (visuales, sin física activa)")]
    [SerializeField] private List<GameObject> bulbosDentro;

    [Header("Dispersión")]
    [SerializeField] private GameObject prefabBulboFisico;
    [SerializeField] private Transform areaEsparcido;
    [SerializeField] private float radioEsparcido = 0.6f;
    [SerializeField] private float anguloInclinacionMinimo = 60f;

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoEsparcir;

    private Grabbable _grabbable;
    private bool _esparcido = false;
    private bool _inclinadaLoSuficiente = false;
    private int _manosSosteniendo = 0;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;

        //Si no se asignó manualmente en el Inspector, toma el del GameObject
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    private void OnPointerEvent(PointerEvent evt)
    {
        if (evt.Type == PointerEventType.Select) _manosSosteniendo++;
        else if (evt.Type == PointerEventType.Unselect)
        {
            _manosSosteniendo = Mathf.Max(0, _manosSosteniendo - 1);

            //Dispersar los bulbos si la canasta está lo suficientemente inclinada y no hay manos sosteniéndola
            if (_manosSosteniendo == 0 && _inclinadaLoSuficiente && !_esparcido)
                Esparcir();
        }
    }

    private void Update()
    {
        if (_manosSosteniendo == 0 || _esparcido) return;

        float angulo = Vector3.Angle(transform.up, Vector3.up);
        if (angulo >= anguloInclinacionMinimo)
            _inclinadaLoSuficiente = true;
    }

    private void Esparcir()
    {
        _esparcido = true;

        foreach (var bulboVisual in bulbosDentro)
            bulboVisual.SetActive(false); // Ocultar los bulbos visuales dentro de la canasta

        foreach (var _ in bulbosDentro)
        {
            Vector2 offset = Random.insideUnitCircle * radioEsparcido;
            Vector3 posicion = areaEsparcido.position + new Vector3(offset.x, 0.3f, offset.y);

            var bulbo = Instantiate(prefabBulboFisico, posicion, Random.rotation);
            var rb = bulbo.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false; //Habilitar física para que caiga y rebote
        }

        ReproducirSonido(sonidoEsparcir);
    }

    private void ReproducirSonido(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}