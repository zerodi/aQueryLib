Namespace aQueryLib
    ''' <summary>
    ''' A strongly typed collection for holding player information.
    ''' </summary>
    Public Class PlayerCollection
        Inherits System.Collections.CollectionBase
        ''' <summary>
        ''' Adds an item to the PlayerCollection.
        ''' </summary>
        ''' <param name="value">The Player to add to the PlayerCollection</param>
        ''' <returns>The position into which the new element was inserted.</returns>
        Public Function Add(ByVal value As Player) As Integer
            Return MyBase.List.Add(value)
        End Function

        ''' <summary>
        ''' Removes the first occurrence of a specific Player from the PlayerCollection.
        ''' </summary>
        ''' <param name="value">The Player to remove from the PlayerCollection</param>
        Public Sub Remove(ByVal value As Player)
            MyBase.List.Remove(value)
        End Sub

        ''' <summary>
        ''' Inserts an item to the PlayerCollection at the specified position.
        ''' </summary>
        ''' <param name="index">The zero-based index at which value should be inserted.</param>
        ''' <param name="value">The Player to insert into the PlayerCollection.</param>
        Public Sub Insert(ByVal index As Integer, ByVal value As Player)
            MyBase.List.Insert(index, value)
        End Sub

        ''' <summary>
        ''' Determines whether the PlayerCollection contains a specific value.
        ''' </summary>
        ''' <param name="value">The Player to locate in the PlayerCollection.</param>
        ''' <returns>true if the Player is found in the PlayerCollection; otherwise, false.</returns>
        Public Function Contains(ByVal value As Player) As Boolean
            Return MyBase.List.Contains(value)
        End Function

        ''' <summary>
        ''' Gets or sets the element at the specified index.
        ''' </summary>
        Default Public Property Item(ByVal index As Integer) As Player
            Get
                Return DirectCast(MyBase.List(index), Player)
            End Get
            Set(ByVal value As Player)
                MyBase.List(index) = value
            End Set
        End Property

        ''' <summary>
        ''' Creates a new instance of the PlayerCollection class.
        ''' </summary>
        Public Sub New()
            MyBase.New()
        End Sub
    End Class
End Namespace
