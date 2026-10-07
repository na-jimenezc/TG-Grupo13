using System.Collections.Generic;
using UnityEngine;

/*Tutorial de la actividad de Siembra. Usa las instancias reales de Canasta, Pala, Bulbo
//y PlantingSlot colocadas en la escena de tutorial, suscribiéndose a sus eventos opcionales
//para avanzar de paso cuando el usuario realiza el gesto real*/
public class TutorialSiembraManager : TutorialManagerBase
{
    [Header("Objetos reales de práctica (escena de tutorial)")]
    [SerializeField] private Canasta canastaPractica;
    [SerializeField] private Pala palaPractica;

    [Tooltip("Slots de práctica: cualquiera que el usuario cave dispara el avance de ese paso")]
    [SerializeField] private PlantingSlot[] slotsPractica;

    [Header("Configuración de Meta")]
    [SerializeField] private int metaBulbosTutorial = 5;
    private int _bulbosSembrados = 0;

    //Los bulbos reales no existen antes de Play (la Canasta los crea e instancia en tiempo de
    //ejecución al vaciarse), así que no se pueden pre-asignar en el Inspector: la Canasta nos
    //avisa cuáles son justo cuando los activa, vía OnBulbosEsparcidos.
    private List<Bulbo> _bulbosPractica = new List<Bulbo>();

    private void Awake()
    {
        if (canastaPractica != null)
            canastaPractica.OnBulbosEsparcidos += RecibirBulbosEsparcidos;
    }

    private void OnDestroy()
    {
        if (canastaPractica != null)
            canastaPractica.OnBulbosEsparcidos -= RecibirBulbosEsparcidos;
    }

    private void RecibirBulbosEsparcidos(List<Bulbo> bulbos)
    {
        _bulbosPractica = bulbos;
    }

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
                SuscribirTodosLosBulbos(b => b.OnAgarrada += AvanzarPaso);
                break;

            case TipoEsperaTutorial.EsperaCavarSlot:
                SuscribirTodosLosSlots(s => s.OnCavado += AvanzarPaso);
                break;

            case TipoEsperaTutorial.EsperaSembrarBulbo:
                _bulbosSembrados = 0; // Reinicio de seguridad
                ActualizarContador(_bulbosSembrados, metaBulbosTutorial);

                // Se suscribe a un método intermedio en lugar de avanzar de inmediato
                SuscribirTodosLosSlots(s => s.OnSembrado += ContarBulboYAvanzar);
                break;

            default:
                Debug.LogWarning($"[TutorialSiembraManager] TipoEsperaTutorial '{tipo}' no está mapeado a ningún evento de práctica.");
                break;
        }
    }

    // Intercepta el evento de siembra para contar antes de permitir el avance
    private void ContarBulboYAvanzar()
    {
        _bulbosSembrados++;
        ActualizarContador(_bulbosSembrados, metaBulbosTutorial);

        if (_bulbosSembrados >= metaBulbosTutorial)
        {
            DesuscribirEventosDePractica();
            AvanzarPaso();
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

        SuscribirTodosLosBulbos(b => b.OnAgarrada -= AvanzarPaso);

        SuscribirTodosLosSlots(s =>
        {
            s.OnCavado -= AvanzarPaso;
            s.OnSembrado -= AvanzarPaso;
            s.OnSembrado -= ContarBulboYAvanzar; // Obligatorio limpiar la suscripción del contador
        });
    }

    private void SuscribirTodosLosBulbos(System.Action<Bulbo> accion)
    {
        if (_bulbosPractica == null) return;
        foreach (var bulbo in _bulbosPractica)
        {
            if (bulbo != null) accion(bulbo);
        }
    }

    private void SuscribirTodosLosSlots(System.Action<PlantingSlot> accion)
    {
        if (slotsPractica == null) return;
        foreach (var slot in slotsPractica)
        {
            if (slot != null) accion(slot);
        }
    }
}