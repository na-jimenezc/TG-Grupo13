using UnityEngine;
using Oculus.Interaction;

/*El producto empaquetado y final de un conjunto de flores niveladas.*/
[RequireComponent(typeof(Grabbable))]
public class RamoTerminado : MonoBehaviour
{
    private Grabbable _grabbable;
    private ZonaDeposito _zonaDebajo;

    private int _agarres = 0;                  //Cuántos interactores lo sostienen ahora mismo (derecha, izquierda o ambas)
    private bool _registradoEnMalla = false;   //Evita que se cuente dos veces
    private bool _apiladoEnHombro = false;     //Marca de transición

    //EVENTO DEL TUTORIAL (estático porque los ramos se instancian en runtime)
    public static event System.Action OnDepositadoEnMalla;

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised -= OnPointerEvent;
    }

    private void Update()
    {
        //FASE FINAL: mientras el ramo está agarrado (por la mano que sea), si se presiona
        //trigger + grab izquierdos a la vez, pasa al hombro.
        if (_agarres <= 0 || _apiladoEnHombro) return;

        var manager = CorteActivityManager.Instance;
        var hombro = HombroAcumulador.Instance;
        if (manager == null || hombro == null || !manager.RamosListos) return;

        if (hombro.GestoApilarPresionado)
        {
            _apiladoEnHombro = true;
            Debug.Log($"[RamoTerminado] Apilado en hombro: {name}");
            hombro.Apilar(gameObject);
        }
    }

    private void OnPointerEvent(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Select:
                _agarres++;
                break;

            case PointerEventType.Unselect:
            case PointerEventType.Cancel:
                _agarres = Mathf.Max(0, _agarres - 1);
                if (_agarres == 0) IntentarRegistrarEnMalla();
                break;
        }
    }

    //Solo cuenta una vez y solo si NO está agarrado
    private void IntentarRegistrarEnMalla()
    {
        if (_registradoEnMalla || _apiladoEnHombro || _agarres > 0) return;
        if (_zonaDebajo == null || !_zonaDebajo.EsZonaDeRamos) return;

        _registradoEnMalla = true;
        Debug.Log($"[RamoTerminado] Registrado en malla: {name}");
        CorteActivityManager.Instance?.RegistrarRamoCompletado();
        OnDepositadoEnMalla?.Invoke();
    }

    //También se revisa al entrar/permanecer en la zona, por si el ramo llega DESPUÉS de soltarse
    private void OnTriggerEnter(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona != null) _zonaDebajo = zona;
        IntentarRegistrarEnMalla();
    }

    private void OnTriggerStay(Collider other)
    {
        if (_registradoEnMalla) return;
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona != null) _zonaDebajo = zona;
        IntentarRegistrarEnMalla();
    }

    private void OnTriggerExit(Collider other)
    {
        var zona = other.GetComponentInParent<ZonaDeposito>();
        if (zona != null && zona == _zonaDebajo) _zonaDebajo = null;
    }
}