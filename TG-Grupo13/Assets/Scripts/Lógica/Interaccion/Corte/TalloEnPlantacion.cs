using UnityEngine;

public class TalloEnPlantacion : MonoBehaviour
{
    [SerializeField] private GameObject prefabTalloCortado; //MODELO DEL LIRIO CORTADO
    private bool _cortado = false;

    public void Cortar()
    {
        if (_cortado) return;
        _cortado = true;

        // TODO: sonido + animación de corte

        var manoSecundaria = ManoSecundaria.Instance;
        if (manoSecundaria != null && !manoSecundaria.TieneAlgoEnMano)
        {
            var talloCortado = Instantiate(prefabTalloCortado, manoSecundaria.PuntoAnclaje.position, manoSecundaria.PuntoAnclaje.rotation);
            manoSecundaria.Sostener(talloCortado);
        }

        gameObject.SetActive(false); // limpieza síncrona antes de desaparecer la planta original
        Destroy(gameObject, 0.1f);
    }
}