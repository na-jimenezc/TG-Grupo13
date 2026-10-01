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
    [SerializeField] private float radioEsparcido = 0.3f;
    [SerializeField] private float anguloInclinacionMinimo = 50f; //Grados necesarios para que caigan

    [Header("Efectos de Sonido")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoEsparcir;

    [Header("Sensibilidad")]
    [Tooltip("Manos que deben sostener la canasta a la vez para que pueda vaciarse. (Pon 1 para probar solo el ángulo.)")]
    [SerializeField] private int manosRequeridas = 2;
    [Tooltip("Segundos que debe mantenerse inclinada para vaciarse (evita picos al agarrarla o al moverla rápido).")]
    [SerializeField] private float tiempoMinimoInclinada = 0.25f;
    [Tooltip("Segundos tras agarrar/cambiar de mano durante los que se ignora la inclinación (el agarre puede reorientar la canasta).")]
    [SerializeField] private float retardoTrasAgarre = 0.5f;

    [Header("Diagnóstico")]
    [Tooltip("Imprime cada 0.5 s (mientras la sostienes) manos, inclinación y estado. Desactívalo al terminar.")]
    [SerializeField] private bool logDiagnostico = true;

    private Vector3 _escalaOriginalBulbo;

    private Grabbable _grabbable;
    private bool _esparcido = false; //Garantiza que solo se vacíe una vez
    private int _manosSosteniendo = 0;
    private readonly List<GameObject> _bulbos = new List<GameObject>();

    //Eje de la "boca" de la canasta en espacio LOCAL (el vector que apunta hacia arriba cuando está en reposo).
    //Se calcula en Awake y se RECALIBRA una sola vez en el primer agarre, por si algún script
    //(carga de la actividad, spawn) movió o rotó la canasta después de Awake.
    private Vector3 _ejeBocaLocal;
    private bool _ejeCalibradoEnAgarre = false;

    private float _tiempoInclinada = 0f;
    private float _tiempoUltimoCambioAgarre = -999f;
    private float _proximoLog = 0f;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
        _ejeBocaLocal = transform.InverseTransformDirection(Vector3.up);
        PoblarCanasta(); //Llena la canasta con bulbos "falsos" o cinemáticos al inicio
    }

    private void OnDestroy()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

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

    //Cuenta cuántas manos están sosteniendo la canasta (útil si se agarra con dos manos o se pasa de una a otra)
    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                //Primer agarre: la canasta aún está en reposo, es el mejor momento para calibrar el eje de la boca
                if (!_ejeCalibradoEnAgarre && _manosSosteniendo == 0)
                {
                    _ejeBocaLocal = transform.InverseTransformDirection(Vector3.up);
                    _ejeCalibradoEnAgarre = true;
                }
                _manosSosteniendo++;
                _tiempoUltimoCambioAgarre = Time.time;
                _tiempoInclinada = 0f;
                if (logDiagnostico) Debug.Log($"[Canasta] Select -> manos={_manosSosteniendo}");
                break;

            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                _manosSosteniendo = Mathf.Max(0, _manosSosteniendo - 1);
                _tiempoUltimoCambioAgarre = Time.time;
                _tiempoInclinada = 0f;
                if (logDiagnostico) Debug.Log($"[Canasta] {evt.Type} -> manos={_manosSosteniendo}");
                break;
        }
    }

    //Mide la inclinación de la boca respecto a la vertical del MUNDO (ignora giros sobre el eje vertical)
    private void Update()
    {
        Vector3 bocaMundo = transform.TransformDirection(_ejeBocaLocal);
        float angulo = Vector3.Angle(bocaMundo, Vector3.up);

        if (logDiagnostico && _manosSosteniendo > 0 && Time.time >= _proximoLog)
        {
            _proximoLog = Time.time + 0.5f;
            bool enRetardo = Time.time - _tiempoUltimoCambioAgarre < retardoTrasAgarre;
            Debug.Log($"[Canasta] manos={_manosSosteniendo}/{manosRequeridas} | inclinación={angulo:F0}° (umbral {anguloInclinacionMinimo:F0}°) | esparcido={_esparcido} | enRetardo={enRetardo} | tiempoInclinada={_tiempoInclinada:F2}s");
        }

        //Solo se puede vaciar si la sostienen las manos requeridas a la vez
        if (_manosSosteniendo < manosRequeridas || _esparcido) { _tiempoInclinada = 0f; return; }

        //Justo al agarrar, el SDK puede reorientar la canasta hacia la mano: se ignora ese momento
        if (Time.time - _tiempoUltimoCambioAgarre < retardoTrasAgarre) { _tiempoInclinada = 0f; return; }

        if (angulo >= anguloInclinacionMinimo)
        {
            _tiempoInclinada += Time.deltaTime;
            if (_tiempoInclinada >= tiempoMinimoInclinada)
            {
                Debug.Log($"[Canasta] Esparcir: inclinación {angulo:F0}° sostenida {_tiempoInclinada:F2}s");
                Esparcir();
            }
        }
        else
        {
            _tiempoInclinada = 0f;
        }
    }

    //Convierte los bulbos placeholder en bulbos físicos y los dispersa en el área de esparcido
    private void Esparcir()
    {
        _esparcido = true;

        Transform destino = areaEsparcido;
        if (destino == null)
        {
            Debug.LogError("[Canasta] Falta asignar Area Esparcido; se usa la posición de la canasta.", this);
            destino = transform;
        }

        foreach (var bulbo in _bulbos)
        {
            if (bulbo == null) continue;

            //Calcula una posición aleatoria de caída sobre el área de esparcido
            Vector2 offset = Random.insideUnitCircle * radioEsparcido;
            Vector3 posicion = destino.position + new Vector3(offset.x, 0.3f, offset.y);
            ActivarComoBulboFisico(bulbo, posicion);
        }
        //Vacía la lista interna
        _bulbos.Clear();

        if (audioSource != null && sonidoEsparcir != null)
            audioSource.PlayOneShot(sonidoEsparcir);
    }
}