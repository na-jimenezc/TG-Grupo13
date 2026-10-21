// RebaSubtareas.cs
//
// Dice en qué subtarea está la persona en cada momento, para etiquetar cada muestra
// de REBA (columna "subtarea") y poder comparar pre / tutorial / post por subtarea.
// Usa los mismos nombres en los tres momentos.
//
//   Tutorial: el paso actual del tutorial del equipo (TutorialManagerBase).
//   Actividad (pre y post): no hay pasos, así que se infiere de lo que la persona
//   tiene en la mano (Grabbable de Meta) y de los avisos que ya lanzan los objetos:
//     Siembra   canasta en la mano -> Esparcir bulbos
//               pala en la mano    -> Cavar hueco
//               bulbo en la mano   -> Sembrar bulbo
//     Deshierbe ortiga en la mano, aún en la tierra -> Arrancar ortiga
//               ortiga en la mano, ya arrancada     -> Dejar ortiga
//     Corte     con las tijeras: Cortar tallo -> (tallo cortado) Deshojar tallo ->
//               (ramo completo) Cortar parejo -> (nivelado) Armar ramo ->
//               (capuchón puesto) Depositar ramo -> (depositado) Cortar tallo
//               capuchón en la mano -> Armar ramo; ramo terminado en la mano -> Depositar ramo
//   Sin nada de eso: "" (sin tarea: caminando hacia la siguiente planta, mirando…).
//
// No modifica los scripts del equipo: solo lee su estado y escucha sus eventos.

