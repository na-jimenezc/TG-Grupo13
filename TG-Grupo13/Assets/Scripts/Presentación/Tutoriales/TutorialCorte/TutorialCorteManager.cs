using UnityEngine;

/*Tutorial de la actividad de Corte. Usa las tijeras reales colocadas en la escena de tutorial y los eventos
//estáticos de TalloEnPlantacion, TalloCortado, RamoEnProgreso, Capuchon y RamoTerminado (que se instancian
//en runtime) para avanzar de paso cuando el usuario realiza el gesto real*/
public class TutorialCorteManager : TutorialManagerBase
{
    [Header("Objetos reales de práctica")]
    [SerializeField] private Tijeras tijerasPractica;

    private int _tallosDeshojados = 0;

    //La meta se lee del ramo de la escena para que siempre coincida con lo que habilita el corte parejo
    private int MetaTallosTutorial => RamoEnProgreso.Instance != null ? RamoEnProgreso.Instance.TallosNecesarios : 0;

    //Los eventos estáticos sobreviven a la escena, así que se limpian al descargar el tutorial
    private void OnDestroy() => DesuscribirEventosDePractica();

    //Suscribe a los eventos de los objetos de práctica según el tipo de espera del paso actual
    protected override void SuscribirEventoDelPaso(TipoEsperaTutorial tipo)
    {
        switch (tipo)
        {
            case TipoEsperaTutorial.EsperaAgarrarTijeras:
                tijerasPractica.OnAgarradas += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaCortarTallo:
                TalloEnPlantacion.OnCortado += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaDeshojarTallo:
                _tallosDeshojados = 0; //Reinicio de seguridad
                ActualizarContador(_tallosDeshojados, MetaTallosTutorial);

                //Se suscribe a un método intermedio en lugar de avanzar de inmediato
                TalloCortado.OnDeshojado += ContarTalloYAvanzar;
                break;

            case TipoEsperaTutorial.EsperaNivelarRamo:
                RamoEnProgreso.OnNivelado += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaEnsamblarRamo:
                Capuchon.OnEnsamblado += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaDepositarRamoMalla:
                RamoTerminado.OnDepositadoEnMalla += AvanzarPaso;
                break;

            default:
                Debug.LogWarning($"[TutorialCorteManager] TipoEsperaTutorial '{tipo}' no está mapeado a ningún evento de práctica.");
                break;
        }
    }

    //Intercepta el evento de deshoje para contar antes de permitir el avance
    private void ContarTalloYAvanzar()
    {
        _tallosDeshojados++;
        ActualizarContador(_tallosDeshojados, MetaTallosTutorial);

        if (_tallosDeshojados >= MetaTallosTutorial)
        {
            DesuscribirEventosDePractica();
            AvanzarPaso();
        }
    }

    //Desuscribe de todos los eventos de práctica para evitar fugas de memoria y suscripciones duplicadas
    protected override void DesuscribirEventosDePractica()
    {
        if (tijerasPractica != null)
            tijerasPractica.OnAgarradas -= AvanzarPaso;

        TalloEnPlantacion.OnCortado -= AvanzarPaso;
        TalloCortado.OnDeshojado -= ContarTalloYAvanzar;
        RamoEnProgreso.OnNivelado -= AvanzarPaso;
        Capuchon.OnEnsamblado -= AvanzarPaso;
        RamoTerminado.OnDepositadoEnMalla -= AvanzarPaso;
    }
}
