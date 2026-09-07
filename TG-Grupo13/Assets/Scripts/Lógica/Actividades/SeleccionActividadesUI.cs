using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

//Encargado de controlar la UI de selección de actividades,
//mostrando el número de actividades seleccionadas y habilitando el botón de comenzar cuando haya al menos una actividad seleccionada
public class SeleccionActividadesUI : MonoBehaviour
{
    [SerializeField] private ActividadCardUI[] tarjetas;
    [SerializeField] private TMP_Text contadorText;
    [SerializeField] private Button comenzarButton;

    //Llamada de inicializar las tarjetas
    void Start()
    {
        foreach (var tarjeta in tarjetas)
            tarjeta.Inicializar(this);

        ActualizarContador();
    }

    //Contador de actividades seleccionadas
    public void OnSeleccionCambiada()
    {
        ActualizarContador();
    }

    //Actualiza el contador de actividades seleccionadas y habilita o deshabilita el botón de comenzar según corresponda
    void ActualizarContador()
    {
        int seleccionadas = tarjetas.Count(t => t.Seleccionada);
        contadorText.text = $"{seleccionadas}/{tarjetas.Length}\nActividades Seleccionadas";
        comenzarButton.interactable = seleccionadas > 0;
    }

    //Llamada al presionar el botón de comenzar, iniciando la secuencia de actividades seleccionadas
    public void OnComenzarPresionado()
    {
        List<ActivityType> seleccionadas = tarjetas.Where(t => t.Seleccionada).Select(t => t.Tipo).ToList();

        Debug.Log($"[UI] Botón Comenzar presionado. Actividades seleccionadas: {seleccionadas.Count}");

        if (seleccionadas.Count == 0) return;

        comenzarButton.interactable = false;

        if (GameFlowManager.Instance == null)
        {
            Debug.LogError("[UI] Error: GameFlowManager.Instance es NULO. No iniciaste desde el principio");
            return;
        }
        GameFlowManager.Instance.IniciarSecuenciaActividades(seleccionadas);
    }
}