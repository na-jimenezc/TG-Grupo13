using System;
using UnityEngine;

public interface IActivityManager
{
    bool ActividadCompletada { get; }
    event Action OnActividadCompletada;

    //Texto corto con el avance (p. ej. "Plantas sembradas: 4/10") y fracción de 0 a 1, para el panel del Game Master.
    string ResumenProgreso { get; }
    float FraccionProgreso { get; }
}

/*Clase base que registra automáticamente al manager activo en un slot estático compartido.
// Cada actividad (Siembra, Deshierbe, Corte) vive en su propia escena, así que solo hay
// un manager "vivo" a la vez — este patrón es análogo al Instance de GameFlowManager,
// pero limitado a la vida de la escena de activida*/

public abstract class ActivityManagerBase : MonoBehaviour, IActivityManager
{
    public static IActivityManager Current { get; private set; }

    public abstract bool ActividadCompletada { get; }
    public event Action OnActividadCompletada;

    public virtual string ResumenProgreso => ActividadCompletada ? "Meta cumplida" : "En curso";
    public virtual float FraccionProgreso => ActividadCompletada ? 1f : 0f;

    //Estático porque el panel del Game Master vive en Core y no conoce al manager hasta que su escena carga.
    public static event Action OnProgresoCambiado;

    protected virtual void Awake()
    {
        Current = this;
    }

    protected virtual void OnDestroy()
    {
        //Solo te "desregistras" si sigues siendo el manager activo (evita pisar
        //al manager de la siguiente actividad si ya se cargó antes de que tú te destruyas).
        if (ReferenceEquals(Current, this))
        {
            Current = null;
            OnProgresoCambiado?.Invoke();
        }
    }

    protected void MarcarCompletada() => OnActividadCompletada?.Invoke();

    //Las subclases lo llaman cada vez que cambian sus contadores.
    protected void NotificarProgreso() => OnProgresoCambiado?.Invoke();
}