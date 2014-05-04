using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Engineer.Extensions
{
    public static class PartExtensions
    {
        /// <summary>
        /// Gets whether the part contains a PartModule.
        /// </summary>
        public static bool HasModule<T>(this Part part)
        {
            return part.Modules.OfType<T>().Count() > 0;
        }

        /// <summary>
        /// Gets whether the part contains a PartModule.
        /// </summary>
        public static bool HasModule(this Part part, string className)
        {
            return part.Modules.Contains(className);
        }

        /// <summary>
        /// Gets the all the PartModules of the specified type.
        /// </summary>
        public static IEnumerable<T> GetModules<T>(this Part part) where T : PartModule
        {
            return part.Modules.OfType<T>();
        }

        /// <summary>
        /// Gets the first typed PartModule.
        /// </summary>
        public static T GetModule<T>(this Part part) where T : PartModule
        {
            foreach (PartModule module in part.Modules)
            {
                if (module is T)
                {
                    return module as T;
                }
            }

            return null;
        }
    }
}
