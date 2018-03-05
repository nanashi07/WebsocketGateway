using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;

namespace WebsocketGateway.Modules
{
    /// <summary>
    /// 訊息中介閘道
    /// </summary>
    public class GatewayHandler : IHttpHandler
    {
        public bool IsReusable => false;

        private int BufferSize => 1024;

        WebSocket serverSocket;
        ClientWebSocket clientSocket;

        public void ProcessRequest(HttpContext context)
        {
            if (context.IsWebSocketRequest)
            {
                context.AcceptWebSocketRequest(ProcessMessage);
            }
            else
            {
                // TODO: 
            }
        }

        /// <summary>
        /// 處理 web socket 中介轉發
        /// </summary>
        /// <param name="context">Websocket 上下文</param>
        /// <returns></returns>
        private async Task ProcessMessage(AspNetWebSocketContext context)
        {
            // 伺服端 channel
            serverSocket = context.WebSocket;

            // 建立 buffer
            ArraySegment<byte> serverBuffer = WebSocket.CreateServerBuffer(BufferSize);

            // 連接至遠端
            Task task = ProcessClient(context);

            MemoryStream serverContent = new MemoryStream();

            while (true)
            {
                // 監聽前端訊息
                WebSocketReceiveResult serverResult = await serverSocket.ReceiveAsync(serverBuffer, CancellationToken.None);

                if (serverSocket.State == WebSocketState.Open)
                {
                    if (serverResult.EndOfMessage)
                    {
                        // 完成接收前端傳入訊息
                        await serverContent.WriteAsync(serverBuffer.Array, 0, serverResult.Count);
                        serverContent.Close();

                        byte[] messageForTransport = serverContent.ToArray();
                        serverContent = new MemoryStream();

                        // 傳送至遠端
                        await clientSocket.SendAsync(new ArraySegment<byte>(messageForTransport), serverResult.MessageType, true, CancellationToken.None);
                    }
                    else
                    {
                        // 寫入前端傳入暫存訊息
                        await serverContent.WriteAsync(serverBuffer.Array, 0, serverResult.Count);
                    }
                }
                else
                {
                    // 釋放暫存訊息
                    serverContent.Close();

                    break;
                }
            }
        }

        private async Task ProcessClient(AspNetWebSocketContext context)
        {
            // 客戶端 channel
            using (clientSocket = new ClientWebSocket())
            {
                // 連線至遠端
                Uri remoteUri = new Uri($"ws://{context.RequestUri.Host}:{context.RequestUri.Port}/message");
                await clientSocket.ConnectAsync(remoteUri, CancellationToken.None);

                // 建立遠端 buffer
                ArraySegment<byte> clientBuffer = WebSocket.CreateServerBuffer(BufferSize);

                MemoryStream clientContent = new MemoryStream();

                while (serverSocket != null && serverSocket.State == WebSocketState.Open)
                {
                    // 接收遠端回應
                    WebSocketReceiveResult clientResult = await clientSocket.ReceiveAsync(clientBuffer, CancellationToken.None);

                    if (clientSocket.State == WebSocketState.Open)
                    {
                        if (clientResult.EndOfMessage)
                        {
                            // 完成接收遠端訊息
                            await clientContent.WriteAsync(clientBuffer.Array, 0, clientResult.Count);
                            clientContent.Close();

                            byte[] response = clientContent.ToArray();
                            clientContent = new MemoryStream();

                            // 回傳前端
                            await serverSocket.SendAsync(new ArraySegment<byte>(response), clientResult.MessageType, true, CancellationToken.None);
                        }
                        else
                        {
                            // 接收遠端暫存訊息
                            await clientContent.WriteAsync(clientBuffer.Array, 0, clientResult.Count);
                        }
                    }
                    else
                    {
                        // 釋放遠端暫存訊息
                        clientContent.Close();
                    }
                }
            }
        }
    }
}
