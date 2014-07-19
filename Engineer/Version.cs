// Kerbal Engineer Redux
// Author:  CYBUTEK
// License: Attribution-NonCommercial-ShareAlike 3.0 Unported

using System;
using System.Collections;
using UnityEngine;

namespace Engineer
{
    public class Version
    {
        public const string VERSION = "0.6.2.6";
        public const string SUFFIX = " (Pad)";
        public const string PRODUCT_NAME = "engineer_redux";
        private string remoteVersion = null;
        private bool hasCompared = false;
        private bool isNewer = false;

        public bool Same
        {
            get
            {
                if (Remote != Local)
                {
                    return true;
                }
                return false;
            }
        }

        public bool Older
        {
            get
            {
                if (!hasCompared)
                {
                    try
                    {
                        CompareVersions();
                    }
                    catch { }
                }
                if (!isNewer && !Same)
                {
                    return true;
                }
                return false;
            }
        }

        public bool Newer
        {
            get
            {
                if (!hasCompared)
                {
                    try
                    {
                        CompareVersions();
                    }
                    catch { }
                }
                if (isNewer)
                {
                    return true;
                }
                return false;
            }
        }

        public string Local
        {
            get
            {
                return VERSION;
            }
        }

        public string Remote
        {
            get
            {
                if (remoteVersion == null)
                {
                    try
                    {

                        remoteVersion = GetRemoteVersion();
                    }
                    catch { }
                }
                return remoteVersion;
            }
        }

        private void CompareVersions()
        {
            if (Remote != null && Remote.Length > 0)
            {
                hasCompared = true;
                return;
            }

            string[] local = Local.Split('.');
            string[] remote = Remote.Split('.');

            try
            {
                if (local.Length > remote.Length)
                {
                    Array.Resize<string>(ref remote, local.Length);

                    for (int i = 0; i < remote.Length; i++)
                    {
                        if (remote[i] == null)
                        {
                            remote[i] = "0";
                        }
                    }
                }
                else
                {
                    Array.Resize<string>(ref local, remote.Length);

                    for (int i = 0; i < local.Length; i++)
                    {
                        if (local[i] == null)
                        {
                            local[i] = "0";
                        }
                    }
                }

                for (int i = 0; i < local.Length; i++)
                {
                    if (Convert.ToInt32(local[i]) < Convert.ToInt32(remote[i]))
                    {
                        isNewer = true;
                        break;
                    }

                    if (Convert.ToInt32(local[i]) > Convert.ToInt32(remote[i]))
                    {
                        isNewer = false;
                        break;
                    }
                }
            }
            catch { }

            hasCompared = true;
        }

        private string GetRemoteVersion()
        {
            return VERSION; // Skip version checking as this system has not been used in a while

/*            try
            {
                WWW www = new WWW("http://www.cybutek.net/ksp/getversion.php?name=" + PRODUCT_NAME);
                while (!www.isDone) { }
                return www.text;
            }
            catch { }

            return "";
 */
        }
    }
}
