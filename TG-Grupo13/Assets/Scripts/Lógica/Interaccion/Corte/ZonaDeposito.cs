using UnityEngine;

//Identificador lógico simple para los colliders del entorno (malla o balde).
//Ponlo en un GameObject HIJO dedicado (ej. "ZonaDeposito_Malla"), con su propio BoxCollider,
//separado del collider sólido de la malla.
[RequireComponent(typeof(BoxCollider))]
public class ZonaDeposito : MonoBehaviour
{
    [Tooltip("true = zona de la malla de entutorado (recibe ramos individuales). false = el balde (recibe la recolección final).")]
    public bool EsZonaDeRamos = true;

    [Tooltip("Metros que se extiende el trigger hacia ARRIBA al iniciar el juego. El ramo se apoya sobre la superficie sólida, así que el trigger debe sobresalir de ella.")]
    [SerializeField] private float alturaExtra = 0.15f;

    private BoxCollider _box;

    //Al añadir el componente en el editor, deja el collider como trigger
    private void Reset()
    {
        var box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    //Mantiene el collider como trigger si alguien lo desmarca por error
    private void OnValidate()
    {
        if (TryGetComponent<BoxCollider>(out var box) && !box.isTrigger)
        {
            box.isTrigger = true;
            Debug.LogWarning($"[ZonaDeposito] '{name}': el BoxCollider debe ser Trigger; se marcó automáticamente.", this);
        }
    }

    private void Awake()
    {
        _box = GetComponent<BoxCollider>();
        _box.isTrigger = true;

        //Se hace en Awake (no en el editor) para que no se acumule cada vez que se recompila
        if (alturaExtra > 0f)
        {
            var s = _box.size;
            var c = _box.center;
            //Se divide por la escala en Y porque size/center están en espacio local
            float extraLocal = alturaExtra / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
            s.y += extraLocal;
            c.y += extraLocal * 0.5f;
            _box.size = s;
            _box.center = c;
        }
    }

    //Utilidad por si necesitas comprobar posición sin depender de los eventos de trigger
    public bool Contiene(Vector3 puntoMundo)
    {
        return _box != null && _box.bounds.Contains(puntoMundo);
    }

    //Visualiza la zona en la Scene: verde = malla, azul = balde
    private void OnDrawGizmos()
    {
        if (!TryGetComponent<BoxCollider>(out var box)) return;

        Gizmos.color = EsZonaDeRamos ? new Color(0f, 1f, 0f, 0.9f) : new Color(0f, 0.5f, 1f, 0.9f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}