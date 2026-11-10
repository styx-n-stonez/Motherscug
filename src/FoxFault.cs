using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MotherMod
{
    internal static class FoxFault
    {
        internal static void Warn(string msg)
        {
            if (Plugin.LogSource != null) Plugin.LogSource.LogWarning("[Mother][Fox] " + msg);
        }
    }
}
