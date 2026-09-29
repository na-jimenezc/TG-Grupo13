using UnityEngine;

//Objeto que representa un tallo de lirio en la plantación, que puede ser cortado por el usuario
public class TalloEnPlantacion : MonoBehaviour
{
    [SerializeField] private GameObject prefabTalloCortado; //MODELO DEL LIRIO CORTADO
    private bool _cortado = false;

    //Ejecuta la acción de corte sobre la planta viva.
    public void Cortar()
    {
        if (_cortado) return;
        _cortado = true;

        /*AGREGAR SONIDO DE CORTE*/

        var manoSecundaria = ManoSecundaria.Instance;
        if (manoSecundaria != null && !manoSecundaria.TieneAlgoEnMano)
        {
            //Si la mano izquierda está libre, spawnea el tallo cortado directamente en ella
            var talloCortado = Instantiate(prefabTalloCortado, manoSecundaria.PuntoAnclaje.position, manoSecundaria.PuntoAnclaje.rotation);
            manoSecundaria.Sostener(talloCortado);
        }

        gameObject.SetActive(false); //Limpieza síncrona antes de desaparecer la planta original
        Destroy(gameObject, 0.1f);
    }
}