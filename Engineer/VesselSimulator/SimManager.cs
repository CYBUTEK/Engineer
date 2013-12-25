using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace Engineer.VesselSimulator
{
    public class SimManager
    {
        private static SimManager _instance;

        public static SimManager Instance
        {
            get
            {
                if (_instance == null) _instance = new SimManager();
                return _instance;
            }
        }

        private bool _simRequested = false;
        private bool _simRunning = false;
        private Stopwatch _timer = new Stopwatch();
        private long _millisecondsBetweenSimulations = 0;

        public Stage[] Stages { get; private set; }
        public Stage LastStage { get; private set; }

        public double Gravity { get; set; }
        public double Atmosphere { get; set; }

        public void RequestSimulation()
        {
            _simRequested = true;
            if (!_timer.IsRunning) _timer.Start();
        }

        public void TryStartSimulation()
        {
            if ((HighLogic.LoadedSceneIsEditor || FlightGlobals.ActiveVessel != null) && !_simRunning)
            {
                if (_timer.ElapsedMilliseconds > _millisecondsBetweenSimulations)
                {
                    if (_simRequested)
                    {
                        _simRequested = false;
                        _timer.Reset();

                        StartSimulation();
                    }
                }
            }
        }

        private void StartSimulation()
        {
            _simRunning = true;
            _timer.Start();

            List<Part> parts = HighLogic.LoadedSceneIsEditor ? EditorLogic.SortedShipList : FlightGlobals.ActiveVessel.Parts;

            if (parts.Count > 0)
            {
                ThreadPool.QueueUserWorkItem(RunSimulation, new Simulation(parts));
                //RunSimulation(new Simulation(parts));
            }
            else
            {
                Stages = null;
            }
        }

        private void RunSimulation(object simObject)
        {
            try
            {
                this.Stages = (simObject as Simulation).RunSimulation(this.Gravity, this.Atmosphere);
                this.LastStage = this.Stages.Last();
            }
            catch { /* Something went wrong! */ }

            _timer.Stop();
            _millisecondsBetweenSimulations = 10 * _timer.ElapsedMilliseconds;

            _timer.Reset();
            _timer.Start();

            _simRunning = false;
        }
    }
}
