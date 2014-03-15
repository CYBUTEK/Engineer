using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using UnityEngine;

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

        private Stopwatch _func = new Stopwatch();

        public Stage[] Stages { get; private set; }
        public Stage LastStage { get; private set; }
        public String failMessage { get; private set; }

        public double Gravity { get; set; }
        public double Atmosphere { get; set; }

        public void RequestSimulation()
        {
            _simRequested = true;
            if (!_timer.IsRunning)
                _timer.Start();
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
            try
            {
                _simRunning = true;
                _timer.Start();

                List<Part> parts = HighLogic.LoadedSceneIsEditor ? EditorLogic.SortedShipList : FlightGlobals.ActiveVessel.Parts;

                // Create the Simulation object in this thread
                Simulation sim = new Simulation();

                if (sim.PrepareSimulation(parts, this.Gravity, this.Atmosphere))
                {
                    ThreadPool.QueueUserWorkItem(RunSimulation, sim);
                }
                else
                {
                    Stages = null;
                }
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Exception in StartSimulation: " + e);
                Stages = null;
                LastStage = null;
                failMessage = e.ToString();
                _simRunning = false;
            }
        }

        private void RunSimulation(object simObject)
        {
            try
            {
                Stages = (simObject as Simulation).RunSimulation();
#if LOG
                foreach (Stage stage in Stages)
                    stage.Dump();
#endif
                LastStage = Stages.Last();
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Exception in RunSimulation: " + e);
                Stages = null;
                LastStage = null;
                failMessage = e.ToString();
            }

            _timer.Stop();
            MonoBehaviour.print("RunSimulation took " + _timer.ElapsedMilliseconds + "ms");
            _millisecondsBetweenSimulations = 10 * _timer.ElapsedMilliseconds;

            _timer.Reset();
            _timer.Start();

            _simRunning = false;
        }
    }
}
