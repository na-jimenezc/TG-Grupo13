using UnityEngine;
using UnityEngine.UI;

//Encargado de controlar la UI de cada actividad, permitiendo seleccionar o deseleccionar la actividad y actualizar los iconos correspondientes
public class ActividadCardUI : MonoBehaviour
{
    [SerializeField] private ActivityType tipo;
    [SerializeField] private Button boton;
    [SerializeField] private GameObject iconoCheck;
    [SerializeField] private GameObject iconoX;

    //Selección de todas como base para mejorar experiencia de usuario, ya que la mayoría de las veces se seleccionan todas las actividades
    private bool _seleccionada = true;
    private SeleccionActividadesUI _controlador;

    public ActivityType Tipo => tipo;
    public bool Seleccionada => _seleccionada;

    //Inicializador del controlador de la UI de selección de actividades, asignando el botón y actualizando los iconos según el estado de selección
    public void Inicializar(SeleccionActividadesUI controlador)
    {
        _controlador = controlador;
        boton.onClick.AddListener(OnClickPresionado);
        ActualizarIconos();
    }

    void OnClickPresionado()
    {
        _seleccionada = !_seleccionada;
        ActualizarIconos();
        _controlador.OnSeleccionCambiada();
    }

    void ActualizarIconos()
    {
        iconoCheck.SetActive(_seleccionada);
        iconoX.SetActive(!_seleccionada);
    }
}