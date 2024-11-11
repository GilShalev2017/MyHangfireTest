using HangfireTest.Models;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace HangfireTest.Services
{
    public static class WebSocketHandler
    {
        private static readonly ConcurrentDictionary<string, WebSocket> WebSocketConnections = new ConcurrentDictionary<string, WebSocket>();

        public static async Task HandleWebSocketConnection(WebSocket webSocket)
        {
            var connectionId = Guid.NewGuid().ToString();
            WebSocketConnections.TryAdd(connectionId, webSocket);

            try
            {
                var buffer = new byte[1024 * 4];
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        WebSocketConnections.TryRemove(connectionId, out _);
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
                    }
                }
            }
            catch (Exception)
            {
                WebSocketConnections.TryRemove(connectionId, out _);
            }
        }

        public static async Task SendNotificationAsync(string message)//NotificationMessage notification)
        {
            //var message = JsonSerializer.Serialize(notifications);
            var messageBuffer = Encoding.UTF8.GetBytes(message);

            foreach (var socket in WebSocketConnections.Values)
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(messageBuffer),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            }
        }
    }
}
