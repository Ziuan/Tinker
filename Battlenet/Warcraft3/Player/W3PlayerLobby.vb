﻿Namespace Warcraft3
    Partial Public NotInheritable Class W3Player
        Private knowMapState As Boolean
        Private mapDownloadPosition As Integer = -1
        Public IsGettingMapFromBot As Boolean
        Private mapUploadPosition As Integer
        Private countdowns As Integer
        Private Const MAX_BUFFERED_MAP_SIZE As UInteger = 64000
        Private ReadOnly handlers(0 To 255) As Action(Of W3Packet)

        Private Sub LobbyStart()
            state = W3PlayerState.Lobby
            handlers(W3PacketId.ClientMapInfo) = AddressOf ReceiveClientMapInfo
            handlers(W3PacketId.PeerConnectionInfo) = AddressOf ReceivePeerConnectionInfo
        End Sub
        Private Sub LobbyStop()
            handlers(W3PacketId.ClientMapInfo) = Nothing
        End Sub

#Region "Networking"
        Private Sub ReceivePeerConnectionInfo(ByVal packet As W3Packet)
            Contract.Requires(packet IsNot Nothing)
            Dim vals = CType(packet.payload.Value, Dictionary(Of String, Object))
            Dim dword = CUInt(vals("player bitflags"))
            Dim flags = From i In enumerable.Range(0, 12)
                        Select connected = ((dword >> i) And &H1) <> 0,
                               pid = CByte(i + 1)
            numPeerConnections = (From flag In flags Where flag.connected).Count
            Contract.Assume(numPeerConnections >= 0)
            Contract.Assume(numPeerConnections <= 12)

            If state = W3PlayerState.Lobby Then
                For Each flag In flags
                    game.DownloadScheduler.SetLink(Me.index, flag.pid, flag.connected).MarkAnyExceptionAsHandled()
                Next flag
            End If
            game.QueueThrowUpdated()
        End Sub
        Private Sub ReceiveClientMapInfo(ByVal packet As W3Packet)
            Contract.Requires(packet IsNot Nothing)
            Dim vals = CType(packet.payload.Value, Dictionary(Of String, Object))
            Dim newMapDownloadPosition = CInt(CUInt(vals("total downloaded")))
            Dim delta = newMapDownloadPosition - mapDownloadPosition
            If delta < 0 Then
                Disconnect(True, W3PlayerLeaveType.Disconnect, "auto-booted: moved download position backwards from {1} to {2}.".Frmt(mapDownloadPosition, newMapDownloadPosition))
                Return
            ElseIf newMapDownloadPosition > game.map.FileSize Then
                Disconnect(True, W3PlayerLeaveType.Disconnect, "auto-booted: moved download position past file size")
                Return
            ElseIf mapDownloadPosition = game.map.FileSize Then
                '[previously finished download]
                Return
            End If

            mapDownloadPosition = newMapDownloadPosition
            mapUploadPosition = Math.Max(mapDownloadPosition, mapUploadPosition)
            If Not knowMapState Then
                Dim hasMap = mapDownloadPosition = game.map.FileSize
                If Not hasMap AndAlso Not game.server.settings.allowDownloads Then
                    Disconnect(True, W3PlayerLeaveType.Disconnect, "no dls allowed")
                    Return
                End If

                game.DownloadScheduler.AddClient(index, hasMap).MarkAnyExceptionAsHandled()
                game.DownloadScheduler.SetLink(index, W3Game.LocalTransferClientKey, True).MarkAnyExceptionAsHandled()
                knowMapState = True
            ElseIf mapDownloadPosition = game.map.FileSize Then
                logger.Log("{0} finished downloading the map.".Frmt(name), LogMessageType.Positive)
                game.DownloadScheduler.StopTransfer(index, True).MarkAnyExceptionAsHandled()
            Else
                Dim d = CDbl(mapDownloadPosition)
                Contract.Assume(d >= 0)
                Contract.Assume(Not Double.IsNaN(d))
                Contract.Assume(Not Double.IsInfinity(d))
                game.DownloadScheduler.UpdateProgress(index, d).MarkAnyExceptionAsHandled()
                If IsGettingMapFromBot Then
                    BufferMap()
                End If
            End If

            game.QueueUpdatedGameState()
        End Sub
#End Region

#Region "Interface"
        Public ReadOnly Property GetMapDownloadPosition() As Integer
            Get
                Contract.Ensures(Contract.Result(Of Integer)() >= 0)
                Return mapDownloadPosition
            End Get
        End Property
        Public ReadOnly Property IsOverCounted() As Boolean
            Get
                Return countdowns > 1
            End Get
        End Property
        Public Function QueueBufferMap() As IFuture
            Contract.Ensures(Contract.Result(Of IFuture)() IsNot Nothing)
            Return ref.QueueAction(AddressOf BufferMap)
        End Function
        Public Function QueueStartCountdown() As IFuture
            Contract.Ensures(Contract.Result(Of IFuture)() IsNot Nothing)
            Return ref.QueueAction(AddressOf StartCountdown)
        End Function
#End Region

#Region "Misc"
        Private Sub StartCountdown()
            countdowns += 1
            If countdowns > 1 Then Return
            SendPacket(W3Packet.MakeStartCountdown())
        End Sub

        Private Sub BufferMap()
            Dim f_index = game.QueueGetFakeHostPlayer.Select(Function(player) If(player Is Nothing, CByte(0), player.index))
            f_index.CallOnValueSuccess(Sub(senderIndex) ref.QueueAction(
                Sub()
                    While mapUploadPosition < Math.Min(game.map.FileSize, mapDownloadPosition + MAX_BUFFERED_MAP_SIZE)
                        Dim out_DataSize = 0
                        Contract.Assume(senderIndex >= 0)
                        Contract.Assume(senderIndex <= 12)
                        Dim pk = W3Packet.MakeMapFileData(game.map, index, mapUploadPosition, out_DataSize, senderIndex)
                        mapUploadPosition += out_DataSize
                        Try
                            SendPacket(pk)
                        Catch e As Exception '[check this more thoroughly]
                            Exit While
                        End Try
                    End While
                End Sub
            ))
        End Sub
#End Region
    End Class
End Namespace