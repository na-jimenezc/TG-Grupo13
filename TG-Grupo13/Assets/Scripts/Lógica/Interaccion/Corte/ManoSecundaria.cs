using UnityEngine;

public class ManoSecundaria : MonoBehaviour
{
    public static ManoSecundaria Instance { get; private set; }

    [SerializeField] private Transform puntoAnclaje; //ANCHOR DE LA MANO SECUNDARIA PARA SOSTENER OBJETOS
    public Transform PuntoAnclaje => puntoAnclaje;

    private GameObject _objetoSostenido;
    public bool TieneAlgoEnMano => _objetoSostenido != null;

    private void Awake() => Instance = this;

    public void Sostener(GameObject objeto)
    {
        _objetoSostenido = objeto;
        objeto.transform.SetParent(puntoAnclaje, worldPositionStays: false);
        objeto.transform.localPosition = Vector3.zero;
        objeto.transform.localRotation = Quaternion.identity;

        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            Vector3 offsetPivotACentro = objeto.transform.position - bounds.center;
            objeto.transform.position += offsetPivotACentro;
        }
    }

    public void Soltar()
    {
        if (_objetoSostenido != null)
            _objetoSostenido.transform.SetParent(null, worldPositionStays: true);
        _objetoSostenido = null;
    }

    public GameObject ObjetoActual => _objetoSostenido;
}