using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace PujJoycon
{
    public class RebaSubtareas : MonoBehaviour
    {
        // Nombres de los pasos de cada tutorial, en el orden de los TutorialStepData de su escena.
        private static readonly string[] PasosSiembra =
            { "Tomar canasta", "Esparcir bulbos", "Tomar pala", "Tomar bulbo", "Cavar hueco", "Sembrar bulbo" };
        private static readonly string[] PasosDeshierbe =
            { "Tomar bolsa", "Tomar ortiga", "Arrancar ortiga", "Dejar ortiga" };
        private static readonly string[] PasosCorte =
            { "Tomar tijeras", "Cortar tallo", "Deshojar tallo", "Cortar parejo", "Armar ramo", "Depositar ramo" };

        public const string CortarTallo = "Cortar tallo", DeshojarTallo = "Deshojar tallo", CortarParejo = "Cortar parejo",
                            ArmarRamo = "Armar ramo", DepositarRamo = "Depositar ramo";

        [Tooltip("Cada cuánto se vuelven a buscar los objetos de la actividad (aparecen bulbos nuevos al esparcir).")]
        [SerializeField] private float segundosEntreBusquedas = 1f;

        private readonly List<(Component objeto, Grabbable agarre)> _objetos = new List<(Component, Grabbable)>();
        private float _proximaBusqueda;
        private IActivityManager _manager;
        private string _faseCorte = CortarTallo;

        private void OnEnable()
        {
            TalloEnPlantacion.OnCortado += AlCortarTallo;
            TalloCortado.OnDeshojado += AlDeshojar;
            RamoEnProgreso.OnNivelado += AlNivelar;
            Capuchon.OnEnsamblado += AlEnsamblar;
            RamoTerminado.OnDepositadoEnMalla += AlDepositarRamo;
        }

        private void OnDisable()
        {
            TalloEnPlantacion.OnCortado -= AlCortarTallo;
            TalloCortado.OnDeshojado -= AlDeshojar;
            RamoEnProgreso.OnNivelado -= AlNivelar;
            Capuchon.OnEnsamblado -= AlEnsamblar;
            RamoTerminado.OnDepositadoEnMalla -= AlDepositarRamo;
        }

        private void AlCortarTallo() => _faseCorte = DeshojarTallo;
        private void AlDeshojar() => _faseCorte = RamoLleno() ? CortarParejo : CortarTallo;
        private void AlNivelar() => _faseCorte = ArmarRamo;
        private void AlEnsamblar() => _faseCorte = DepositarRamo;
        private void AlDepositarRamo() => _faseCorte = CortarTallo;

        private static bool RamoLleno()
        {
            var corte = CorteActivityManager.Instance;
            return corte != null && corte.TallosEnRamoActual >= corte.TallosPorRamo;
        }

        /// <summary>Subtarea actual ("" = sin tarea).</summary>
        public string Subtarea(FaseSesion fase, ActivityType? actividad)
        {
            if (actividad == null) return "";
            if (fase == FaseSesion.Tutorial) return PasoTutorial(actividad.Value);
            if (fase != FaseSesion.Actividad) return "";

            ActualizarObjetos();
            switch (actividad.Value)
            {
                case ActivityType.Siembra: return SubtareaSiembra();
                case ActivityType.Deshierbe: return SubtareaDeshierbe();
                case ActivityType.Corte: return SubtareaCorte();
                default: return "";
            }
        }

        /// <summary>Cuántas unidades lleva: bulbos sembrados, ortigas depositadas o tallos procesados.</summary>
        public int AccionesCompletadas(ActivityType? actividad)
        {
            switch (actividad)
            {
                case ActivityType.Siembra:
                    return SiembraActivityManager.Instance != null ? SiembraActivityManager.Instance.PlantasSembradas : 0;
                case ActivityType.Deshierbe:
                    return DeshierbeActivityManager.Instance != null ? DeshierbeActivityManager.Instance.OrtigasRecolectadas : 0;
                case ActivityType.Corte:
                    var c = CorteActivityManager.Instance;
                    return c != null ? c.RamosCompletados * c.TallosPorRamo + c.TallosEnRamoActual : 0;
                default:
                    return 0;
            }
        }

        private static string PasoTutorial(ActivityType actividad)
        {
            var tutorial = TutorialManagerBase.Actual;
            if (tutorial == null) return "";
            string[] pasos = actividad == ActivityType.Siembra ? PasosSiembra
                           : actividad == ActivityType.Deshierbe ? PasosDeshierbe : PasosCorte;
            int i = tutorial.PasoActual;
            return i >= 0 && i < pasos.Length ? pasos[i] : $"Paso {i + 1}";
        }

        private string SubtareaSiembra()
        {
            if (Agarrado<Bulbo>(out _)) return "Sembrar bulbo";
            if (Agarrado<Pala>(out _)) return "Cavar hueco";
            if (Agarrado<Canasta>(out _)) return "Esparcir bulbos";
            return "";
        }

        private string SubtareaDeshierbe()
        {
            if (Agarrado<Ortiga>(out var ortiga)) return ortiga.EstaExtraida ? "Dejar ortiga" : "Arrancar ortiga";
            return "";
        }

        private string SubtareaCorte()
        {
            if (Agarrado<RamoTerminado>(out _)) return DepositarRamo;
            if (Agarrado<Capuchon>(out _)) return ArmarRamo;
            if (Agarrado<Tijeras>(out _)) return _faseCorte;
            return "";
        }

        private bool Agarrado<T>(out T objeto) where T : Component
        {
            foreach (var (o, agarre) in _objetos)
            {
                if (o is T t && o != null && agarre != null && agarre.SelectingPointsCount > 0)
                {
                    objeto = t;
                    return true;
                }
            }
            objeto = null;
            return false;
        }

        // Los objetos de la actividad cambian (bulbos que aparecen, ortigas que se destruyen):
        // se vuelven a buscar cada segundo, y la fase de Corte se reinicia al cambiar de actividad.
        private void ActualizarObjetos()
        {
            var manager = ActivityManagerBase.Current;
            if (!ReferenceEquals(manager, _manager))
            {
                _manager = manager;
                _faseCorte = CortarTallo;
                _proximaBusqueda = 0f;
            }
            if (Time.unscaledTime < _proximaBusqueda) return;
            _proximaBusqueda = Time.unscaledTime + segundosEntreBusquedas;

            _objetos.Clear();
            Buscar<Bulbo>();
            Buscar<Pala>();
            Buscar<Canasta>();
            Buscar<Ortiga>();
            Buscar<Tijeras>();
            Buscar<Capuchon>();
            Buscar<RamoTerminado>();
        }

        private void Buscar<T>() where T : Component
        {
            foreach (var o in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                var agarre = o.GetComponent<Grabbable>();
                if (agarre != null) _objetos.Add((o, agarre));
            }
        }
    }
}
