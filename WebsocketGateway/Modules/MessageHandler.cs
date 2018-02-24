using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;

namespace WebsocketGateway.Modules
{
    /// <summary>
    /// 簡易 web socket
    /// </summary>
    public class MessageHandler : IHttpHandler
    {

        public bool IsReusable => true;

        public void ProcessRequest(HttpContext context)
        {
            if (context.IsWebSocketRequest)
            {
                context.AcceptWebSocketRequest(ProcessMessage);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }

        private async Task ProcessMessage(AspNetWebSocketContext context)
        {
            WebSocket socket = context.WebSocket;
            int bufferSize = 1024;

            ArraySegment<byte> buffer = WebSocket.CreateServerBuffer(bufferSize);
            MemoryStream content = new MemoryStream();

            while (true)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (socket.State == WebSocketState.Open)
                {
                    if (result.EndOfMessage)
                    {
                        await content.WriteAsync(buffer.Array, 0, result.Count);
                        content.Close();

                        string userMessage = Encoding.UTF8.GetString(content.ToArray());
                        content = new MemoryStream();

                        userMessage = $"{DateTime.Now:HH:mm:ss} [{userMessage.Length}] {userMessage}";
                        buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(userMessage));
                        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    else
                    {
                        await content.WriteAsync(buffer.Array, 0, result.Count);
                    }
                }
                else
                {
                    content.Close();
                    break;
                }
            }
        }

    }
}