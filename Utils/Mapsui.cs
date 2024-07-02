using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hajk
{
    internal class MapsuiLogging
    {
        public static void AttachMapsuiLogging()
        {
            var mapsuiPrefix = "[Mapsui]";
            Mapsui.Logging.Logger.LogDelegate += (level, message, ex) => {
                if (level == Mapsui.Logging.LogLevel.Error)
                    Serilog.Log.Error(ex, $"{mapsuiPrefix} {message}");
                else if (level == Mapsui.Logging.LogLevel.Warning)
                    Serilog.Log.Warning(ex, $"{mapsuiPrefix} {message}");
                else if (level == Mapsui.Logging.LogLevel.Information)
                    Serilog.Log.Information(ex, $"{mapsuiPrefix} {message}");
                else if (level == Mapsui.Logging.LogLevel.Debug)
                    Serilog.Log.Debug(ex, $"{mapsuiPrefix} {message}");
                else if (level == Mapsui.Logging.LogLevel.Trace)
                    Serilog.Log.Verbose(ex, $"{mapsuiPrefix} {message}");
            };
        }
    }
}
