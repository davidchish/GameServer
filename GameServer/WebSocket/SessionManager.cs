using GameServer.WebSocketing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameServer.WebSocketing
{
    public sealed class SessionManager
    {
        private readonly ConcurrentDictionary<Guid, Session> _sessions = new();

        public void Register(Session session)
        {
            _sessions[session.SessionId] = session;
        }

        public bool TryGetSession(Guid sessionId, out Session? session)
        {
            return _sessions.TryGetValue(sessionId, out session);
        }

        public void Unregister(Guid sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }
}
