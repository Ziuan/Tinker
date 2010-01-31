Imports Tinker.Pickling

Namespace Bnet.Protocol
    Public Class IPAddressJar
        Inherits BaseJar(Of Net.IPAddress)

        Public Sub New(ByVal name As InvariantString)
            MyBase.New(name)
        End Sub

        Public Overrides Function Pack(Of TValue As Net.IPAddress)(ByVal value As TValue) As IPickle(Of TValue)
            Contract.Assume(value IsNot Nothing)
            Dim data = value.GetAddressBytes()
            Return New Pickle(Of TValue)(Name, value, data.AsReadableList)
        End Function
        Public Overrides Function Parse(ByVal data As IReadableList(Of Byte)) As IPickle(Of System.Net.IPAddress)
            If data.Count < 4 Then Throw New PicklingException("Not enough data.")
            Dim datum = data.SubView(0, 4)
            Dim value = New Net.IPAddress(datum.ToArray)
            Return New Pickle(Of Net.IPAddress)(Name, value, datum)
        End Function
    End Class
End Namespace