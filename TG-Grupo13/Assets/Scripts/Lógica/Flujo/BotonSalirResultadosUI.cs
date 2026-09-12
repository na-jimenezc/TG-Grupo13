using UnityEngine;

/*Vive sobre el propio botón de Salir en la Pantalla de Resultados. Llama al GameFlowManager por su
 * instancia estática (GameFlowManager.Instance) en vez de por una referencia arrastrada a otra escena,
 * ya que las referencias entre objetos de escenas distintas no se resuelven de forma confiable
 * cuando las escenas se cargan de forma aditiva en tiempos distintos.*/
public class BotonSalirResultadosUI : MonoBehaviour
{
    public void OnSalirPresionado()
    {
        GameFlowManager.Instance?.SalirDeSimulacion();
    }
}
