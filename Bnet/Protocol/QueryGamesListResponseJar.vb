Imports Tinker.Pickling

Namespace Bnet.Protocol
    Public Class QueryGamesListResponse
        Implements IEquatable(Of QueryGamesListResponse)

        Private ReadOnly _games As IRist(Of WC3.RemoteGameDescription)
        Private ReadOnly _result As QueryGameResponse

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_games IsNot Nothing)
        End Sub

        Public Sub New(result As QueryGameResponse, games As IRist(Of WC3.RemoteGameDescription))
            Contract.Requires(games IsNot Nothing)
            Me._games = games
            Me._result = result
        End Sub

        Public ReadOnly Property Games As IRist(Of WC3.RemoteGameDescription)
            Get
                Contract.Ensures(Contract.Result(Of IRist(Of WC3.RemoteGameDescription))() IsNot Nothing)
                Return _games
            End Get
        End Property
        Public ReadOnly Property Result As QueryGameResponse
            Get
                Return _result
            End Get
        End Property

        Public Overloads Function Equals(other As QueryGamesListResponse) As Boolean Implements IEquatable(Of QueryGamesListResponse).Equals
            If other Is Nothing Then Return False
            If Me.Result <> other.Result Then Return False
            If Not Me.Games.SequenceEqual(other.Games) Then Return False
            Return True
        End Function
        Public Overrides Function Equals(obj As Object) As Boolean
            Return Me.Equals(TryCast(obj, QueryGamesListResponse))
        End Function
        Public Overrides Function GetHashCode() As Integer
            Return _result.GetHashCode Xor _games.Aggregate(0, Function(acc, e) acc Xor e.GetHashCode)
        End Function
    End Class
    Public Class QueryGamesListResponseJar
        Inherits BaseJar(Of QueryGamesListResponse)

        Private Shared ReadOnly queryResultJar As INamedJar(Of QueryGameResponse) = New EnumUInt32Jar(Of QueryGameResponse)().Named("result")
        Private Shared ReadOnly gameDataJar As INamedJar(Of IRist(Of NamedValueMap)) =
            New EnumUInt32Jar(Of WC3.Protocol.GameTypes)().Named("game type").
            Then(New UInt32Jar().Named("language id")).
            Then(New IPEndPointJar().Named("host address")).
            Then(New EnumUInt32Jar(Of GameStates)().Named("game state")).
            Then(New UInt32Jar().Named("elapsed seconds")).
            Then(New UTF8Jar().NullTerminated.Named("game name")).
            Then(New UTF8Jar().NullTerminated.Named("game password")).
            Then(New TextHexUInt32Jar(digitCount:=1).Named("num free slots")).
            Then(New TextHexUInt32Jar(digitCount:=8).Named("game id")).
            Then(New WC3.Protocol.GameStatsJar().Named("game statstring")).
            Named("game").
            RepeatedWithCountPrefix(prefixSize:=4).
            Named("games")

        Private ReadOnly _clock As IClock

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_clock IsNot Nothing)
        End Sub

        Public Sub New(clock As IClock)
            Contract.Requires(clock IsNot Nothing)
            Me._clock = clock
        End Sub

        Public Overrides Function Pack(value As QueryGamesListResponse) As IRist(Of Byte)
            Contract.Assume(value IsNot Nothing)
            If value.Games.Count = 0 Then
                Return 0UI.Bytes().Concat(queryResultJar.Pack(value.Result))
            Else
                Return gameDataJar.Pack(PackRawGameDescriptions(value.Games))
            End If
        End Function

        Public Overrides Function Parse(data As IRist(Of Byte)) As ParsedValue(Of QueryGamesListResponse)
            If data.Count < 4 Then Throw New PicklingNotEnoughDataException("A QueryGamesListResponse requires at least 4 bytes.")
            If data.TakeExact(4).ToUInt32 = 0 Then
                'result of a single-game query
                Dim parsed = queryResultJar.Parse(data.SkipExact(4))
                Contract.Assume(data.Count >= 8)
                Return New QueryGamesListResponse(parsed.Value, MakeRist(Of WC3.RemoteGameDescription)()).ParsedWithDataCount(8)
            Else
                'result of a game search
                Dim parsed = gameDataJar.Parse(data)
                Return parsed.WithValue(New QueryGamesListResponse(QueryGameResponse.Ok,
                                                                   ParseRawGameDescriptions(parsed.Value, _clock)))
            End If
        End Function

        Public Overrides Function Describe(value As QueryGamesListResponse) As String
            Contract.Assume(value IsNot Nothing)
            Return MakeListDescription({queryResultJar.Describe(value.Result),
                                        gameDataJar.Describe(PackRawGameDescriptions(value.Games))})
        End Function
        Public Overrides Function Parse(text As String) As QueryGamesListResponse
            Dim lines = SplitListDescription(text)
            If lines.LazyCount <> 2 Then Throw New PicklingException("Incorrect number of lines.")
            Return New QueryGamesListResponse(queryResultJar.Parse(lines.First.AssumeNotNull),
                                              ParseRawGameDescriptions(gameDataJar.Parse(lines.Last.AssumeNotNull), _clock))
        End Function

        Private Shared Function PackRawGameDescriptions(games As IEnumerable(Of WC3.RemoteGameDescription)) As IRist(Of NamedValueMap)
            Contract.Requires(games IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IRist(Of NamedValueMap))() IsNot Nothing)
            Return (From game In games Select PackRawGameDescription(game)).ToRist
        End Function
        Private Shared Function ParseRawGameDescriptions(games As IEnumerable(Of NamedValueMap),
                                                         clock As IClock) As IRist(Of WC3.RemoteGameDescription)
            Contract.Requires(games IsNot Nothing)
            Contract.Requires(clock IsNot Nothing)
            Contract.Ensures(Contract.Result(Of IRist(Of WC3.RemoteGameDescription))() IsNot Nothing)
            Return games.Select(Function(game) ParseRawGameDescription(game, clock)).ToRist()
        End Function

        Private Shared Function PackRawGameDescription(game As WC3.RemoteGameDescription) As NamedValueMap
            Contract.Requires(game IsNot Nothing)
            Contract.Ensures(Contract.Result(Of NamedValueMap)() IsNot Nothing)
            Return New Dictionary(Of InvariantString, Object) From {
                    {"game type", game.GameType},
                    {"language id", 0UI},
                    {"host address", New Net.IPEndPoint(game.Address, game.Port)},
                    {"game state", game.GameState},
                    {"elapsed seconds", CUInt(game.AgeClock.ElapsedTime.TotalSeconds)},
                    {"game name", game.Name.ToString},
                    {"game password", ""},
                    {"num free slots", CUInt(game.TotalSlotCount - game.UsedSlotCount)},
                    {"game id", game.GameId},
                    {"game statstring", game.GameStats}}
        End Function
        Private Shared Function ParseRawGameDescription(vals As NamedValueMap, clock As IClock) As WC3.RemoteGameDescription
            Contract.Requires(vals IsNot Nothing)
            Contract.Requires(clock IsNot Nothing)
            Contract.Ensures(Contract.Result(Of WC3.RemoteGameDescription)() IsNot Nothing)
            Dim totalSlots = CInt(vals.ItemAs(Of UInt32)("num free slots"))
            If totalSlots <= 0 Then Throw New PicklingException("Total slots must be positive.")
            If totalSlots > 12 Then Throw New PicklingException("Total slots must be at most 12.")
            Dim gameId = vals.ItemAs(Of UInt32)("game id")
            If gameId <= 0 Then Throw New PicklingException("game id must be positive and non-zero.")
            Dim usedSlots = 0
            Return New WC3.RemoteGameDescription(Name:=vals.ItemAs(Of String)("game name"),
                                                 gamestats:=vals.ItemAs(Of WC3.GameStats)("game statstring"),
                                                 location:=vals.ItemAs(Of Net.IPEndPoint)("host address"),
                                                 gameId:=gameId,
                                                 entryKey:=0,
                                                 totalSlotCount:=totalSlots,
                                                 gameType:=vals.ItemAs(Of WC3.Protocol.GameTypes)("game type"),
                                                 state:=vals.ItemAs(Of GameStates)("game state"),
                                                 usedSlotCount:=usedSlots,
                                                 ageClock:=clock.StartingAt(vals.ItemAs(Of UInt32)("elapsed seconds").Seconds))
        End Function

        Public Overrides Function MakeControl() As IValueEditor(Of QueryGamesListResponse)
            Dim resultControl = queryResultJar.MakeControl()
            Dim gamesControl = gameDataJar.MakeControl()
            Dim panel = PanelWithControls({resultControl.Control, gamesControl.Control})
            Return New DelegatedValueEditor(Of QueryGamesListResponse)(
                Control:=panel,
                eventAdder:=Sub(action)
                                AddHandler resultControl.ValueChanged, Sub() action()
                                AddHandler gamesControl.ValueChanged, Sub() action()
                            End Sub,
                getter:=Function() New QueryGamesListResponse(resultControl.Value,
                                                              ParseRawGameDescriptions(gamesControl.Value, _clock)),
                setter:=Sub(value)
                            resultControl.SetValueIfDifferent(value.Result)
                            gamesControl.SetValueIfDifferent(PackRawGameDescriptions(value.Games))
                        End Sub,
                disposer:=Sub()
                              resultControl.Dispose()
                              gamesControl.Dispose()
                              panel.Dispose()
                          End Sub)
        End Function
    End Class
End Namespace
