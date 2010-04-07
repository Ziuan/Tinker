Namespace Pickling
    Public NotInheritable Class KeyPrefixedJar(Of TKey)
        Inherits BaseJar(Of KeyValuePair(Of TKey, Object))

        Private ReadOnly _keyJar As IJar(Of TKey)
        Private ReadOnly _valueJars As New Dictionary(Of TKey, NonNull(Of ISimpleJar))
        Private ReadOnly _useSingleLineDescription As Boolean

        <ContractInvariantMethod()> Private Sub ObjectInvariant()
            Contract.Invariant(_keyJar IsNot Nothing)
            Contract.Invariant(_valueJars IsNot Nothing)
        End Sub

        Public Sub New(ByVal keyJar As IJar(Of TKey),
                       ByVal valueJars As Dictionary(Of TKey, ISimpleJar),
                       Optional ByVal useSingleLineDescription As Boolean = True)
            Contract.Requires(keyJar IsNot Nothing)
            Contract.Requires(valueJars IsNot Nothing)
            Me._keyJar = keyJar
            Me._valueJars = valueJars.ToDictionary(Function(e) e.Key, Function(e) e.Value.AsNonNull)
            Me._useSingleLineDescription = useSingleLineDescription
        End Sub

        <ContractVerification(False)>
        Public Overrides Function Pack(ByVal value As KeyValuePair(Of TKey, Object)) As IEnumerable(Of Byte)
            If Not _valueJars.ContainsKey(value.Key) Then Throw New PicklingException("No subjar with key {0}.".Frmt(value.Key))
            Dim keyData = _keyJar.Pack(value.Key)
            Dim valueData = _valueJars(value.Key).Value.Pack(value.Value)
            Return keyData.Concat(valueData)
        End Function
        <ContractVerification(False)>
        Public Overrides Function Parse(ByVal data As IReadableList(Of Byte)) As ParsedValue(Of KeyValuePair(Of TKey, Object))
            Dim parsedKey = _keyJar.Parse(data)
            If Not _valueJars.ContainsKey(parsedKey.Value) Then Throw New PicklingException("No subjar with key {0}.".Frmt(parsedKey.Value))
            Dim parsedValue = _valueJars(parsedKey.Value).Value.Parse(data.SubView(parsedKey.UsedDataCount))

            Dim value = parsedKey.Value.KeyValue(parsedValue.Value)
            Return value.ParsedWithDataCount(parsedKey.UsedDataCount + parsedValue.UsedDataCount)
        End Function

        <ContractVerification(False)>
        Public Overrides Function Describe(ByVal value As KeyValuePair(Of TKey, Object)) As String
            Dim keyDesc = _keyJar.Describe(value.Key)
            Dim valueDesc = _valueJars(value.Key).Value.Describe(value.Value)
            Return If(_useSingleLineDescription,
                      "{0}: {1}".Frmt(keyDesc, valueDesc),
                      {keyDesc, valueDesc}.StringJoin(Environment.NewLine))
        End Function
        Public Overrides Function Parse(ByVal text As String) As KeyValuePair(Of TKey, Object)
            Dim p = text.IndexOf(":"c)
            If p < 0 Then Throw New PicklingException("Expected key:value style.")
            Dim key = _keyJar.Parse(text.Substring(0, p).TrimEnd)
            If Not _valueJars.ContainsKey(key) Then Throw New PicklingException("No subjar with key {0}.".Frmt(key))
            Return key.KeyValue(_valueJars(key).Value.Parse(text.Substring(p + 1).TrimStart))
        End Function

        Public Overrides Function MakeControl() As IValueEditor(Of KeyValuePair(Of TKey, Object))
            Dim keyControl = _keyJar.MakeControl()
            If Not _valueJars.ContainsKey(keyControl.Value) Then
                keyControl.Value = _valueJars.Keys.First.AssumeNotNull
            End If
            Dim valueControl = _valueJars(keyControl.Value).Value.MakeControl()
            Dim panel = PanelWithControls({keyControl.Control, valueControl.Control}, borderStyle:=BorderStyle.FixedSingle)
            Dim handlers = New List(Of Action)

            Dim updateValueControl = Sub()
                                         valueControl = _valueJars(keyControl.Value).Value.MakeControl()
                                         panel.Controls.RemoveAt(1)
                                         panel.Controls.Add(valueControl.Control)
                                         LayoutPanel(panel, borderStyle:=BorderStyle.FixedSingle)
                                         For Each handler In handlers
                                             Dim h = handler
                                             AddHandler valueControl.ValueChanged, Sub() h()
                                         Next handler
                                         AddHandler valueControl.ValueChanged, Sub() LayoutPanel(panel, borderStyle:=BorderStyle.FixedSingle)
                                     End Sub
            AddHandler keyControl.ValueChanged, Sub() updateValueControl()

            Return New DelegatedValueEditor(Of KeyValuePair(Of TKey, Object))(
                Control:=panel,
                eventAdder:=Sub(action)
                                AddHandler keyControl.ValueChanged, Sub() action()
                                AddHandler valueControl.ValueChanged, Sub() action()
                                handlers.Add(action)
                            End Sub,
                getter:=Function() keyControl.Value.KeyValue(valueControl.Value),
                setter:=Sub(value)
                            If Not value.Key.Equals(keyControl.Value) Then keyControl.Value = value.Key
                            If Not value.Value.Equals(valueControl.Value) Then valueControl.Value = value.Value
                        End Sub,
                disposer:=Sub()
                              keyControl.Dispose()
                              valueControl.Dispose()
                              panel.Dispose()
                          End Sub)
        End Function
    End Class
End Namespace
