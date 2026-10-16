using UnityEngine;

/*Tutorial de la actividad de Deshierbe. Usa las instancias reales de Bolsa y Ortiga y capta sus eventos para
 asegurar de que se esté avanzando*/
public class TutorialDeshierbeManager : TutorialManagerBase
{
    [Header("Bolsa de práctica")]
    [SerializeField] private Bolsa bolsaPractica;

    [Tooltip("Ortigas de práctica")]
    [SerializeField] private Ortiga[] ortigasPractica;

    [Header("Configuración de Meta")]
    [SerializeField] private int metaOrtigasTutorial = 8;
    private int _ortigasDepositadas = 0;

    /* Suscribe a los eventos de los objetos de práctica según el tipo de espera del paso actual*/
    protected override void SuscribirEventoDelPaso(TipoEsperaTutorial tipo)
    {
        //Se recibe le tipo de evento y se avanza al siguiente paso cuando se cumple la condición de ese evento
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
                _ortigasDepositadas = 0; //Reinicio de seguridad
                ActualizarContador(_ortigasDepositadas, metaOrtigasTutorial);

                //Se suscribe a un método intermedio en lugar de avanzar de inmediato
                SuscribirTodasLasOrtigas(o => o.OnDepositada += ContarOrtigaYAvanzar);
                break;

            default:
                Debug.LogWarning($"[TutorialDeshierbeManager] '{tipo}' no está mapeado.");
                break;
        }
    }

    //Intercepta el evento de depósito para contar antes de permitir el avance
    private void ContarOrtigaYAvanzar()
    {
        _ortigasDepositadas++;
        ActualizarContador(_ortigasDepositadas, metaOrtigasTutorial);

        if (_ortigasDepositadas >= metaOrtigasTutorial)
        {
            DesuscribirEventosDePractica();
            AvanzarPaso();
        }
    }

    //Desuscribe a todos los eventos de los objetos de práctica para evitar que se avance de manera incorrecta
    protected override void DesuscribirEventosDePractica()
    {
        if (bolsaPractica != null)
            bolsaPractica.OnAgarrada -= AvanzarPaso;

        SuscribirTodasLasOrtigas(o =>
        {
            o.OnAgarrada -= AvanzarPaso;
            o.OnExtraida -= AvanzarPaso;
            o.OnDepositada -= AvanzarPaso;
            o.OnDepositada -= ContarOrtigaYAvanzar; //Se limpia la sección del contador
        });
    }

    //Suscribe a todos los eventos de las ortigas de práctica para el evento
    private void SuscribirTodasLasOrtigas(System.Action<Ortiga> accion)
    {
        if (ortigasPractica == null) return;
        foreach (var ortiga in ortigasPractica)
        {
            if (ortiga != null) accion(ortiga);
        }
    }
}