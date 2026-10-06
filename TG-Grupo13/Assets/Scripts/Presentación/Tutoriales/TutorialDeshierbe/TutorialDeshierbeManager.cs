using UnityEngine;

// Tutorial de la actividad de Deshierbe. Usa las instancias reales de Bolsa y Ortiga
// colocadas en la escena de tutorial (no las del maniquí/poses), suscribiéndose a sus
// eventos opcionales para avanzar de paso cuando el usuario realiza el gesto real.
public class TutorialDeshierbeManager : TutorialManagerBase
{
    [Header("Objetos reales de práctica (escena de tutorial)")]
    [SerializeField] private Bolsa bolsaPractica;

    [Tooltip("Son varias ortigas repartidas en la escena de tutorial: cualquiera que el usuario agarre/extraiga/deposite dispara el avance de ese paso")]
    [SerializeField] private Ortiga[] ortigasPractica;

    protected override void SuscribirEventoDelPaso(TipoEsperaTutorial tipo)
    {
        switch (tipo)
        {
            case TipoEsperaTutorial.EsperaAgarrarBolsa:
                bolsaPractica.OnAgarrada += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaAgarrarOrtiga:
                SuscribirTodasLasOrtigas(o => o.OnAgarrada += AvanzarPaso);
                break;

            case TipoEsperaTutorial.EsperaExtraerOrtiga:
                SuscribirTodasLasOrtigas(o => o.OnExtraida += AvanzarPaso);
                break;

            case TipoEsperaTutorial.EsperaDepositarOrtiga:
                SuscribirTodasLasOrtigas(o => o.OnDepositada += AvanzarPaso);
                break;

            default:
                Debug.LogWarning($"[TutorialDeshierbeManager] TipoEsperaTutorial '{tipo}' no está mapeado a ningún evento de práctica.");
                break;
        }
    }

    protected override void DesuscribirEventosDePractica()
    {
        if (bolsaPractica != null)
            bolsaPractica.OnAgarrada -= AvanzarPaso;

        SuscribirTodasLasOrtigas(o =>
        {
            o.OnAgarrada -= AvanzarPaso;
            o.OnExtraida -= AvanzarPaso;
            o.OnDepositada -= AvanzarPaso;
        });
    }

    //Aplica la acción dada a cada ortiga viva del arreglo. Varias pueden haberse destruido
    //(ya fueron depositadas) a lo largo del tutorial, por eso el chequeo de null en cada una.
    private void SuscribirTodasLasOrtigas(System.Action<Ortiga> accion)
    {
        if (ortigasPractica == null) return;
        foreach (var ortiga in ortigasPractica)
        {
            if (ortiga != null) accion(ortiga);
        }
    }
}