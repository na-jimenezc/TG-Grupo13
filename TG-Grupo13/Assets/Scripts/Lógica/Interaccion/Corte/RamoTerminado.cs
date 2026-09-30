using UnityEngine;
using Oculus.Interaction;

/*El producto empaquetado y final de un conjunto de flores niveladas.*/
[RequireComponent(typeof(Grabbable))]
public class RamoTerminado : MonoBehaviour
{
    private Grabbable _grabbable;
    private ZonaDeposito _zonaDebajo;

    private int _agarres = 0;                  //Cuántos interactores lo sostienen ahora mismo
    private bool _registradoEnMalla = false;   //Evita que se cuente dos veces
    private bool _apiladoEnHombro = false;     //Marca de transición

    private void Awake()
    {
        _grabbable = GetComponent<Grabbable>();
        _grabbable.WhenPointerEventRaised += OnPointerEvent;
    }

    private void OnDestroy()
    {
        if (_grabbable != null) _grabbable.WhenPointerEventRaised -= OnPointerEvent;
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
                if (_agarres == 0) AlSoltar();
                break;
        }
    }

    private void AlSoltar()
    {
        if (_apiladoEnHombro) return;

        //FASE FINAL: con todos los ramos listos, se transfieren a la mano izquierda
        var manager = CorteActivityManager.Instance;
        if (manager != null && manager.RamosListos
            && HombroAcumulador.Instance != null
            && HombroAcumulador.Instance.PuedeRecibir(transform.position))
        {
            _apiladoEnHombro = true;
            HombroAcumulador.Instance.Apilar(gameObject);
            return;
        }

        //FASE NORMAL: puede que ya esté sobre la malla...
        IntentarRegistrarEnMalla();
    }

    //Se puede llamar desde varios sitios: solo cuenta una vez y solo si NO está agarrado
    private void IntentarRegistrarEnMalla()
    {
        if (_registradoEnMalla || _apiladoEnHombro || _agarres > 0) return;
        if (_zonaDebajo == null || !_zonaDebajo.EsZonaDeRamos) return;

        _registradoEnMalla = true;
        Debug.Log($"[RamoTerminado] Registrado en malla: {name}");
        CorteActivityManager.Instance.RegistrarRamoCompletado();
    }

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