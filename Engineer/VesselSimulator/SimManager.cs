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
        private static bool bRequested = false;
        private static bool bRunning = false;
        private static Stopwatch timer = new Stopwatch();
        private static long delayBetweenSims = 0;

        private static Stopwatch _func = new Stopwatch();

        public static Stage[] Stages { get; private set; }
        public static Stage LastStage { get; private set; }
        public static String failMessage { get; private set; }

        public static double Gravity { get; set; }
        public static double Atmosphere { get; set; }

        public static void RequestSimulation()
        {
            bRequested = true;
            if (!timer.IsRunning)
                timer.Start();
        }

        public static void TryStartSimulation()
        {
            if (bRequested && !bRunning && (HighLogic.LoadedSceneIsEditor || FlightGlobals.ActiveVessel != null) && timer.ElapsedMilliseconds > delayBetweenSims)
            {
                bRequested = false;
                timer.Reset();
                StartSimulation();
            }
        }

        public static bool ResultsReady()
        {
            return !bRunning;
        }

        private static void ClearResults()
        {
            failMessage = "";
            Stages = null;
            LastStage = null;
        }

        private static void StartSimulation()
        {
            try
            {
                bRunning = true;
                ClearResults();
                timer.Start();

                List<Part> parts = HighLogic.LoadedSceneIsEditor ? EditorLogic.SortedShipList : FlightGlobals.ActiveVessel.Parts;

                // Create the Simulation object in this thread
                Simulation sim = new Simulation();

                // This call doesn't ever fail at the moment but we'll check and return a sensible error for display
                if (sim.PrepareSimulation(parts, Gravity, Atmosphere))
                {
                    ThreadPool.QueueUserWorkItem(RunSimulation, sim);
                }
                else
                {
                    failMessage = "PrepareSimulation failed";
                    bRunning = false;
                }
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Exception in StartSimulation: " + e);
                failMessage = e.ToString();
                bRunning = false;
            }
        }

        private static void RunSimulation(object simObject)
        {
            try
            {
                Stages = (simObject as Simulation).RunSimulation();
                if (Stages != null)
                {
#if LOG
                    foreach (Stage stage in Stages)
                        stage.Dump();
#endif
                    LastStage = Stages.Last();
                }
            }
            catch (Exception e)
            {
                MonoBehaviour.print("Exception in RunSimulation: " + e);
                Stages = null;
                LastStage = null;
                failMessage = e.ToString();
            }

            timer.Stop();
            MonoBehaviour.print("RunSimulation took " + timer.ElapsedMilliseconds + "ms");
            delayBetweenSims = 10 * timer.ElapsedMilliseconds;

            timer.Reset();
            timer.Start();

            bRunning = false;
        }
    }
}
