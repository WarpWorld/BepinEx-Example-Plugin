using System;
using System.Net.Sockets;
using System.Text;
using LethalCompanyTestMod;
using Newtonsoft.Json;

namespace BepinControl
{
    public class CrowdResponse
    {
        public enum Status
        {
            STATUS_SUCCESS,
            STATUS_FAILURE,
            STATUS_UNAVAIL,
            STATUS_RETRY,
            STATUS_START = 5,
            STATUS_PAUSE = 6,
            STATUS_RESUME = 7,
            STATUS_STOP = 8,

            STATUS_VISIBLE = 0x80,
            STATUS_NOTVISIBLE = 0x81,
            STATUS_SELECTABLE = 0x82,
            STATUS_NOTSELECTABLE = 0x83,

            STATUS_KEEPALIVE = 255
        }

        public int id;
        public string message;
        public string code;
        public int status;
        public int type;

        public CrowdResponse(int id, Status status = Status.STATUS_SUCCESS, string message = "")
        {
            this.type = 0;
            code = "";
            this.id = id;
            this.message = message;
            this.status = (int)status;
        }

        public static void KeepAlive(Socket socket)
        {
            new CrowdResponse(0, Status.STATUS_KEEPALIVE).Send(socket);
        }

        public void Send(Socket socket)
        {
            byte[] tmpData = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(this));
            byte[] outData = new byte[tmpData.Length + 1];
            Buffer.BlockCopy(tmpData, 0, outData, 0, tmpData.Length);
            outData[tmpData.Length] = 0;
            socket.Send(outData);
        }
    }
}
