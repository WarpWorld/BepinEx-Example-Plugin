using System;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace BepinControl
{
    public class TimedResponse : CrowdResponse
    {
        public int timeRemaining;

        public TimedResponse(int id, int dur, Status status = Status.STATUS_SUCCESS, string message = "") : base(id, status, message)
        {
            this.timeRemaining = dur;
        }
    }
}
