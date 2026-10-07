using UnityEngine;

public class TutorialDeshierbeManager : TutorialManagerBase
{
    [Header("Bolsa de práctica")]
    [SerializeField] private Bolsa bolsaPractica;

    [Tooltip("Ortigas de práctica")]
    [SerializeField] private Ortiga[] ortigasPractica;

    [Header("Configuración de Meta")]
    [SerializeField] private int metaOrtigasTutorial = 8;
    private int _ortigasDepositadas = 0;

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
                _ortigasDepositadas = 0; // Reinicio de seguridad
                ActualizarContador(_ortigasDepositadas, metaOrtigasTutorial);

                // Se suscribe a un método intermedio en lugar de avanzar de inmediato
                SuscribirTodasLasOrtigas(o => o.OnDepositada += ContarOrtigaYAvanzar);
                break;

            default:
                Debug.LogWarning($"[TutorialDeshierbeManager] '{tipo}' no está mapeado.");
                break;
        }
    }

    // Intercepta el evento de depósito para contar antes de permitir el avance
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

    protected override void DesuscribirEventosDePractica()
    {
        if (bolsaPractica != null)
            bolsaPractica.OnAgarrada -= AvanzarPaso;

        SuscribirTodasLasOrtigas(o =>
        {
            o.OnAgarrada -= AvanzarPaso;
            o.OnExtraida -= AvanzarPaso;
            o.OnDepositada -= AvanzarPaso;
            o.OnDepositada -= ContarOrtigaYAvanzar; // Obligatorio limpiar la suscripción del contador
        });
    }

    private void SuscribirTodasLasOrtigas(System.Action<Ortiga> accion)
    {
        if (ortigasPractica == null) return;
        foreach (var ortiga in ortigasPractica)
        {
            if (ortiga != null) accion(ortiga);
        }
    }
}