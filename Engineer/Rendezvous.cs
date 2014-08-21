﻿// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Engineer
{
    public class Rendezvous
    {
        enum TargetType { None, Vessel, Celestial };
        FlightEngineer fe;
        object targetObject;
        Vector2 scrollPosition = Vector2.zero;
        TargetType typeOfTarget = TargetType.None;
        VesselType typeOfVessel = VesselType.Unknown;

        public FlightEngineer FlightEngineer
        {
            get { return fe; }
            set { fe = value; }
        }

        public void Draw()
        {
            if (typeOfTarget == TargetType.None)
            {
                DrawTargetTypes();
            }
            else
            {
                if (typeOfTarget == TargetType.Vessel)
                {
                    if (targetObject == null)
                    {
                        DrawVessels();
                    }
                    else
                    {
                        DrawDetails();
                    }
                }
                else if (typeOfTarget == TargetType.Celestial)
                {
                    if (targetObject == null)
                    {
                        DrawCelestialBodies();
                    }
                    else
                    {
                        DrawDetails();
                    }
                }
            }
        }

        private void DrawTargetTypes()
        {
            bool displaying = false;

            GUILayout.Label("RENDEZVOUS DISPLAY", fe.headingStyle);

            GUILayout.BeginHorizontal(fe.areaStyle);
            GUILayout.BeginVertical();

            List<VesselType> vesselTypes = new List<VesselType>();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                if (!vesselTypes.Contains(vessel.vesselType) && vessel.vesselType != VesselType.Unknown && vessel != fe.vessel && vessel.mainBody == fe.vessel.mainBody)
                {
                    displaying = true;
                    vesselTypes.Add(vessel.vesselType);
                    if (GUILayout.Button(vessel.vesselType.ToString(), fe.buttonStyle))
                    {
                        typeOfTarget = TargetType.Vessel;
                        typeOfVessel = vessel.vesselType;
                        FlightEngineer.settings.Changed = true;
                    }
                }
            }

            if (GUILayout.Button("Celestial Body", fe.buttonStyle) || !displaying)
            {
                typeOfTarget = TargetType.Celestial;
                FlightEngineer.settings.Changed = true;
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawVessels()
        {
            bool displaying = false;

            GUILayout.Label("RENDEZVOUS DISPLAY", fe.headingStyle);

            GUILayout.BeginHorizontal(fe.areaStyle);
            GUILayout.BeginVertical();
            foreach (Vessel vessel in FlightGlobals.Vessels)
            {
                if (vessel.vesselType == typeOfVessel && vessel != fe.vessel)
                {
                    if (vessel.mainBody == fe.vessel.mainBody)
                    {
                        displaying = true;
                        if (GUILayout.Button(vessel.vesselName, fe.buttonStyle))
                        {
                            targetObject = vessel;
                            FlightEngineer.settings.Changed = true;
                        }
                    }
                }
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Back to Target Type", fe.buttonStyle) || !displaying)
            {
                targetObject = null;
                typeOfTarget = TargetType.None;
                typeOfVessel = VesselType.Unknown;
                FlightEngineer.settings.Changed = true;
            }
        }

        private void DrawCelestialBodies()
        {
            bool displaying = false;

            GUILayout.BeginHorizontal(fe.areaStyle);
            GUILayout.BeginVertical();
            foreach (CelestialBody body in fe.vessel.mainBody.orbitingBodies)
            {
                displaying = true;
                if (GUILayout.Button(body.name, fe.buttonStyle))
                {
                    targetObject = body;
                    FlightEngineer.settings.Changed = true;
                    scrollPosition = Vector2.zero;
                    return;
                }
            }

            foreach (CelestialBody body in fe.vessel.mainBody.referenceBody.orbitingBodies)
            {
                displaying = true;
                if (body == fe.vessel.mainBody)
                {
                    continue;
                }
                if (GUILayout.Button(body.name, fe.buttonStyle))
                {
                    targetObject = body;
                    FlightEngineer.settings.Changed = true;
                    scrollPosition = Vector2.zero;
                    return;
                }
            }

            if (!displaying)
            {
                GUILayout.Label("No selectable targets!", fe.headingStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Back to Target Type", fe.buttonStyle))
            {
                targetObject = null;
                typeOfTarget = TargetType.None;
                typeOfVessel = VesselType.Unknown;
                FlightEngineer.settings.Changed = true;
            }
        }

        private void DrawDetails()
        {
            if (typeOfTarget == TargetType.Vessel)
            {
                DrawVesselDetails(targetObject as Vessel);
            }
            else if (typeOfTarget == TargetType.Celestial)
            {
                DrawBodyDetails(targetObject as CelestialBody);
            }

            if (GUILayout.Button("Back to Targets", fe.buttonStyle))
            {
                targetObject = null;
                FlightEngineer.settings.Changed = true;
                FlightGlobals.fetch.SetVesselTarget(null);
            }
        }

        private void DrawBodyDetails(CelestialBody targetBody)
        {
            if (fe.vessel.mainBody == targetBody)
            {
                targetObject = null;
                FlightEngineer.settings.Changed = true;
                return;
            }

            if (FlightGlobals.fetch.VesselTarget == null)
            {
                FlightGlobals.fetch.SetVesselTarget(targetBody);
            }

            Orbit activeOrbit;
            Orbit targetOrbit = targetBody.orbit;

            Vector3d activePosition = Vector3d.zero;
            Vector3d targetPosition = targetOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            double altitude = targetOrbit.altitude;
            double phaseAngle = 0.0d;
            double interceptAngle = 0.0d;

            if (fe.vessel.mainBody == Planetarium.fetch.Sun || fe.vessel.mainBody == targetBody.referenceBody)
            {
                activeOrbit = fe.vessel.orbit;
                activePosition = activeOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
                interceptAngle = CalcInterceptAngle(activeOrbit.radius, targetOrbit.radius);
            }
            else
            {
                activeOrbit = fe.vessel.mainBody.orbit;
                activePosition = activeOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
                interceptAngle = CalcInterceptAngle(activeOrbit.radius, targetOrbit.radius);
            }

            double distance = Vector3d.Distance(targetPosition, activePosition);

            double angleToPrograde = 0d;
            double angleToRetrograde = 0d;
            double ejectionAngle = 0d;
            if (fe.vessel.mainBody != targetBody.referenceBody && fe.vessel.mainBody != Planetarium.fetch.Sun)
            {
                angleToPrograde = AngleToPrograde(fe.vessel);
                angleToRetrograde = AngleToRetrograde(fe.vessel);
                ejectionAngle = CalcEjectionAngle(fe.vessel, targetBody);
            }

            double relInclination = CalcRelativeInclination(activeOrbit, targetOrbit);
            double ascendingNode = CalcAngleToAscendingNode(activePosition, activeOrbit, targetOrbit);
            double descendingNode = CalcAngleToDescendingNode(activePosition, activeOrbit, targetOrbit);
            double timeToAN = CalcTimeToNode(activeOrbit, ascendingNode);
            double timeToDN = CalcTimeToNode(activeOrbit, descendingNode);

            if (interceptAngle < 0)
            {
                phaseAngle = CalcPhaseAngle(activePosition, targetPosition) - 360;
                interceptAngle = (phaseAngle - interceptAngle) + 360;
            }
            else
            {
                phaseAngle = CalcPhaseAngle(activePosition, targetPosition);
                interceptAngle = phaseAngle - interceptAngle;
            }

            if (interceptAngle < 0)
            {
                interceptAngle += 360;
            }

            GUILayout.Label(targetBody.name, fe.headingStyle);

            GUILayout.BeginHorizontal(fe.areaStyle);
            GUILayout.BeginVertical();
            FlightEngineer.settings.Set("*SPACER_RENDEZVOUS_CELESTIAL", "");
            FlightEngineer.settings.Set("*headingStyle_RENDEZVOUS_CELESTIAL", "RENDEZVOUS DISPLAY - CELESTIAL BODY");
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Current Phase Angle", true)) GUILayout.Label("Current Phase Angle", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Intercept Angle", true)) GUILayout.Label("Intercept Angle", fe.headingStyle);
            
            if (fe.vessel.mainBody != targetBody.referenceBody && fe.vessel.mainBody != Planetarium.fetch.Sun)
            {
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Ejection Angle", true)) GUILayout.Label("Ejection Angle", fe.headingStyle);
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Angle to Prograde/Retrograde", true))
                {
                    if (phaseAngle > 0)
                    {
                        GUILayout.Label("Angle to Prograde", fe.headingStyle);
                    }
                    else
                    {
                        GUILayout.Label("Angle to Retrograde", fe.headingStyle);
                    }
                }
            }

            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Relative Inclination", true)) GUILayout.Label("Relative Inclination", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Ascending Node", true)) GUILayout.Label("Ascending Node", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Descending Node", true)) GUILayout.Label("Descending Node", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Time to Ascending Node", true)) GUILayout.Label("Time to AN", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Time to Ascending Node", true)) GUILayout.Label("Time to DN", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Altitude", true)) GUILayout.Label("Altitude", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Distance", true)) GUILayout.Label("Distance", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Orbital Period", true)) GUILayout.Label("Orbital Period", fe.headingStyle);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Current Phase Angle")) GUILayout.Label(Tools.FormatNumber(phaseAngle, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Intercept Angle")) GUILayout.Label(Tools.FormatNumber(interceptAngle, "°", 6), fe.dataStyle);
            
            if (fe.vessel.mainBody != targetBody.referenceBody && fe.vessel.mainBody != Planetarium.fetch.Sun)
            {
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Ejection Angle")) GUILayout.Label(Tools.FormatNumber(ejectionAngle, "°", 6), fe.dataStyle);

                if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Angle to Prograde/Retrograde", true))
                {
                    if (phaseAngle > 0)
                    {
                        GUILayout.Label(Tools.FormatNumber(angleToPrograde, "°", 6), fe.dataStyle);
                    }
                    else
                    {
                        GUILayout.Label(Tools.FormatNumber(angleToRetrograde, "°", 6), fe.dataStyle);
                    }
               
                }
            }

            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Relative Inclination")) GUILayout.Label(Tools.FormatNumber(relInclination, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Ascending Node", true)) GUILayout.Label(Tools.FormatNumber(ascendingNode, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Descending Node", true)) GUILayout.Label(Tools.FormatNumber(descendingNode, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Time to Ascending Node", true)) GUILayout.Label(Tools.FormatTime(timeToAN), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Time to Ascending Node", true)) GUILayout.Label(Tools.FormatTime(timeToDN), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Altitude")) GUILayout.Label(Tools.FormatSI(altitude, Tools.SIUnitType.Distance), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Distance")) GUILayout.Label(Tools.FormatSI(distance, Tools.SIUnitType.Distance), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Celestial Body - Orbital Period")) GUILayout.Label(Tools.FormatTime(targetBody.orbit.period), fe.dataStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawVesselDetails(Vessel targetVessel)
        {
            if (targetVessel.mainBody != fe.vessel.mainBody)
            {
                targetObject = null;
                typeOfTarget = TargetType.None;
                typeOfVessel = VesselType.Unknown;
                FlightEngineer.settings.Changed = true;
            }

            if (FlightGlobals.fetch.VesselTarget == null)
            {
                FlightGlobals.fetch.SetVesselTarget(targetVessel);
            }
            Orbit activeOrbit = fe.vessel.orbit;
            Orbit targetOrbit = targetVessel.orbit;

            Vector3d activePosition = activeOrbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            Vector3d targetPosition = targetVessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            Vector3d activeVelocity = Vector3d.zero;
            Vector3d targetVelocity = Vector3d.zero;
            Vector3d relVelocity = FlightGlobals.ship_tgtVelocity;
            double phaseAngle = CalcPhaseAngle(activePosition, targetPosition);
            double interceptAngle = CalcInterceptAngle(activeOrbit.radius, targetOrbit.radius);
            double distance = Vector3d.Distance(targetPosition, activePosition);
            double interceptDistance = CalcInterceptDistance(activeOrbit.radius, targetOrbit.radius, interceptAngle);
            double relInclination = CalcRelativeInclination(activeOrbit, targetVessel.orbit);
            double ascendingNode = CalcAngleToAscendingNode(activePosition, activeOrbit, targetOrbit);
            double descendingNode = CalcAngleToDescendingNode(activePosition, activeOrbit, targetOrbit);
            double timeToAN = CalcTimeToNode(activeOrbit, ascendingNode);
            double timeToDN = CalcTimeToNode(activeOrbit, descendingNode);
            double altitude = targetOrbit.altitude;
            double velocity = targetVessel.obt_velocity.magnitude;
            double relVelocityMagnitude = FlightGlobals.ship_tgtVelocity.magnitude;

            if (interceptAngle < 0)
            {
                phaseAngle -= 360;
                interceptAngle = (phaseAngle - interceptAngle) + 360;
            }
            else
            {
                interceptAngle = phaseAngle - interceptAngle;
            }

            if (interceptAngle < 0)
            {
                interceptAngle += 360;
            }

            GUILayout.Label(targetVessel.vesselName, fe.headingStyle);

            GUILayout.BeginHorizontal(fe.areaStyle);
            GUILayout.BeginVertical();
            FlightEngineer.settings.Set("*SPACER_RENDEZVOUS_VESSEL", "");
            FlightEngineer.settings.Set("*headingStyle_RENDEZVOUS_VESSEL", "RENDEZVOUS DISPLAY - VESSEL");
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Phase Angle", true)) GUILayout.Label("Phase Angle", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Intercept Angle", true)) GUILayout.Label("Intercept Angle", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Intercept Distance", true)) GUILayout.Label("Intercept Distance", fe.headingStyle);

            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Inclination", true)) GUILayout.Label("Rel. Inclination", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Latitude", false)) GUILayout.Label("Rel. Latitude", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Ascending Node", true)) GUILayout.Label("Ascending Node", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Descending Node", true)) GUILayout.Label("Descending Node", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Time to Ascending Node", true)) GUILayout.Label("Time to AN", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Time to Ascending Node", true)) GUILayout.Label("Time to DN", fe.headingStyle);

            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Current Altitude", true)) GUILayout.Label("Current Altitude", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Apoapsis Height", true)) GUILayout.Label("Apoapsis Height", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Periapsis Height", true)) GUILayout.Label("Periapsis Height", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Orbital Period", true)) GUILayout.Label("Orbital Period", fe.headingStyle);

            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Distance", true)) GUILayout.Label("Distance", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Velocity", true)) GUILayout.Label("Velocity", fe.headingStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity", true)) GUILayout.Label("Rel. Velocity", fe.headingStyle);
            if (relVelocity != Vector3d.zero)
            {
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity (Vertical)", true)) GUILayout.Label(" > Vertical", fe.headingStyle);
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity (Horizontal)", true)) GUILayout.Label(" > Horizontal", fe.headingStyle);
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity (Forward)", true)) GUILayout.Label(" > Forward", fe.headingStyle);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Phase Angle")) GUILayout.Label(Tools.FormatNumber(phaseAngle, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Intercept Angle")) GUILayout.Label(Tools.FormatNumber(interceptAngle, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Intercept Distance")) GUILayout.Label(Tools.FormatSI(distance - interceptDistance, Tools.SIUnitType.Distance), fe.dataStyle);

            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Inclination")) GUILayout.Label(Tools.FormatNumber(relInclination, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Latitude")) GUILayout.Label(Tools.FormatNumber(fe.vessel.latitude - targetVessel.latitude, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Ascending Node", true)) GUILayout.Label(Tools.FormatNumber(ascendingNode, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Descending Node", true)) GUILayout.Label(Tools.FormatNumber(descendingNode, "°", 6), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Time to Ascending Node", true)) GUILayout.Label(Tools.FormatTime(timeToAN), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Time to Ascending Node", true)) GUILayout.Label(Tools.FormatTime(timeToDN), fe.dataStyle);
            
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Current Altitude")) GUILayout.Label(Tools.FormatSI(altitude, Tools.SIUnitType.Distance), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Apoapsis Height")) GUILayout.Label(Tools.FormatSI(targetOrbit.ApA, Tools.SIUnitType.Distance), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Periapsis Height")) GUILayout.Label(Tools.FormatSI(targetOrbit.PeA, Tools.SIUnitType.Distance), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Orbital Period")) GUILayout.Label(Tools.FormatTime(targetOrbit.period), fe.dataStyle);

            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Distance")) GUILayout.Label(Tools.FormatSI(distance, Tools.SIUnitType.Distance), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Velocity")) GUILayout.Label(Tools.FormatSI(velocity, Tools.SIUnitType.Speed), fe.dataStyle);
            if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity")) GUILayout.Label(Tools.FormatSI(relVelocityMagnitude, Tools.SIUnitType.Speed), fe.dataStyle);
            if (relVelocity != Vector3d.zero)
            {
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity (Vertical)")) GUILayout.Label(Tools.FormatSI(relVelocity.x, Tools.SIUnitType.Speed), fe.dataStyle);
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity (Horizontal)")) GUILayout.Label(Tools.FormatSI(relVelocity.y, Tools.SIUnitType.Speed), fe.dataStyle);
                if (FlightEngineer.settings.Get<bool>("Rendezvous: Vessel - Rel. Velocity (Forward)")) GUILayout.Label(Tools.FormatSI(relVelocity.z, Tools.SIUnitType.Speed), fe.dataStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private double CalcMeanAltitude(CelestialBody body)
        {
            return body.orbit.semiMajorAxis * (1 + body.orbit.eccentricity * body.orbit.eccentricity / 2);
        }

        private double CalcMeanAltitude(Vessel vessel)
        {
            return vessel.mainBody.orbit.semiMajorAxis * (1 + vessel.mainBody.orbit.eccentricity * vessel.mainBody.orbit.eccentricity / 2);
        }

        private double CalcPhaseAngle(Vector3d origin, Vector3d target)
        {
            double phaseAngle = Vector3d.Angle(target, origin);
            if (Vector3d.Angle(Quaternion.AngleAxis(90, Vector3d.forward) * origin, target) > 90)
            {
                phaseAngle = 360 - phaseAngle;
            }
            return (phaseAngle + 360) % 360;
        }

        private double CalcEjectionAngle(Vessel vessel, CelestialBody body)
        {
            double originAltitude = CalcMeanAltitude(vessel.mainBody);
            double destinationAltitude = CalcMeanAltitude(body);
            double originSOI = vessel.mainBody.sphereOfInfluence;
            double originRadius = vessel.mainBody.Radius;
            double originGravity = vessel.mainBody.gravParameter;
            double targetGravity = vessel.mainBody.referenceBody.gravParameter;
            double soiExitAltitude = originAltitude + originSOI;
            double v2 = Math.Sqrt(targetGravity / soiExitAltitude) * (Math.Sqrt((2 * destinationAltitude) / (soiExitAltitude + destinationAltitude)) - 1);
            double r = originRadius + (fe.vessel.mainBody.GetAltitude(vessel.findWorldCenterOfMass()));
            double v = Math.Sqrt((r * (originSOI * v2 * v2 - 2 * originGravity) + 2 * originSOI * originGravity) / (r * originSOI));
            double eta = Math.Abs(v * v / 2 - originGravity / r);
            double h = r * v;
            double e = Math.Sqrt(1 + ((2 * eta * h * h) / (originGravity * originGravity)));

            return 180 - (Math.Acos(1 / e) * (180 / Math.PI));
        }

        private double CalcRelativeInclination(Orbit origin, Orbit target)
        {
            return Vector3d.Angle(origin.GetOrbitNormal(), target.GetOrbitNormal());
        }

        private double CalcInterceptAngle(double originRadius, double targetRadius)
        {
            return 180 * (1 - Math.Pow((originRadius + targetRadius) / (2 * targetRadius), 1.5d));
        }

        private double CalcInterceptDistance(double originRadius, double targetRadius, double interceptAngle)
        {
            double radian = Math.PI / 180;
            return Math.Sqrt(Math.Pow(targetRadius * Math.Cos(interceptAngle * radian) - originRadius, 2) + Math.Pow(targetRadius * Math.Sin(interceptAngle * radian), 2));
        }

        private Vector3d CalcRelativeVelocity(Vector3d origin, Vector3d target)
        {
            return Vector3d.Project(origin, target.normalized) - target;
        }

        private double NormaliseAngle(Vector3d vector1, Vector3d vector2)
        {
            vector1 = Vector3d.Project(new Vector3d(vector1.x, 0, vector1.z), vector1);
            vector2 = Vector3d.Project(new Vector3d(vector2.x, 0, vector2.z), vector2);
            return Vector3d.Angle(vector1, vector2);
        }

        private double AngleToPrograde(Vessel vessel)
        {
            Vector3d vesselPosition = vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            Vector3d bodyPosition = vessel.mainBody.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());

            double angleToPrograde = NormaliseAngle(vesselPosition, Quaternion.AngleAxis(90, Vector3d.forward) * bodyPosition);

            if (NormaliseAngle(vesselPosition, Quaternion.AngleAxis(180, Vector3d.forward) * bodyPosition) > NormaliseAngle(vesselPosition, bodyPosition))
            {
                angleToPrograde = 360 - angleToPrograde;
            }

            return 360 - angleToPrograde;
        }

        private double AngleToRetrograde(Vessel vessel)
        {
            Vector3d vesselPosition = vessel.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());
            Vector3d bodyPosition = vessel.mainBody.orbit.getRelativePositionAtUT(Planetarium.GetUniversalTime());

            double angleToPrograde = NormaliseAngle(vesselPosition, Quaternion.AngleAxis(90, Vector3d.back) * bodyPosition);

            if (NormaliseAngle(vesselPosition, Quaternion.AngleAxis(180, Vector3d.back) * bodyPosition) > NormaliseAngle(vesselPosition, bodyPosition))
            {
                angleToPrograde = 360 - angleToPrograde;
            }

            return 360 - angleToPrograde;
        }

        private Vector3d GetAscendingNode(Orbit origin, Orbit target)
        {
            return Vector3d.Cross(target.GetOrbitNormal(), origin.GetOrbitNormal());
        }

        private Vector3d GetDescendingNode(Orbit origin, Orbit target)
        {
            return Vector3d.Cross(origin.GetOrbitNormal(), target.GetOrbitNormal());
        }

        private double CalcAngleToAscendingNode(Vector3d position, Orbit origin, Orbit target)
        {
            double angleToNode = 0d;

            if (origin.inclination < 90)
            {
                angleToNode = CalcPhaseAngle(position, GetAscendingNode(origin, target));
            }
            else
            {
                angleToNode = 360 - CalcPhaseAngle(position, GetAscendingNode(origin, target));
            }

            return angleToNode;
        }

        private double CalcAngleToDescendingNode(Vector3d position, Orbit origin, Orbit target)
        {
            double angleToNode = 0d;

            if (origin.inclination < 90)
            {
                angleToNode = CalcPhaseAngle(position, GetDescendingNode(origin, target));
            }
            else
            {
                angleToNode = 360 - CalcPhaseAngle(position, GetDescendingNode(origin, target));
            }

            return angleToNode;
        }

        private double CalcTimeToNode(Orbit origin, double angleToNode)
        {
            return (origin.period / 360d) * angleToNode;
        }
    }
}
