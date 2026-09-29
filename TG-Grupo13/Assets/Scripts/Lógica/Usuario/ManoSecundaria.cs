using UnityEngine;

/*Singleton que representa el punto de anclaje de la mano izquierda del usuario, que puede sostener objetos. 
 * Se utiliza para sostener los tallos cortados y otros objetos interactivos.*/
public class ManoSecundaria : MonoBehaviour
{
    public static ManoSecundaria Instance { get; private set; }

    [SerializeField] private Transform puntoAnclaje; //ANCHOR DE LA MANO SECUNDARIA PARA SOSTENER OBJETOS
    public Transform PuntoAnclaje => puntoAnclaje;

    private GameObject _objetoSostenido;
    public bool TieneAlgoEnMano => _objetoSostenido != null;

    private void Awake() => Instance = this;

    //Sostiene un objeto en la mano secundaria, ajustando su posición y rotación al punto de anclaje.
    public void Sostener(GameObject objeto)
    {
        _objetoSostenido = objeto;
        //Se emparenta el objeto a la mano
        objeto.transform.SetParent(puntoAnclaje, worldPositionStays: false);
        objeto.transform.localPosition = Vector3.zero;
        objeto.transform.localRotation = Quaternion.identity;

        //Lógica para alinear el centro de la malla del objeto con el punto de anclaje
        Renderer[] renderers = objeto.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds); //Encapsulamiento de mallas para obtener el centro total del objeto

            //Se calcula la diferencia entre el pivote original y el centro real de las mallas
            Vector3 offsetPivotACentro = objeto.transform.position - bounds.center;
            objeto.transform.position += offsetPivotACentro;
        }
    }

    //Suelta el objeto actual de la mano secundaria, dejándolo libre en el mundo.
    public void Soltar()
    {
        if (_objetoSostenido != null)
            _objetoSostenido.transform.SetParent(null, worldPositionStays: true);
        _objetoSostenido = null;
    }

    public GameObject ObjetoActual => _objetoSostenido;
}