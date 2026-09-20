using System;
using UnityEngine;

public interface IActivityManager
{
    bool ActividadCompletada { get; }
    event Action OnActividadCompletada;
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

    protected virtual void Awake()
    {
        Current = this;
    }

    protected virtual void OnDestroy()
    {
        //Solo te "desregistras" si sigues siendo el manager activo (evita pisar
        //al manager de la siguiente actividad si ya se cargó antes de que tú te destruyas).
        if (ReferenceEquals(Current, this))
            Current = null;
    }

    protected void MarcarCompletada() => OnActividadCompletada?.Invoke();
}