using UnityEngine;
using Oculus.Interaction;/*El producto empaquetado y final de un conjunto de flores niveladas.*/



[RequireComponent(typeof(Grabbable))]
public class RamoTerminado : MonoBehaviour

{

    private Grabbable _grabbable;

    private ZonaDeposito _zonaDebajo;

    private bool _registradoEnMalla = false; //Evita que se cuente dos veces si se levanta

    private bool _apiladoEnHombro = false; //Marca de transición



    private void Awake()

    {

        _grabbable = GetComponent<Grabbable>();

        _grabbable.WhenPointerEventRaised += OnPointerEvent;

    }



    private void OnDestroy() => _grabbable.WhenPointerEventRaised -= OnPointerEvent;



    //Se llama cuando el ramo es soltado (evento Unselect) y se encuentra cerca de la mano izquierda o en la zona de depósito.

    private void OnPointerEvent(PointerEvent evt)

    {

        //Se ejecuta solo al soltar el objeto, y si no ha sido guardado ya

        if (evt.Type != PointerEventType.Unselect || _apiladoEnHombro) return;



        //FASE FINAL DE RECOLECCIÓN

        //Si ya se cumplió la meta de armar ramos, los ramos deben llevarse al hombro

        if (CorteActivityManager.Instance.RamosListos

        && HombroAcumulador.Instance != null

        && HombroAcumulador.Instance.PuedeRecibir(transform.position))

        {

            _apiladoEnHombro = true;

            HombroAcumulador.Instance.Apilar(gameObject); //Transfiere control al Hombro

            return;

        }



        //FASE NORMAL DE CONSTRUCCIÓN

        //(Dejarlo temporalmente en la malla/mesa)

        if (!_registradoEnMalla && _zonaDebajo != null && _zonaDebajo.EsZonaDeRamos)

        {

            _registradoEnMalla = true;

            //Aumenta el progreso de "Ramos Creados"

            CorteActivityManager.Instance.RegistrarRamoCompletado();

        }

    }



    //Identifica en qué zona se encuentra actualmente (Balde o Malla)

    private void OnTriggerEnter(Collider other)

    {

        if (other.TryGetComponent<ZonaDeposito>(out var zona)) _zonaDebajo = zona;

    }



    private void OnTriggerExit(Collider other)

    {

        if (other.TryGetComponent<ZonaDeposito>(out var zona) && zona == _zonaDebajo) _zonaDebajo = null;

    }

}