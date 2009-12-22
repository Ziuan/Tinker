Namespace Pickling.Jars
    '''<summary>Pickles lists of values, where the serialized form simply continues until there are no more items.</summary>
    Public NotInheritable Class RepeatingJar(Of T)
        Inherits BaseJar(Of IList(Of T))
        Private ReadOnly _subJar As IJar(Of T)

        Public Sub New(ByVal name As InvariantString,
                       ByVal subJar As IJar(Of T))
            MyBase.New(name)
            Contract.Requires(subJar IsNot Nothing)
            Me._subJar = subJar
        End Sub

        Public Overrides Function Pack(Of TValue As IList(Of T))(ByVal value As TValue) As IPickle(Of TValue)
            Dim pickles = (From e In value Select CType(_subJar.Pack(e), IPickle(Of T))).ToList()
            Dim data = Concat(From p In pickles Select p.Data.ToArray)
            Return New Pickle(Of TValue)(Me.Name, value, data.AsReadableList(), Function() Pickle(Of T).MakeListDescription(pickles))
        End Function

        Public Overrides Function Parse(ByVal data As IReadableList(Of Byte)) As IPickle(Of IList(Of T))
            'Parse
            Dim vals As New List(Of T)
            Dim pickles As New List(Of IPickle(Of Object))
            Dim curCount = data.Count
            Dim curOffset = 0
            'List Elements
            While curOffset < data.Count
                'Value
                Dim p = _subJar.Parse(data.SubView(curOffset, curCount))
                vals.Add(p.Value)
                pickles.Add(New Pickle(Of Object)(p.Value, p.Data, p.Description))
                'Size
                Dim n = p.Data.Count
                curCount -= n
                curOffset += n
            End While

            Return New Pickle(Of IList(Of T))(Me.Name, vals, data.SubView(0, curOffset), Function() Pickle(Of Object).MakeListDescription(pickles))
        End Function
    End Class
End Namespace