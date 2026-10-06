using UnityEngine;

/*Tutorial de la actividad de Siembra. Usa las instancias reales de Canasta, Pala, Bulbo
//y PlantingSlot colocadas en la escena de tutorial, suscribiéndose a sus eventos opcionales
//para avanzar de paso cuando el usuario realiza el gesto real*/
public class TutorialSiembraManager : TutorialManagerBase
{
    [Header("Objetos reales de práctica (escena de tutorial)")]
    [SerializeField] private Canasta canastaPractica;
    [SerializeField] private Pala palaPractica;
    [SerializeField] private Bulbo bulboPractica;
    [SerializeField] private PlantingSlot slotPractica;

    protected override void SuscribirEventoDelPaso(TipoEsperaTutorial tipo)
    {
        switch (tipo)
        {
            case TipoEsperaTutorial.EsperaAgarrarCanastaDosManos:
                canastaPractica.OnAgarradaDosManos += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaInclinarCanasta:
                canastaPractica.OnEsparcida += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaAgarrarPala:
                palaPractica.OnAgarrada += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaAgarrarBulbo:
                bulboPractica.OnAgarrada += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaCavarSlot:
                slotPractica.OnCavado += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaSembrarBulbo:
                slotPractica.OnSembrado += AvanzarPaso;
                break;

            default:
                Debug.LogWarning($"[TutorialSiembraManager] TipoEsperaTutorial '{tipo}' no está mapeado a ningún evento de práctica.");
                break;
        }
    }

    protected override void DesuscribirEventosDePractica()
    {
        if (canastaPractica != null)
        {
            canastaPractica.OnAgarradaDosManos -= AvanzarPaso;
            canastaPractica.OnEsparcida -= AvanzarPaso;
        }

        if (palaPractica != null)
            palaPractica.OnAgarrada -= AvanzarPaso;

        if (bulboPractica != null)
            bulboPractica.OnAgarrada -= AvanzarPaso;

        if (slotPractica != null)
        {
            slotPractica.OnCavado -= AvanzarPaso;
            slotPractica.OnSembrado -= AvanzarPaso;
        }
    }
}