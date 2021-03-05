#Region "Using directives"

Imports System
Imports System.Collections.Specialized

#End Region

Namespace aQueryLib
    ''' <summary>
    ''' Represents a player on a server
    ''' </summary>
    Public Class Player
        Private _name As String, _team As String
        Private _score As Integer, _ping As Integer
        Private _time As TimeSpan
        Private _params As StringDictionary

        ''' <summary>
        ''' Represents a player on a server
        ''' </summary>
        Public Sub New()
            _params = New StringDictionary()
        End Sub

        ''' <param name="name">Playername</param>
        ''' <param name="score">Playerscore</param>
        Public Sub New(ByVal name As String, ByVal score As Integer)
            Me.New()
            _name = name
            _score = score
        End Sub

        ''' <param name="name">Playername</param>
        ''' <param name="score">Playerscore</param>
        ''' <param name="ping">Playerping</param>
        Public Sub New(ByVal name As String, ByVal score As Integer, ByVal ping As Integer)
            Me.New(name, score)
            _ping = ping
        End Sub

        ''' <param name="name">Playername</param>
        ''' <param name="team">Teamname</param>
        ''' <param name="score">Playerscore</param>
        ''' <param name="ping">Playerping</param>
        Public Sub New(ByVal name As String, ByVal team As String, ByVal score As Integer, ByVal ping As Integer)
            Me.New(name, score, ping)
            _team = team
        End Sub

        ''' <summary>
        ''' Gets the playername
        ''' </summary>
        Public Property Name() As String
            Get
                Return _name
            End Get
            Set(ByVal value As String)
                _name = value
            End Set
        End Property

        ''' <summary>
        ''' Gets the playerscore
        ''' </summary>
        Public Property Score() As Integer
            Get
                Return _score
            End Get
            Set(ByVal value As Integer)
                _score = value
            End Set
        End Property

        ''' <summary>
        ''' Gets the ping of the player
        ''' </summary>
        Public Property Ping() As Integer
            Get
                Return _ping
            End Get
            Set(ByVal value As Integer)
                _ping = value
            End Set
        End Property

        ''' <summary>
        ''' Gets the team of the player
        ''' </summary>
        Public Property Team() As String
            Get
                Return _team
            End Get
            Set(ByVal value As String)
                _team = value
            End Set
        End Property

        ''' <summary>
        ''' Gets the time that the player is on the server
        ''' </summary>
        Public Property Time() As TimeSpan
            Get
                Return _time
            End Get
            Set(ByVal value As TimeSpan)
                _time = value
            End Set
        End Property

        ''' <summary>
        ''' Gets extended parameters
        ''' </summary>
        Public Property Parameters() As StringDictionary
            Get
                Return _params
            End Get
            Set(ByVal value As StringDictionary)
                _params = value
            End Set
        End Property
    End Class
End Namespace

