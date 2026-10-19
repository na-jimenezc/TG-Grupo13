// RebaStats.cs
//
// Acumula las muestras de REBA de un tramo (ej. "Siembra, pre-tutorial, intento 1")
// y calcula su resumen: promedio, mediana, percentil 90, máximo (y cuándo), % del
// tiempo en cada nivel de riesgo y el puntaje promedio de cada parte del cuerpo.
// No depende de UnityEngine.
//
// Las muestras se toman a frecuencia fija (RebaSessionRecorder), así que el % de
// muestras en un nivel de riesgo es el % de tiempo en ese nivel.

using System;
using System.Collections.Generic;

namespace PujJoycon
{
    public sealed class RebaStats
    {
        private readonly List<int> _finals = new List<int>();
        private readonly int[] _riskCount = new int[5];
        private double _duration;
        private int _max;
        private double _timeOfMax;

        // Sumas de los puntajes por parte (del lado peor de cada muestra).
        private double _trunk, _neck, _legs, _upperArm, _lowerArm, _wrist;
        // Puntajes del brazo por lado (el grupo B se evalúa por lado).
        private double _upperArmLeft, _upperArmRight, _lowerArmLeft, _lowerArmRight, _wristLeft, _wristRight;

        public int Count => _finals.Count;
        public double DurationSeconds => _duration;
        public int Max => _max;
        /// <summary>Segundos desde el inicio del tramo hasta el primer REBA máximo.</summary>
        public double TimeOfMaxSeconds => _timeOfMax;

        /// <summary>Agrega una muestra. <paramref name="dt"/> = tiempo que representa (1 / frecuencia).</summary>
        public void Add(RebaResult left, RebaResult right, double dt)
        {
            RebaResult worst = left.final >= right.final ? left : right;
            if (_finals.Count == 0 || worst.final > _max)
            {
                _max = worst.final;
                _timeOfMax = _duration;
            }
            _finals.Add(worst.final);
            _riskCount[(int)worst.risk]++;
            _duration += dt;

            _trunk += worst.trunk; _neck += worst.neck; _legs += worst.legs;
            _upperArm += worst.upperArm; _lowerArm += worst.lowerArm; _wrist += worst.wrist;
            _upperArmLeft += left.upperArm; _upperArmRight += right.upperArm;
            _lowerArmLeft += left.lowerArm; _lowerArmRight += right.lowerArm;
            _wristLeft += left.wrist; _wristRight += right.wrist;
        }

        /// <summary>Suma otro tramo a este (para el total de todos los intentos de una condición).</summary>
        public void Merge(RebaStats other)
        {
            if (other == null || other.Count == 0) return;
            if (Count == 0 || other._max > _max)
            {
                _max = other._max;
                _timeOfMax = _duration + other._timeOfMax;
            }
            _finals.AddRange(other._finals);
            for (int i = 0; i < _riskCount.Length; i++) _riskCount[i] += other._riskCount[i];
            _duration += other._duration;
            _trunk += other._trunk; _neck += other._neck; _legs += other._legs;
            _upperArm += other._upperArm; _lowerArm += other._lowerArm; _wrist += other._wrist;
            _upperArmLeft += other._upperArmLeft; _upperArmRight += other._upperArmRight;
            _lowerArmLeft += other._lowerArmLeft; _lowerArmRight += other._lowerArmRight;
            _wristLeft += other._wristLeft; _wristRight += other._wristRight;
        }

        public double Mean
        {
            get
            {
                if (Count == 0) return 0;
                double sum = 0;
                foreach (int f in _finals) sum += f;
                return sum / Count;
            }
        }

        public double Median => Percentile(50);

        /// <summary>Percentil por rango más cercano (p en 0..100). 0 si no hay muestras.</summary>
        public double Percentile(double p)
        {
            if (Count == 0) return 0;
            var sorted = new List<int>(_finals);
            sorted.Sort();
            int rank = (int)Math.Ceiling(p / 100.0 * sorted.Count);
            rank = Math.Max(1, Math.Min(sorted.Count, rank));
            return sorted[rank - 1];
        }

        /// <summary>% de las muestras (= del tiempo) en el nivel de riesgo dado.</summary>
        public double RiskPercent(RebaRisk risk) => Count == 0 ? 0 : 100.0 * _riskCount[(int)risk] / Count;

        public double MeanTrunk => Avg(_trunk);
        public double MeanNeck => Avg(_neck);
        public double MeanLegs => Avg(_legs);
        public double MeanUpperArm => Avg(_upperArm);
        public double MeanLowerArm => Avg(_lowerArm);
        public double MeanWrist => Avg(_wrist);
        public double MeanUpperArmLeft => Avg(_upperArmLeft);
        public double MeanUpperArmRight => Avg(_upperArmRight);
        public double MeanLowerArmLeft => Avg(_lowerArmLeft);
        public double MeanLowerArmRight => Avg(_lowerArmRight);
        public double MeanWristLeft => Avg(_wristLeft);
        public double MeanWristRight => Avg(_wristRight);

        private double Avg(double sum) => Count == 0 ? 0 : sum / Count;
    }
}
