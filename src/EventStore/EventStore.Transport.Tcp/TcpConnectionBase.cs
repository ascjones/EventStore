// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using EventStore.Common.Utils;

namespace EventStore.Transport.Tcp
{
    public class TcpConnectionBase : IMonitoredTcpConnection
    {
        private Socket _socket;
        private IPEndPoint _endPoint;

        //TODO GFY THERE ARE SOME DATETIMES IN HERE THAT ARE NOT LOCKED. MIGHT WANT TO STORE AS LONGS THOUGH
        //I DONT THINK THEY ARE EVER CHECKED FROM ANOTHER THREAD!
        private long _lastSendStarted = -1;
        private long _lastReceiveStarted = -1;
        private bool _isClosed;

        private int _pendingSendBytes;
        private int _inSendBytes;
        private int _pendingReceivedBytes;
        private long _totaBytesSent;
        private long _totaBytesReceived;

        public TcpConnectionBase()
        {
            TcpConnectionMonitor.Default.Register(this);
        }

        public IPEndPoint EndPoint
        {
            get { return _endPoint; }
        }

        public bool IsReadyForSend
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectWrite);
                }
                catch (ObjectDisposedException)
                {
                    //TODO: why do we get this?
                    return false;
                }
            }
        }

        public bool IsReadyForReceive
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectRead);
                }
                catch (ObjectDisposedException)
                {
                    //TODO: why do we get this?
                    return false;
                }
            }
        }

        public bool IsInitialized
        {
            get { return _socket != null; }
        }

        public bool IsFaulted
        {
            get
            {
                try
                {
                    return !_isClosed && _socket.Poll(0, SelectMode.SelectError);
                }
                catch (ObjectDisposedException)
                {
                    //TODO: why do we get this?
                    return false;
                }
            }
        }

        public bool IsClosed
        {
            get { return _isClosed; }
        }

        public bool InSend
        {
            get { return Interlocked.Read(ref _lastSendStarted) >= 0; }
        }

        public bool InReceive
        {
            get { return Interlocked.Read(ref _lastReceiveStarted) >= 0; }
        }

        public DateTime? LastSendStarted
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastSendStarted);
                return ticks >= 0 ? new DateTime(ticks) : (DateTime?) null;
            }
        }

        public DateTime? LastReceiveStarted
        {
            get
            {
                var ticks = Interlocked.Read(ref _lastReceiveStarted);
                return ticks >= 0 ? new DateTime(ticks) : (DateTime?)null;
            }
        }

        public int PendingSendBytes
        {
            get { return _pendingSendBytes; }
        }

        public int InSendBytes
        {
            get { return _inSendBytes; }
        }

        public int PendingReceivedBytes
        {
            get { return _pendingReceivedBytes; }
        }

        public long TotalBytesSent
        {
            get
            {
                return Interlocked.Read(ref _totaBytesReceived);
            }
        }

        public long TotalBytesReceived
        {
            get
            {
                return Interlocked.Read(ref _totaBytesReceived);
            }
        }

        protected void InitSocket(Socket socket, IPEndPoint endPoint)
        {
            Ensure.NotNull(socket, "socket");
            Ensure.NotNull(endPoint, "endPoint");

            _socket = socket;
            _endPoint = endPoint;
        }

        protected void NotifySendScheduled(int bytes)
        {
            Interlocked.Add(ref _pendingSendBytes, bytes);
        }

        protected void NotifySendStarting(int bytes)
        {
            if (Interlocked.CompareExchange(ref _lastSendStarted, DateTime.UtcNow.Ticks, -1) != -1)
                throw new Exception("Concurrent send detected.");
            Interlocked.Add(ref _pendingSendBytes, -bytes);
            Interlocked.Add(ref _inSendBytes, bytes);
        }

        protected void NotifySendCompleted(int bytes)
        {
            Interlocked.Exchange(ref _lastSendStarted, -1);
            Interlocked.Add(ref _inSendBytes, -bytes);
            Interlocked.Add(ref _totaBytesSent, bytes);
        }

        protected void NotifyReceiveStarting()
        {
            if (Interlocked.CompareExchange(ref _lastReceiveStarted, DateTime.UtcNow.Ticks, -1) != -1)
                throw new Exception("Concurrent receive detected.");
        }

        protected void NotifyReceiveCompleted(int bytes)
        {
            Interlocked.Exchange(ref _lastReceiveStarted, -1);
            Interlocked.Add(ref _pendingReceivedBytes, bytes);
            Interlocked.Add(ref _totaBytesReceived, bytes);
        }

        protected void NotifyReceiveDispatched(int bytes)
        {
            Interlocked.Add(ref _pendingReceivedBytes, -bytes);
        }

        protected void NotifyClosed()
        {
            _isClosed = true;
            TcpConnectionMonitor.Default.Unregister(this);
        }
    }
}