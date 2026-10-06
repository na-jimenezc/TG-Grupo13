using UnityEngine;

/*Tutorial de la actividad de Deshierbe. Usa las instancias de Bolsa y Ortiga
// colocadas en la escena de tutorial, suscribiéndose a sus
// eventos opcionales para avanzar de paso cuando el usuario realiza el gesto real*/
public class TutorialDeshierbeManager : TutorialManagerBase
{
    [Header("Objetos reales de práctica (escena de tutorial)")]
    [SerializeField] private Bolsa bolsaPractica;
    [SerializeField] private Ortiga ortigaPractica;

    protected override void SuscribirEventoDelPaso(TipoEsperaTutorial tipo)
    {
        switch (tipo)
        {
            case TipoEsperaTutorial.EsperaAgarrarBolsa:
                bolsaPractica.OnAgarrada += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaAgarrarOrtiga:
                ortigaPractica.OnAgarrada += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaExtraerOrtiga:
                ortigaPractica.OnExtraida += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaDepositarOrtiga:
                ortigaPractica.OnDepositada += AvanzarPaso;
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

        if (ortigaPractica != null)
        {
            ortigaPractica.OnAgarrada -= AvanzarPaso;
            ortigaPractica.OnExtraida -= AvanzarPaso;
            ortigaPractica.OnDepositada -= AvanzarPaso;
        }
    }
}