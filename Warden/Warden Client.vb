﻿Namespace Warden
    Public NotInheritable Class Client
        Inherits DisposableWithTask

        Public Event ReceivedWardenData(sender As Warden.Client, wardenData As IRist(Of Byte))

        Public ReadOnly Property FutureDisconnected As Task
            Get
                Return Async Function()
                           Dim s = Await _socket
                           Await s.FutureDisconnected
                       End Function()
            End Get
        End Property
        Public ReadOnly Property FutureFailed As Task
            Get
                Return Async Function()
                           Dim s = Await _socket
                           Await s.FutureFail
                       End Function()
            End Get
        End Property
        Private ReadOnly _socket As Task(Of Warden.Socket)
        Private ReadOnly _activated As New TaskCompletionSource(Of NoValue)()
        Private ReadOnly _logger As Logger

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_activated IsNot Nothing)
            Contract.Invariant(_socket IsNot Nothing)
            Contract.Invariant(_logger IsNot Nothing)
        End Sub

        Public Sub New(socket As Task(Of Warden.Socket),
                       activated As TaskCompletionSource(Of NoValue),
                       logger As Logger)
            Contract.Requires(socket IsNot Nothing)
            Contract.Requires(activated IsNot Nothing)
            Contract.Requires(logger IsNot Nothing)
            Me._socket = socket
            Me._activated = activated
            Me._logger = logger
        End Sub
        Public Shared Function MakeMock(logger As Logger) As Warden.Client
            Contract.Requires(logger IsNot Nothing)
            Contract.Ensures(Contract.Result(Of Warden.Client)() IsNot Nothing)
            Dim failedSocket = New TaskCompletionSource(Of Warden.Socket)
            Dim activated = New TaskCompletionSource(Of NoValue)()
            failedSocket.Task.ConsiderExceptionsHandled()
            failedSocket.SetException(New ArgumentException("No remote host specified for bnls server."))
            Contract.Assume(activated.Task IsNot Nothing)
            Contract.Assume(failedSocket.Task IsNot Nothing)
            Call Async Sub()
                     Await activated.Task
                     logger.Log("Warning: No BNLS server set, but received a Warden packet.", LogMessageType.Problem)
                 End Sub
            Return New Warden.Client(failedSocket.Task, activated, logger)
        End Function
        Public Shared Function MakeConnect(remoteHost As InvariantString,
                                           remotePort As UInt16,
                                           seed As UInt32,
                                           cookie As UInt32,
                                           clock As IClock,
                                           logger As Logger) As Warden.Client
            Contract.Requires(clock IsNot Nothing)
            Contract.Requires(logger IsNot Nothing)
            Contract.Ensures(Contract.Result(Of Warden.Client)() IsNot Nothing)

            logger.Log("Connecting to bnls server at {0}:{1}...".Frmt(remoteHost, remotePort), LogMessageType.Positive)

            'Initiate connection
            Dim s = Async Function()
                        Try
                            Dim tcpClient = Await AsyncTcpConnect(remoteHost, remotePort)
                            Dim packetSocket = New PacketSocket(stream:=tcpClient.GetStream,
                                                                localendpoint:=DirectCast(tcpClient.Client.LocalEndPoint, Net.IPEndPoint),
                                                                remoteendpoint:=DirectCast(tcpClient.Client.RemoteEndPoint, Net.IPEndPoint),
                                                                timeout:=5.Minutes,
                                                                preheaderLength:=0,
                                                                sizeHeaderLength:=2,
                                                                logger:=logger,
                                                                Name:="BNLS",
                                                                clock:=clock)
                            Return New Warden.Socket(Socket:=packetSocket,
                                                     seed:=seed,
                                                     cookie:=cookie,
                                                     logger:=logger)
                        Catch ex As Exception
                            logger.Log("Error connecting to bnls server at {0}:{1}: {2}".Frmt(remoteHost, remotePort, ex.Summarize), LogMessageType.Problem)
                            ex.RaiseAsUnexpected("Connecting to bnls server.")
                            Throw
                        End Try
                    End Function()

            Contract.Assume(s IsNot Nothing)
            Dim result = New Warden.Client(s, New TaskCompletionSource(Of NoValue), logger)
            result.Start()
            Return result
        End Function
        Private Async Sub Start()
            'Wire events
            Dim wardenClient As Socket
            Try
                wardenClient = Await _socket
            Catch ex As Exception
                'socket creation exceptions are handled elsewhere
                Return
            End Try

            _logger.Log("Connected to bnls server.", LogMessageType.Positive)

            Dim receiveForward As Warden.Socket.ReceivedWardenDataEventHandler =
                    Sub(sender, wardenData) RaiseEvent ReceivedWardenData(Me, wardenData)
            AddHandler wardenClient.ReceivedWardenData, receiveForward
            Await wardenClient.DisposalTask
            RemoveHandler wardenClient.ReceivedWardenData, receiveForward
        End Sub

        Public ReadOnly Property Activated As Task
            Get
                Contract.Ensures(Contract.Result(Of Task)() IsNot Nothing)
                Return _activated.Task.AssumeNotNull
            End Get
        End Property

        Public Async Function QueueSendWardenData(wardenData As IRist(Of Byte)) As Task
            Contract.Assume(wardenData IsNot Nothing)
            'Contract.Ensures(Contract.Result(Of Task)() IsNot Nothing)
            _activated.TrySetResult(Nothing)
            Dim wardenClient = Await _socket
            Await wardenClient.QueueSendWardenData(wardenData)
        End Function

        Protected Overrides Function PerformDispose(finalizing As Boolean) As Task
            If finalizing Then Return Nothing
            Return _socket.DisposeAsync()
        End Function
    End Class
End Namespace
