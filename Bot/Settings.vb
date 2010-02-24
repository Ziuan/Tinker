Namespace Bot
    Public Class Settings
        Private _clientProfiles As IReadableList(Of ClientProfile) = New List(Of ClientProfile)().AsReadableList
        Private _pluginProfiles As IReadableList(Of PluginProfile) = New List(Of PluginProfile)().AsReadableList
        Private ReadOnly lock As New Object()

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_clientProfiles IsNot Nothing)
            Contract.Invariant(_pluginProfiles IsNot Nothing)
        End Sub

        Public Sub UpdateProfiles(ByVal clientProfiles As IEnumerable(Of Bot.ClientProfile),
                                  ByVal pluginProfiles As IEnumerable(Of Bot.PluginProfile))
            Contract.Requires(clientProfiles IsNot Nothing)
            Contract.Requires(pluginProfiles IsNot Nothing)
            SyncLock lock
                Me._clientProfiles = clientProfiles.ToReadableList
                Me._pluginProfiles = pluginProfiles.ToReadableList
            End SyncLock
        End Sub
        <Pure()>
        Public ReadOnly Property ClientProfiles() As IReadableList(Of Bot.ClientProfile)
            Get
                Contract.Ensures(Contract.Result(Of IReadableList(Of Bot.ClientProfile))() IsNot Nothing)
                Return _clientProfiles
            End Get
        End Property
        <Pure()>
        Public ReadOnly Property PluginProfiles() As IReadableList(Of Bot.PluginProfile)
            Get
                Contract.Ensures(Contract.Result(Of IReadableList(Of Bot.PluginProfile))() IsNot Nothing)
                Return _pluginProfiles
            End Get
        End Property

#Region "Serialization"
        Private Const FormatMagic As UInteger = 7352
        Private Const FormatVersion As UInteger = 0
        Private Const FormatMinReadCompatibleVersion As UInteger = 0
        Private Const FormatMinWriteCompatibleVersion As UInteger = 0
        Public Sub WriteTo(ByVal writer As IO.BinaryWriter)
            Contract.Requires(writer IsNot Nothing)

            SyncLock lock
                'Header
                writer.Write(FormatMagic)
                writer.Write(FormatVersion)
                writer.Write(FormatMinWriteCompatibleVersion)

                'Data
                writer.Write(CUInt(_clientProfiles.Count))
                For Each profile In _clientProfiles
                    Contract.Assume(profile IsNot Nothing)
                    profile.Save(writer)
                Next profile
                writer.Write(CUInt(_pluginProfiles.Count))
                For Each profile In _pluginProfiles
                    Contract.Assume(profile IsNot Nothing)
                    profile.Save(writer)
                Next profile
            End SyncLock
        End Sub
        Public Sub ReadFrom(ByVal reader As IO.BinaryReader)
            Contract.Requires(reader IsNot Nothing)

            SyncLock lock
                'Header
                Dim writerMagic = reader.ReadUInt32()
                Dim writerFormatVersion = reader.ReadUInt32()
                Dim writerMinFormatVersion = reader.ReadUInt32()
                If writerMagic <> FormatMagic Then
                    Throw New IO.InvalidDataException("Corrupted profile data.")
                ElseIf writerFormatVersion < FormatMinReadCompatibleVersion Then
                    Throw New IO.InvalidDataException("Profile data is saved in an earlier non-forwards-compatible version.")
                ElseIf writerMinFormatVersion > FormatVersion Then
                    Throw New IO.InvalidDataException("Profile data is saved in a later non-backwards-compatible format.")
                End If

                'Data
                Dim clientProfiles = New List(Of ClientProfile)
                For repeat = 1UI To reader.ReadUInt32()
                    Dim p = New Bot.ClientProfile(reader)
                    clientProfiles.Add(p)
                Next repeat
                Dim pluginProfiles = New List(Of PluginProfile)
                For repeat = 1UI To reader.ReadUInt32()
                    Dim p = New Bot.PluginProfile(reader)
                    pluginProfiles.Add(p)
                Next repeat
                UpdateProfiles(clientProfiles, pluginProfiles)
            End SyncLock
        End Sub
#End Region
    End Class
End Namespace
