using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;

/*Contenedor interactivo para los bulbos. Inicia con objetos estáticos 
y los convierte en objetos físicos al inclinar la canasta para vaciarla*/

public class Canasta : MonoBehaviour
{
    [Header("Bulbos dentro de la canasta")]
    [SerializeField] private int bulbosDentro = 10;
    [SerializeField] private GameObject prefabBulboFisico; //Prefab con Rigidbody y Grabbable
    [SerializeField] private Transform puntoInterior; //Centro visual interno
    [SerializeField] private float radioInterior = 0.15f; //Área de distribución inicial

    [Header("Dispersión")]
    [SerializeField] private Transform areaEsparcido; //Hacia dónde caen al vaciarse
    [SerializeField] private float radioEsparcido = 0.6f;
    [SerializeField] private float anguloInclinacionMinimo = 90f; //Grados necesarios para que caigan

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoEsparcir;

    private Vector3 _escalaOriginalBulbo;

    private Grabbable _grabbable;
    private bool _esparcido = false; //Garantiza que solo se vacíe una vez
    private bool _inclinadaLoSuficiente = false;
    private int _manosSosteniendo = 0;
    private readonly List<GameObject> _bulbos = new List<GameObject>(); //Permite saber si el usuario la está manipulando

    private Quaternion _rotacionReposo; //Rotación inicial de referencia

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
        _rotacionReposo = transform.rotation;   
        PoblarCanasta(); //Llena la canasta con bulbos "falsos" o cinemáticos al inicio
    }

    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;

    //Llena la canasta con bulbos "falsos" o cinemáticos al inicio
    private void PoblarCanasta()
    {
        if (prefabBulboFisico == null || puntoInterior == null)
        {
            Debug.LogWarning("[Canasta] Falta asignar Prefab Bulbo Fisico o Punto Interior.");
            return;
        }

        for (int i = 0; i < bulbosDentro; i++)
        {
            //Posicionamiento aleatorio en círculo 2D transformado a espacio 3D interno
            Vector2 offset = Random.insideUnitCircle * radioInterior;
            Vector3 posicionMundo = puntoInterior.position + new Vector3(offset.x, Random.Range(0f, 0.05f), offset.y);

            GameObject bulbo = Instantiate(prefabBulboFisico, posicionMundo, Random.rotation);
            bulbo.transform.SetParent(puntoInterior, worldPositionStays: true);

            //Apaga físicas e interacción temporalmente
            _escalaOriginalBulbo = prefabBulboFisico.transform.localScale;
            DesactivarComoPlaceholder(bulbo);
            _bulbos.Add(bulbo);
        }
    }

    //Desactiva físicas e interacción de un bulbo para que actúe como placeholder dentro de la canasta
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

    //Despierta al bulbo con todas sus físicas para que caiga y pueda ser agarrado
    private void ActivarComoBulboFisico(GameObject bulbo, Vector3 posicionMundo)
    {
        bulbo.transform.SetParent(null, worldPositionStays: true); //Lo saca de la canasta
        bulbo.transform.position = posicionMundo;
        bulbo.transform.rotation = Random.rotation;
        bulbo.transform.localScale = _escalaOriginalBulbo;   

        foreach (var col in bulbo.GetComponentsInChildren<Collider>()) col.enabled = true;
        if (bulbo.TryGetComponent<Grabbable>(out var g)) g.enabled = true;
        if (bulbo.TryGetComponent<Bulbo>(out var b)) b.enabled = true;
        if (bulbo.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;
    }

    //Cuenta cuántas manos están sosteniendo la canasta para evitar que se vacíe mientras se manipula
    private void OnPointerEvent(PointerEvent evt)
    {
        //Lleva un conteo (útil por si se agarra con dos manos o se pasa de una a otra)
        if (evt.Type == PointerEventType.Select) _manosSosteniendo++;
        else if (evt.Type == PointerEventType.Unselect)
        {
            _manosSosteniendo = Mathf.Max(0, _manosSosteniendo - 1);
        }
    }

    //Verifica cada frame si la canasta está inclinada lo suficiente para vaciarse y si no está siendo sostenida
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

    //Convierte los bulbos placeholder en bulbos físicos y los dispersa en el área de esparcido
    private void Esparcir()
    {
        _esparcido = true;

        foreach (var bulbo in _bulbos)
        {
            if (bulbo == null) continue;

            //Calcula una posición aleatoria de caída sobre el área de esparcido
            Vector2 offset = Random.insideUnitCircle * radioEsparcido;
            Vector3 posicion = areaEsparcido.position + new Vector3(offset.x, 0.3f, offset.y);
            ActivarComoBulboFisico(bulbo, posicion);
        }
        //Vacía la lista interna
        _bulbos.Clear();

        if (audioSource != null && sonidoEsparcir != null)
            audioSource.PlayOneShot(sonidoEsparcir);
    }
}