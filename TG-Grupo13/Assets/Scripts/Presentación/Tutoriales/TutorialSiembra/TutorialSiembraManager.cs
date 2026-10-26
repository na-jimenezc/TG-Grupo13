using System.Collections.Generic;
using UnityEngine;

/*Tutorial de la actividad de Siembra. Usa las instancias reales de Canasta, Pala, Bulbo
//y PlantingSlot colocadas en la escena de tutorial, suscribiéndose a sus eventos opcionales
//para avanzar de paso cuando el usuario realiza el gesto real*/
public class TutorialSiembraManager : TutorialManagerBase
{
    [Header("Objetos reales de práctica")]
    [SerializeField] private Canasta canastaPractica;
    [SerializeField] private Pala palaPractica;

    [Tooltip("Slots de práctica")]
    [SerializeField] private PlantingSlot[] slotsPractica;

    [Header("Configuración de Meta")]
    [SerializeField] private int metaBulbosTutorial = 5;
    private int _bulbosSembrados = 0;

    //Los bulbos reales no existen antes de Play, la canasta avisa cuales son los objetos Bulbo que contiene al esparcirlos
    //, y se guardan en esta lista para suscribirse a sus eventos
    private List<Bulbo> _bulbosPractica = new List<Bulbo>();

    private void Awake()
    {
        if (canastaPractica != null)
            canastaPractica.OnBulbosEsparcidos += RecibirBulbosEsparcidos;
    }

    //Guía de controles del pre: usa la canasta, la pala y los huecos de la escena de actividad.
    protected override void BuscarObjetosEnEscena()
    {
        if (canastaPractica == null)
        {
            canastaPractica = FindAnyObjectByType<Canasta>();
            if (canastaPractica != null) canastaPractica.OnBulbosEsparcidos += RecibirBulbosEsparcidos;
        }
        if (palaPractica == null) palaPractica = FindAnyObjectByType<Pala>();
        if (slotsPractica == null || slotsPractica.Length == 0)
            slotsPractica = FindObjectsByType<PlantingSlot>(FindObjectsSortMode.None);
    }

    //Se desuscribe de los eventos de la canasta para evitar fugas de memoria
    private void OnDestroy()
    {
        if (canastaPractica != null)
            canastaPractica.OnBulbosEsparcidos -= RecibirBulbosEsparcidos;
    }

    //Recibe la lista de bulbos esparcidos por la canasta y los guarda para suscribirse a sus eventos
    private void RecibirBulbosEsparcidos(List<Bulbo> bulbos)
    {
        _bulbosPractica = bulbos;
    }

    //Suscribe a los eventos de los objetos de práctica según el tipo de espera del paso actual
    protected override void SuscribirEventoDelPaso(TipoEsperaTutorial tipo)
    {
        if (EsGuia) BuscarObjetosEnEscena();

        switch (tipo)
        {
            case TipoEsperaTutorial.EsperaAgarrarCanastaDosManos:
                if (canastaPractica != null) canastaPractica.OnAgarradaDosManos += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaInclinarCanasta:
                if (canastaPractica != null) canastaPractica.OnEsparcida += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaAgarrarPala:
                if (palaPractica != null) palaPractica.OnAgarrada += AvanzarPaso;
                break;

            case TipoEsperaTutorial.EsperaAgarrarBulbo:
                SuscribirTodosLosBulbos(b => b.OnAgarrada += AvanzarPaso);
                break;

            case TipoEsperaTutorial.EsperaCavarSlot:
                SuscribirTodosLosSlots(s => s.OnCavado += AvanzarPaso);
                break;

            case TipoEsperaTutorial.EsperaSembrarBulbo:
                _bulbosSembrados = 0; // Reinicio de seguridad
                ActualizarContador(_bulbosSembrados, Meta(metaBulbosTutorial));

                //Se suscribe a un método intermedio en lugar de avanzar de inmediato
                SuscribirTodosLosSlots(s => s.OnSembrado += ContarBulboYAvanzar);
                break;

            default:
                Debug.LogWarning($"[TutorialSiembraManager] TipoEsperaTutorial '{tipo}' no está mapeado a ningún evento de práctica.");
                break;
        }
    }

    //Intercepta el evento de siembra para contar antes de permitir el avance
    private void ContarBulboYAvanzar()
    {
        //Se cuenta el bulbo sembrado y se actualiza el contador en pantalla
        _bulbosSembrados++;
        ActualizarContador(_bulbosSembrados, Meta(metaBulbosTutorial));

        if (_bulbosSembrados >= Meta(metaBulbosTutorial))
        {
            DesuscribirEventosDePractica();
            AvanzarPaso();
        }
    }

    //Desuscribe de todos los eventos de práctica para evitar fugas de memoria y suscripciones duplicadas
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
            s.OnSembrado -= ContarBulboYAvanzar; 
        });
    }

    //Métodos auxiliares para suscribirse a todos los bulbos o slots de práctica, evitando null reference exceptions
    private void SuscribirTodosLosBulbos(System.Action<Bulbo> accion)
    {
        //En la escena de actividad los bulbos pueden existir sin haberlos esparcido en esta guía: se toman todos los de la escena.
        IEnumerable<Bulbo> bulbos = EsGuia ? FindObjectsByType<Bulbo>(FindObjectsSortMode.None) : _bulbosPractica;
        if (bulbos == null) return;
        foreach (var bulbo in bulbos)
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